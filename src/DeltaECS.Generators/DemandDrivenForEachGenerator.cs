using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates only the ForEach shapes actually used by a consumer assembly.
/// </summary>
[Generator]
public sealed class DemandDrivenForEachGenerator : IIncrementalGenerator
{
    private const int FirstRefReadonlyParameterLanguageVersion = 1200;
    private static readonly ContextModeKind[] ContextModes =
    {
        ContextModeKind.Ref,
        ContextModeKind.In,
        ContextModeKind.RefReadonly,
        ContextModeKind.Value
    };

    private static readonly DiagnosticDescriptor Unsupported = new(
        "DECSGEN001",
        "Unsupported ForEach shape",
        "ForEach shape '{0}' is not supported by the demand-driven generator",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AmbiguousFunctor = new(
        "DECSGEN003",
        "Ambiguous ForEach functor",
        "Functor '{0}' has multiple supported Invoke overloads ({1}); keep exactly one Invoke implementation",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InaccessibleFunctor = new(
        "DECSGEN004",
        "ForEach functor is not accessible to generated code",
        "Functor '{0}' and its containing types must be at least internal",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InterceptionNotApplied = new(
        "DECSGEN005",
        "ForEach interception was not applied",
        "ForEach interception was not applied: {0}; the delegate fallback remains active",
        "ForEach",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            GeneratorPipeline.Input(context)
                .Combine(context.AnalyzerConfigOptionsProvider.Select(
                    static (provider, _) => GeneratorSupport.IsInterceptionEnabled(provider.GlobalOptions))),
            static (productionContext, input) => Execute(
                input.Left.Compilation,
                input.Left.Invocations,
                productionContext,
                input.Right));
    }

    private static void Execute(
        Compilation compilation,
        System.Collections.Immutable.ImmutableArray<InvocationCandidate> discoveredInvocations,
        SourceProductionContext context,
        bool interceptorsEnabled)
    {
        bool profiling = compilation.GetTypeByMetadataName("DeltaECS.Profiling.ProfilerRuntime") is not null
            && compilation.GetTypeByMetadataName("DeltaECS.Profiling.ProfiledMethodMetadataAttribute") is not null;
        bool languageSupportsInterceptors = GeneratorSupport.SupportsInterceptors(compilation);
        bool languageSupportsRefReadonlyParameters = SupportsRefReadonlyParameters(compilation);
        var shapes = new Dictionary<string, IterationModel>(StringComparer.Ordinal);
        var interceptionSites = new Dictionary<string, List<InterceptionSite>>(StringComparer.Ordinal);
        foreach (InvocationCandidate candidate in GeneratorSupport.ExcludeGenerated(discoveredInvocations))
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            SemanticModel model = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryReadShape(model, invocation, out IterationModel? shape, out Diagnostic? diagnostic))
            {
                if (diagnostic is not null)
                {
                    context.ReportDiagnostic(diagnostic);
                }

                continue;
            }

            if (shape is null)
            {
                continue;
            }

            string key = shape.Key;
            if (!shapes.ContainsKey(key))
            {
                shapes.Add(key, shape);
            }

            if (interceptorsEnabled && languageSupportsInterceptors && !shape.IsFunctor)
            {
                if (TryCreateInterceptionSite(model, invocation, shape, invocation.SyntaxTree, out InterceptionSite? site, out string? reason)
                    && site is { } interceptionSite)
                {
                    if (!interceptionSites.TryGetValue(key, out List<InterceptionSite>? sites))
                    {
                        sites = new List<InterceptionSite>();
                        interceptionSites.Add(key, sites);
                    }

                    sites.Add(interceptionSite);
                }
                else if (reason is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InterceptionNotApplied, invocation.GetLocation(), reason));
                }
            }
        }

        foreach (IGrouping<string, IterationModel> patternGroup in shapes.Values
            .OrderBy(static value => value.Key, StringComparer.Ordinal)
            .GroupBy(static value => value.Pattern, StringComparer.Ordinal))
        {
            bool renderContracts = true;
            foreach (IterationModel shape in patternGroup)
            {
                context.AddSource(
                    $"DemandForEach_{GeneratorSupport.StableName(shape.Key)}.g.cs",
                    DemandDrivenForEachTemplates.Render(new IterationRenderModel(
                        shape,
                        renderContracts,
                        profiling,
                        ContextModes
                            .Where(mode => mode != ContextModeKind.RefReadonly || languageSupportsRefReadonlyParameters)
                            .ToImmutableArray())));
                if (interceptionSites.TryGetValue(shape.Key, out List<InterceptionSite>? interceptorSites))
                {
                    foreach (InterceptionSite interceptorSite in interceptorSites.OrderBy(static site => site.Id, StringComparer.Ordinal))
                    {
                        context.AddSource(
                            $"DemandForEachInterceptor_{interceptorSite.Id}.g.cs",
                            DemandDrivenForEachTemplates.RenderInterceptorSource(interceptorSite));
                    }
                }
                if (!shape.IsFunctor)
                {
                    renderContracts = false;
                }
            }
        }
    }

    private static bool SupportsRefReadonlyParameters(Compilation compilation)
        => compilation.SyntaxTrees.FirstOrDefault()?.Options is CSharpParseOptions options
            && (int)options.LanguageVersion >= FirstRefReadonlyParameterLanguageVersion;

    private static bool TryCreateInterceptionSite(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        IterationModel shape,
        SyntaxTree tree,
        out InterceptionSite? site,
        out string? reason)
    {
        site = null;
        reason = null;

        LambdaExpressionSyntax? lambda = Lambda(invocation.ArgumentList.Arguments);
        IMethodSymbol? methodGroup = null;
        int callbackArgumentIndex = -1;
        if (lambda is null)
        {
            GenericNameSyntax? genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
            bool namedEntity = shape.HasEntity;
            ImmutableArray<ITypeSymbol?> expectedTypes = genericName is null || shape.IsStamp
                ? ImmutableArray<ITypeSymbol?>.Empty
                : genericName.TypeArgumentList.Arguments
                    .Select(argument => model.GetTypeInfo(argument).Type)
                    .ToImmutableArray();
            int expectedParameterCount = genericName is null
                ? -1
                : genericName.TypeArgumentList.Arguments.Count + (namedEntity ? 1 : 0);
            IMethodSymbol? resolvedMethod = null;
            for (int index = invocation.ArgumentList.Arguments.Count - 1; index >= 0; index--)
            {
                IMethodSymbol? candidate;
                bool resolved = expectedParameterCount < 0
                    ? CallbackReader.TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments[index].Expression, out candidate)
                    : CallbackReader.TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments[index].Expression, expectedParameterCount, expectedTypes, namedEntity, out candidate);
                if (resolved)
                {
                    resolvedMethod = candidate;
                    callbackArgumentIndex = index;
                    break;
                }
            }

            if (resolvedMethod is not { } methodTarget)
            {
                reason = "the callback is not a resolvable static method group";
                return false;
            }

            methodGroup = methodTarget;
            if (!methodTarget.IsStatic)
            {
                reason = "the method group target is an instance method";
                return false;
            }

            if (!CallbackReader.IsStaticMethodGroupExpression(model, invocation.ArgumentList.Arguments[callbackArgumentIndex].Expression))
            {
                reason = "the method group has an instance receiver";
                return false;
            }

            if (methodTarget.MethodKind != MethodKind.Ordinary
                || methodTarget.Arity != 0
                || methodTarget.ContainingType is null)
            {
                reason = "only non-generic ordinary static method groups are supported";
                return false;
            }

            if (!GeneratorSupport.IsAccessibleSymbol(methodTarget))
            {
                reason = "the static method group target is not accessible to generated callback code";
                return false;
            }

            if (methodTarget.Parameters.Any(parameter => !GeneratorSupport.IsAccessibleSymbol(parameter.Type)))
            {
                reason = "a static method group parameter type is not accessible to generated callback code";
                return false;
            }

        }

        if (lambda is not null
            && !lambda.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            reason = "the callback is not a static lambda";
            return false;
        }

        if (lambda is not null && lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword))
        {
            reason = "async lambdas are not supported by the synchronous functor path";
            return false;
        }

        if (shape.HasContext && string.IsNullOrEmpty(shape.ContextType))
        {
            reason = "the lambda context type could not be resolved";
            return false;
        }

        if (lambda is not null && !CallbackReader.HasAccessibleLambdaReferences(model, lambda, out reason))
        {
            return false;
        }

        if (!GeneratorSupport.TryGetInterceptionLocation(model, invocation, out string locationData, out string attributeSyntax))
        {
            reason = "Roslyn did not provide an interceptable location for this call";
            return false;
        }

        ISymbol? enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
        var usings = lambda is null
            ? Array.Empty<string>()
            : tree.GetRoot()
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(static directive => directive.ToString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        if (lambda is not null && enclosing is not null)
        {
            string namespaceName = enclosing.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (!string.IsNullOrEmpty(namespaceName))
            {
                usings = usings
                    .Append("using global::" + namespaceName + ";")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            if (enclosing.ContainingType is { Arity: 0 } containingType)
            {
                if (!GeneratorSupport.IsAccessibleSymbol(containingType))
                {
                    reason = "the containing type is not accessible to generated callback code";
                    return false;
                }

                usings = usings
                    .Append("using static " + containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ";")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        }

        string id = GeneratorSupport.StableName(shape.Key + "|" + locationData);
        site = new InterceptionSite(
            id,
            shape,
            lambda,
            methodGroup,
            attributeSyntax,
            usings);
        return true;
    }

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out IterationModel? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || !ApiDescriptor.TryGet(member.Name.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Iteration)
        {
            return false;
        }

        if (member.Expression is InvocationExpressionSyntax whereInvocation
            && whereInvocation.Expression is MemberAccessExpressionSyntax whereMember
            && whereMember.Name.Identifier.ValueText == "Where"
            && GeneratorSupport.IsEcsType(model.GetTypeInfo(whereMember.Expression).Type, "World"))
        {
            return false;
        }

        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool stamp = descriptor.Value == ValueDomain.Stamp;
        bool parallel = descriptor.Schedule == Schedule.Parallel;
        bool hasLambda = invocation.ArgumentList.Arguments.Any(static argument => argument.Expression is LambdaExpressionSyntax);
        if (!hasLambda)
        {
            if (TryReadMethodGroupShape(model, invocation, member, out shape, out diagnostic))
            {
                return true;
            }

            return TryReadFunctorShape(model, invocation, member, out shape, out diagnostic);
        }

        ITypeSymbol? receiverType = model.GetTypeInfo(member.Expression).Type;
        if (!GeneratorSupport.IsEcsType(receiverType, "World"))
        {
            return false;
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        var arguments = invocation.ArgumentList.Arguments;
        bool namedEntity = descriptor.HasEntity;
        int callbackArgumentIndex = FindCallbackArgumentIndex(arguments);
        bool queryRequired = arguments.Count == 0
            || !GeneratorSupport.IsEntityBatch(model.GetTypeInfo(arguments[0].Expression).Type);
        var cursor = new InvocationCursor(model, arguments, descriptor);
        if (!cursor.TryRead(callbackArgumentIndex, out InvocationCursorResult prefix, queryRequired))
        {
            return false;
        }
        bool hasContext = prefix.HasContext;
        ContextModeKind contextMode = prefix.ContextMode;
        if (!TryNormalizeParallelContext(parallel, contextMode, invocation, out contextMode, out diagnostic))
        {
            return false;
        }
        int prefixCount = hasContext ? 1 : 0;
        bool implicitComponents = genericName is null;
        LambdaExpressionSyntax? lambda = Lambda(arguments);
        ParameterSyntax[] lambdaParameters = CallbackReader.LambdaParameters(lambda);
        int lambdaParameterCount = lambdaParameters.Length;
        if (implicitComponents
            && namedEntity
            && lambdaParameterCount == prefixCount + 1
            && lambdaParameters[prefixCount].Type is null)
        {
            return false;
        }

        bool callbackHasEntity = lambdaParameterCount > prefixCount
            && CallbackReader.IsEntityParameter(model, lambdaParameters[prefixCount], allowImplicit: namedEntity);
        if (callbackHasEntity != namedEntity)
        {
            return false;
        }

        bool hasEntity = namedEntity;
        int callbackComponentCount = lambdaParameterCount - prefixCount - (hasEntity ? 1 : 0);
        int? genericComponentCount = genericName is null
            ? null
            : genericCount - prefixCount;
        var arityEvidence = new ArityEvidence();
        arityEvidence.Add(genericComponentCount);
        arityEvidence.Add(prefix.ComponentIdCount == 0 ? null : prefix.ComponentIdCount);
        arityEvidence.Add(callbackComponentCount);
        if (!arityEvidence.TryBind(descriptor.MinimumArity, out int componentCount))
        {
            return false;
        }
        string? accessPattern = InferPattern(arguments, componentCount, hasContext, hasEntity);
        if (stamp && accessPattern is not null)
        {
            accessPattern = NormalizeStampPattern(accessPattern);
        }

        if (accessPattern is null || accessPattern.Length != componentCount || accessPattern.Any(static c => c is not ('R' or 'W' or 'I' or 'V')))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        bool explicitIds = componentCount != 0 && prefix.ComponentIdCount == componentCount;
        var typeArguments = genericName?.TypeArgumentList.Arguments
            .Select(argument => model.GetTypeInfo(argument).Type is { } type
                ? GeneratorSupport.DisplayType(type)
                : argument.ToString())
            .ToArray()
            ?? LambdaComponentTypes(model, lambda, prefixCount, hasEntity);
        int componentStart = genericName is null ? 0 : prefixCount;
        var components = typeArguments.Skip(componentStart).ToArray();
        if (components.Length != componentCount)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        if (stamp)
        {
            if (genericName is null && !explicitIds)
            {
                diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
                return false;
            }

            ParameterSyntax[] stampParameters = lambdaParameters
                .Skip(prefixCount + (hasEntity ? 1 : 0))
                .ToArray();
            if (stampParameters.Length != componentCount
                || stampParameters.Any(static parameter => !IsStampSyntax(parameter)))
            {
                diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
                return false;
            }
        }

        string? lambdaContextType = null;
        if (hasContext)
        {
            TypeSyntax? contextSyntax = genericName is not null && genericName.TypeArgumentList.Arguments.Count > 0
                ? genericName.TypeArgumentList.Arguments[0]
                : lambdaParameters.Length > 0
                    ? lambdaParameters[0].Type
                : null;
            lambdaContextType = contextSyntax is not null
                ? model.GetTypeInfo(contextSyntax).Type is { } type
                    ? GeneratorSupport.DisplayType(type)
                : null
                : null;

            ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
            if (parameters.Length == 0)
            {
                diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
                return false;
            }

            ContextModeKind callbackMode = CallbackReader.ContextMode(CallbackReader.ParameterRefKind(parameters[0]));
            if (!CallbackReader.AreCompatibleContextModes(contextMode, callbackMode))
            {
                diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
                return false;
            }

            contextMode = NormalizeParallelContext(parallel, contextMode);
        }

        shape = new IterationModel(
            explicitIds ? RegistrationBindingKind.Explicit : RegistrationBindingKind.Primary,
            hasEntity || LambdaHasEntity(model, arguments, componentCount, hasContext),
            hasContext,
            isFunctor: false,
            accessPattern,
            components,
            functorType: null,
            contextType: lambdaContextType,
            parallel: parallel,
            contextMode: contextMode,
            methodName: member.Name.Identifier.ValueText,
            hasEntityTarget: prefix.HasTarget,
            hasQuery: prefix.HasQuery,
            isStamp: stamp,
            typeBinding: genericName is not null
                ? TypeBindingKind.Generic
                : TypeBindingKind.CallbackInferred);
        return true;
    }

    private static bool TryReadMethodGroupShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out IterationModel? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        if (!ApiDescriptor.TryGet(member.Name.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Iteration)
        {
            return false;
        }

        int callbackArgumentIndex = -1;
        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool namedEntity = descriptor.HasEntity;
        bool stampName = descriptor.Value == ValueDomain.Stamp;
        ImmutableArray<ITypeSymbol?> expectedTypes = genericName is null || stampName
            ? ImmutableArray<ITypeSymbol?>.Empty
            : genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .ToImmutableArray();
        int expectedMethodParameterCount = genericName is null
            ? -1
            : genericName.TypeArgumentList.Arguments.Count + (namedEntity ? 1 : 0);
        for (int index = invocation.ArgumentList.Arguments.Count - 1; index >= 0; index--)
        {
            if ((expectedMethodParameterCount < 0
                    ? CallbackReader.TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments[index].Expression, out _)
                    : CallbackReader.TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments[index].Expression, expectedMethodParameterCount, expectedTypes, namedEntity, out _)))
            {
                callbackArgumentIndex = index;
                break;
            }
        }

        if (callbackArgumentIndex < 0)
        {
            return false;
        }

        ArgumentSyntax callback = invocation.ArgumentList.Arguments[callbackArgumentIndex];
        IMethodSymbol? method;
        bool resolved = expectedMethodParameterCount < 0
            ? CallbackReader.TryGetMethodGroupTarget(model, callback.Expression, out method)
            : CallbackReader.TryGetMethodGroupTarget(model, callback.Expression, expectedMethodParameterCount, expectedTypes, namedEntity, out method);
        if (callback.RefKindKeyword.RawKind != 0
            || !resolved
            || method is not { } methodTarget)
        {
            return false;
        }

        if (!GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "World")
            || methodTarget.MethodKind != MethodKind.Ordinary
            || methodTarget.Arity != 0
            || !methodTarget.ReturnsVoid
            || methodTarget.ContainingType is null
            || methodTarget.Parameters.Any(static parameter => parameter.IsParams
                || !CallbackReader.IsSupportedRefKind(parameter.RefKind)))
        {
            return false;
        }

        bool stamp = descriptor.Value == ValueDomain.Stamp;
        bool parallel = descriptor.Schedule == Schedule.Parallel;
        namedEntity = descriptor.HasEntity;
        var arguments = invocation.ArgumentList.Arguments;
        bool queryRequired = arguments.Count == 0
            || !GeneratorSupport.IsEntityBatch(model.GetTypeInfo(arguments[0].Expression).Type);
        var cursor = new InvocationCursor(model, arguments, descriptor);
        if (!cursor.TryRead(callbackArgumentIndex, out InvocationCursorResult prefix, queryRequired))
        {
            return false;
        }
        bool hasEntityTarget = prefix.HasTarget;
        bool hasQuery = prefix.HasQuery;
        int componentIdCount = prefix.ComponentIdCount;
        bool hasContext = prefix.HasContext;
        ContextModeKind contextMode = prefix.ContextMode;
        if (!TryNormalizeParallelContext(parallel, contextMode, invocation, out contextMode, out diagnostic))
        {
            return false;
        }
        int parameterIndex = 0;
        if (hasContext)
        {
            if (methodTarget.Parameters.Length == 0
                || !CallbackReader.AreCompatibleContextModes(contextMode, CallbackReader.ContextMode(methodTarget.Parameters[0].RefKind)))
            {
                return false;
            }

            contextMode = CallbackReader.ContextMode(methodTarget.Parameters[0].RefKind);
            contextMode = NormalizeParallelContext(parallel, contextMode);
            parameterIndex++;
        }

        bool hasEntity = false;
        if (namedEntity)
        {
            if (methodTarget.Parameters.Length <= parameterIndex
                || methodTarget.Parameters[parameterIndex].RefKind != RefKind.None
                || !GeneratorSupport.IsEntityType(methodTarget.Parameters[parameterIndex].Type))
            {
                return false;
            }

            hasEntity = true;
            parameterIndex++;
        }

        IParameterSymbol[] componentParameters = methodTarget.Parameters.Skip(parameterIndex).ToArray();
        if (!namedEntity && componentParameters.Any(static parameter => GeneratorSupport.IsEntityType(parameter.Type)))
        {
            return false;
        }

        if (stamp && componentParameters.Any(static parameter =>
                !GeneratorSupport.IsStampType(parameter.Type)
                || parameter.RefKind is not (RefKind.In or (RefKind)4 or (RefKind)5)))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        int expectedGenericCount = componentParameters.Length + (hasContext ? 1 : 0);
        if (genericName is not null && genericCount != expectedGenericCount)
        {
            return false;
        }

        var arityEvidence = new ArityEvidence();
        arityEvidence.Add(genericName is null ? null : genericCount - (hasContext ? 1 : 0));
        arityEvidence.Add(componentIdCount == 0 ? null : componentIdCount);
        arityEvidence.Add(componentParameters.Length);
        if (!arityEvidence.TryBind(descriptor.MinimumArity, out _))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        string[] components;
        string? contextType = hasContext
            ? GeneratorSupport.DisplayType(methodTarget.Parameters[0].Type)
            : null;
        if (genericName is not null)
        {
            ITypeSymbol?[] requestedTypes = genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .ToArray();
            if (hasContext
                && (requestedTypes[0] is null
                    || !SymbolEqualityComparer.Default.Equals(requestedTypes[0], methodTarget.Parameters[0].Type)))
            {
                return false;
            }

            int requestedComponentStart = hasContext ? 1 : 0;
            int methodComponentStart = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
            for (int index = 0; index < componentParameters.Length; index++)
            {
                ITypeSymbol? requestedType = requestedTypes[requestedComponentStart + index];
                ITypeSymbol expectedType = methodTarget.Parameters[methodComponentStart + index].Type;
                if (requestedType is null
                    || (!stamp && !SymbolEqualityComparer.Default.Equals(requestedType, expectedType)))
                {
                    return false;
                }
            }

            components = new string[componentParameters.Length];
            for (int index = 0; index < components.Length; index++)
            {
                ITypeSymbol requestedType = requestedTypes[requestedComponentStart + index] is { } type
                    ? type
                    : ThrowHelper.ThrowValidatedComponentTypeUnavailable();
                components[index] = GeneratorSupport.DisplayType(requestedType);
            }

            if (hasContext)
            {
                contextType = requestedTypes[0] is { } requestedContextType
                    ? GeneratorSupport.DisplayType(requestedContextType)
                    : ThrowHelper.ThrowValidatedContextTypeUnavailable();
            }
        }
        else
        {
            components = componentParameters
                .Select(static parameter => GeneratorSupport.DisplayType(parameter.Type))
                .ToArray();
        }

        if (stamp && genericName is null && componentIdCount == 0)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        string pattern = new(componentParameters.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray());
        if (stamp)
        {
            pattern = NormalizeStampPattern(pattern);
        }

        shape = new IterationModel(
            componentIdCount == componentParameters.Length && componentIdCount != 0
                ? RegistrationBindingKind.Explicit
                : RegistrationBindingKind.Primary,
            hasEntity,
            hasContext,
            isFunctor: false,
            pattern,
            components,
            functorType: null,
            contextType,
            parallel: parallel,
            contextMode: contextMode,
            methodName: member.Name.Identifier.ValueText,
            hasEntityTarget: hasEntityTarget,
            hasQuery: hasQuery,
            isStamp: stamp,
            typeBinding: genericName is not null
                ? TypeBindingKind.Generic
                : TypeBindingKind.CallbackInferred);
        return true;
    }

    private static bool TryReadFunctorShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out IterationModel? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (!GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "World")
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        int functorArgumentIndex = -1;
        INamedTypeSymbol? functorType = null;
        for (int index = invocation.ArgumentList.Arguments.Count - 1; index >= 0; index--)
        {
            ArgumentSyntax candidate = invocation.ArgumentList.Arguments[index];
            if (!candidate.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || model.GetTypeInfo(candidate.Expression).Type is not INamedTypeSymbol candidateType
                || !CallbackReader.TryGetForEachMarker(candidateType, out _, out _, out _))
            {
                continue;
            }

            functorArgumentIndex = index;
            functorType = candidateType;
            break;
        }

        if (functorArgumentIndex < 0 || functorType is null)
        {
            return false;
        }

        if (!CallbackReader.TryGetForEachMarker(functorType, out bool hasContext, out bool hasEntity, out ITypeSymbol? contextType))
        {
            return false;
        }

        if (!GeneratorSupport.IsAccessibleSymbol(functorType))
        {
            diagnostic = Diagnostic.Create(InaccessibleFunctor, invocation.GetLocation(), functorType.Name);
            return false;
        }

        if (!ApiDescriptor.TryGet(member.Name.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Iteration)
        {
            return false;
        }

        bool stamp = descriptor.Value == ValueDomain.Stamp;
        bool parallel = descriptor.Schedule == Schedule.Parallel;
        bool namedEntity = descriptor.HasEntity;
        if (namedEntity != hasEntity)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        bool queryRequired = arguments.Count == 0
            || !GeneratorSupport.IsEntityBatch(model.GetTypeInfo(arguments[0].Expression).Type);
        var cursor = new InvocationCursor(model, arguments, descriptor);
        if (!cursor.TryRead(functorArgumentIndex, out InvocationCursorResult prefix, queryRequired))
        {
            return false;
        }
        bool hasEntityTarget = prefix.HasTarget;
        bool hasQuery = prefix.HasQuery;
        int componentIdCount = prefix.ComponentIdCount;
        int contextArgumentIndex = prefix.ContextIndex;
        ContextModeKind contextMode = prefix.ContextMode;
        if (hasContext)
        {
            if (invocation.ArgumentList.Arguments.Count <= contextArgumentIndex
                || invocation.ArgumentList.Arguments[contextArgumentIndex].Expression is null)
            {
                diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
                return false;
            }

            contextMode = CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(invocation.ArgumentList.Arguments[contextArgumentIndex]));
        }
        if (!TryNormalizeParallelContext(parallel, contextMode, invocation, out contextMode, out diagnostic))
        {
            return false;
        }

        IMethodSymbol[] invokes = functorType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && method.ReturnsVoid)
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false))
            .Where(method => method.Parameters.Skip((hasContext ? 1 : 0) + (hasEntity ? 1 : 0)).All(
                static parameter => CallbackReader.IsSupportedRefKind(parameter.RefKind)))
            .ToArray();
        if (invokes.Length != 1)
        {
            string patterns = invokes.Length == 0
                ? "none"
                : string.Join(", ", invokes.Select(FunctorSignature).OrderBy(static value => value, StringComparer.Ordinal));
            diagnostic = invokes.Length > 1
                ? Diagnostic.Create(AmbiguousFunctor, invocation.GetLocation(), functorType.Name, patterns)
                : Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        IMethodSymbol invoke = invokes[0];
        ContextModeKind invokeContextMode = hasContext
            ? CallbackReader.ContextMode(invoke.Parameters[0].RefKind)
            : ContextModeKind.None;
        if (hasContext && !CallbackReader.AreCompatibleContextModes(contextMode, invokeContextMode))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }
        contextMode = NormalizeParallelContext(parallel, invokeContextMode);
        int prefixCount = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IParameterSymbol[] componentParameters = invoke.Parameters.Skip(prefixCount).ToArray();
        if (stamp && componentParameters.Any(static parameter =>
                !GeneratorSupport.IsStampType(parameter.Type)
                || parameter.RefKind is not (RefKind.In or (RefKind)4 or (RefKind)5)))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool genericSelectors = genericName is not null;
        if (stamp && !genericSelectors && componentIdCount == 0)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        string pattern = new(componentParameters.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray());
        string[] components = genericSelectors
            ? genericName!.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type is { } type ? GeneratorSupport.DisplayType(type) : argument.ToString())
                .ToArray()
            : componentParameters.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray();
        if (genericSelectors && components.Length != componentParameters.Length)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        var arityEvidence = new ArityEvidence();
        arityEvidence.Add(genericSelectors ? components.Length : null);
        arityEvidence.Add(componentIdCount == 0 ? null : componentIdCount);
        arityEvidence.Add(componentParameters.Length);
        if (!arityEvidence.TryBind(descriptor.MinimumArity, out _))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }
        shape = new IterationModel(
            componentIdCount != 0
                ? RegistrationBindingKind.Explicit
                : RegistrationBindingKind.Primary,
            hasEntity,
            hasContext,
            isFunctor: true,
            pattern,
            components,
            GeneratorSupport.DisplayType(functorType),
            contextType is { } resolvedContextType ? GeneratorSupport.DisplayType(resolvedContextType) : null,
            parallel: parallel,
            contextMode: contextMode,
            methodName: member.Name.Identifier.ValueText,
            hasEntityTarget: hasEntityTarget,
            hasQuery: hasQuery,
            isStamp: stamp,
            typeBinding: genericSelectors
                ? TypeBindingKind.Generic
                : TypeBindingKind.CallbackInferred);
        return true;
    }

    private static string FunctorSignature(IMethodSymbol method)
        => string.Join(string.Empty, method.Parameters.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)));

    private static bool TryNormalizeParallelContext(
        bool parallel,
        ContextModeKind mode,
        InvocationExpressionSyntax invocation,
        out ContextModeKind normalized,
        out Diagnostic? diagnostic)
    {
        normalized = NormalizeParallelContext(parallel, mode);
        diagnostic = parallel && mode == ContextModeKind.Ref
            ? Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation)
            : null;
        return diagnostic is null;
    }

    private static ContextModeKind NormalizeParallelContext(bool parallel, ContextModeKind mode)
        => parallel && mode == ContextModeKind.RefReadonly ? ContextModeKind.In : mode;

    private static bool IsStampSyntax(ParameterSyntax parameter)
        => parameter.Type?.ToString() is "Stamp" or "global::Delta.ECS.Stamp"
            && CallbackReader.ParameterRefKind(parameter) is RefKind.In or (RefKind)4 or (RefKind)5;

    private static string NormalizeStampPattern(string pattern)
        => pattern.Replace('R', 'I');

    private static int FindCallbackArgumentIndex(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => arguments.IndexOf(arguments.First(static argument => argument.Expression is LambdaExpressionSyntax));

    private static LambdaExpressionSyntax? Lambda(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

    private static string[] LambdaComponentTypes(
        SemanticModel model,
        LambdaExpressionSyntax? lambda,
        int prefixCount,
        bool hasEntity)
    {
        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        int start = prefixCount + (hasEntity ? 1 : 0);
        var result = new string[Math.Max(0, parameters.Length - start)];
        for (int index = 0; index < result.Length; index++)
        {
            ITypeSymbol? type = parameters[index + start].Type is { } syntax
                ? model.GetTypeInfo(syntax).Type
                : null;
            if (type is null)
            {
                return Array.Empty<string>();
            }

            result[index] = GeneratorSupport.DisplayType(type);
        }

        return result;
    }

    private static string? InferPattern(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int componentCount,
        bool hasContext,
        bool hasEntity)
    {
        LambdaExpressionSyntax? lambda = arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
        if (lambda is null)
        {
            return new string('W', componentCount);
        }

        var parameters = CallbackReader.LambdaParameters(lambda);
        int start = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);

        var result = new char[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            result[index] = parameters.Length > index + start
                ? GeneratorSupport.PatternLetter(parameters[index + start])
                : 'W';
        }

        return new string(result);
    }

    private static bool LambdaHasEntity(
        SemanticModel model,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int componentCount,
        bool hasContext)
    {
        LambdaExpressionSyntax? lambda = arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
        if (lambda is not ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            return false;
        }

        int expected = componentCount + (hasContext ? 1 : 0);
        if (parenthesized.ParameterList.Parameters.Count != expected + 1)
        {
            return false;
        }

        int entityIndex = hasContext ? 1 : 0;
        TypeSyntax? entityType = parenthesized.ParameterList.Parameters[entityIndex].Type;
        if (entityType is null)
        {
            return false;
        }

        ITypeSymbol? type = model.GetTypeInfo(entityType).Type;
        return type is not null && GeneratorSupport.IsEntityType(type);
    }

}
