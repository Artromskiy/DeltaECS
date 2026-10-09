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

    private static readonly DiagnosticDescriptor Unsupported = GeneratorDiagnostics.Error(
        "DECSGEN001",
        "Unsupported ForEach shape",
        "ForEach shape '{0}' is not supported by the demand-driven generator",
        "ForEach");
    private static readonly DiagnosticDescriptor ExplicitComponentTypesRequired = GeneratorDiagnostics.Error(
        "DECSGEN011",
        "ForEach component types must remain explicit",
        "ForEach component parameter types must be explicit when generic type arguments are omitted; the generator needs them to identify component rows",
        "ForEach");
    private static readonly DiagnosticDescriptor AmbiguousFunctor = GeneratorDiagnostics.Error(
        "DECSGEN003",
        "Ambiguous ForEach functor",
        "Functor '{0}' has multiple supported Invoke overloads ({1}); keep exactly one Invoke implementation",
        "ForEach");
    private static readonly DiagnosticDescriptor InaccessibleFunctor = GeneratorDiagnostics.Error(
        "DECSGEN004",
        "ForEach functor is not accessible to generated code",
        "Functor '{0}' and its containing types must be at least internal",
        "ForEach");
    private static readonly DiagnosticDescriptor InterceptionNotApplied = GeneratorDiagnostics.Info(
        "DECSGEN005",
        "ForEach interception was not applied",
        "ForEach interception was not applied: {0}; the delegate fallback remains active",
        "ForEach");

    public void Initialize(IncrementalGeneratorInitializationContext context)
        => GeneratorPipeline.RegisterInterceptorInput(context, Execute);

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
        bool languageSupportsScoped = GeneratorSupport.SupportsScoped(compilation);
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

            if (shape.OrderedQueryReceiver)
            {
                IterationModel entityListShape = OrderedEntityListShape(shape);
                if (!shapes.ContainsKey(entityListShape.Key))
                {
                    shapes.Add(entityListShape.Key, entityListShape);
                }
            }

            if (interceptorsEnabled && languageSupportsInterceptors && !shape.IsFunctor && !shape.OrderedQueryReceiver)
            {
                if (TryCreateInterceptionSite(model, invocation, shape, out InterceptionSite? site, out string? reason)
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
            .GroupBy(static value => value.Namespace + "|" + value.Pattern, StringComparer.Ordinal))
        {
            bool renderContracts = true;
            foreach (IterationModel shape in patternGroup)
            {
                context.AddSource(
                    $"DemandForEach_{GeneratorSupport.StableName(shape.Key)}.g.cs",
                    DemandDrivenForEachTemplates.Render(new IterationRenderModel(
                        shape,
                        renderContracts && !shape.OrderedQueryReceiver,
                        profiling,
                        languageSupportsScoped,
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
                if (!shape.IsFunctor && !shape.OrderedQueryReceiver)
                {
                    renderContracts = false;
                }
            }
        }
    }

    private static IterationModel OrderedEntityListShape(IterationModel shape)
        => new(
            shape.RegistrationBinding,
            shape.HasEntity,
            shape.HasContext,
            shape.IsFunctor,
            shape.Pattern,
            shape.Components,
            shape.FunctorType,
            shape.ContextType,
            parallel: false,
            contextMode: shape.ContextMode,
            methodName: shape.MethodName,
            hasEntityTarget: true,
            hasQuery: true,
            isStamp: shape.IsStamp,
            typeBinding: shape.Api.Selector.TypeBinding,
            functorPassMode: shape.FunctorPassMode,
            namespaceName: shape.Namespace);

    private static bool SupportsRefReadonlyParameters(Compilation compilation)
        => compilation.SyntaxTrees.FirstOrDefault()?.Options is CSharpParseOptions options
            && (int)options.LanguageVersion >= FirstRefReadonlyParameterLanguageVersion;

    private static bool TryCreateInterceptionSite(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        IterationModel shape,
        out InterceptionSite? site,
        out string? reason)
    {
        site = null;
        reason = null;

        if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol
            {
                IsExtensionMethod: false,
                ContainingType.Name: "World",
                ContainingType.ContainingNamespace: { } @namespace
            }
            && @namespace.ToDisplayString() == "Delta.ECS")
        {
            reason = "the call targets a World instance method with its own operation type";
            return false;
        }

        LambdaExpressionSyntax? lambda = invocation.ArgumentList.Arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
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
                    : CallbackReader.TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments[index].Expression, expectedParameterCount, expectedTypes, namedEntity, entityRef: true, out candidate);
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

        bool hasLambda = lambda is not null;
        if (!GeneratorSupport.TryGetInterceptionUsings(
                model,
                invocation,
                new[] { lambda },
                new[] { methodGroup },
                includeSourceUsings: hasLambda,
                includeEnclosingUsings: hasLambda,
                rejectInaccessibleContainingType: hasLambda,
                out string[] usings))
        {
            reason = "the containing type is not accessible to generated callback code";
            return false;
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
        bool isOrderedWhereReceiver = OrderedQueryInvocationGrammar.TryReadOrderedWhereSource(
            member.Expression,
            model,
            out PredicateModel? orderedWhereSource);
        bool orderedQueryReceiver = isOrderedWhereReceiver
            || GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "OrderedQuery");
        if (!GeneratorSupport.IsEcsType(model.GetTypeInfo(member.Expression).Type, "World")
            && !orderedQueryReceiver)
        {
            return false;
        }

        if (orderedQueryReceiver && (parallel || stamp))
        {
            return false;
        }

        bool hasLambda = invocation.ArgumentList.Arguments.Any(static argument => argument.Expression is LambdaExpressionSyntax);
        if (!hasLambda)
        {
            if (TryReadMethodGroupShape(model, invocation, member, descriptor, orderedQueryReceiver, orderedWhereSource, out shape, out diagnostic))
            {
                return true;
            }

            return TryReadFunctorShape(model, invocation, member, descriptor, orderedQueryReceiver, orderedWhereSource, out shape, out diagnostic);
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        var arguments = invocation.ArgumentList.Arguments;
        bool namedEntity = descriptor.HasEntity;
        int callbackArgumentIndex = FindCallbackArgumentIndex(arguments);
        if (!TryReadPrefix(model, invocation, descriptor, orderedQueryReceiver, callbackArgumentIndex, out InvocationCursorResult prefix))
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
        LambdaExpressionSyntax lambda = (LambdaExpressionSyntax)arguments[callbackArgumentIndex].Expression;
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
            && CallbackReader.IsEntityRefParameter(model, lambdaParameters[prefixCount], allowImplicit: namedEntity);
        if (callbackHasEntity != namedEntity)
        {
            return false;
        }

        bool hasEntity = namedEntity;
        int callbackComponentCount = lambdaParameterCount - prefixCount - (hasEntity ? 1 : 0);
        int? genericComponentCount = genericName is null
            ? null
            : genericCount - prefixCount;
        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out int componentCount,
                genericComponentCount,
                prefix.ComponentIdCount == 0 ? null : prefix.ComponentIdCount,
                callbackComponentCount))
        {
            return false;
        }

        int componentParameterStart = prefixCount + (hasEntity ? 1 : 0);
        if (genericName is null
            && componentCount != 0
            && lambdaParameters
                .Skip(componentParameterStart)
                .Take(componentCount)
                .Any(static parameter => parameter.Type is null))
        {
            return Reject(ExplicitComponentTypesRequired, invocation, invocation, out diagnostic);
        }

        string? accessPattern = InferPattern(lambda, componentCount, hasContext, hasEntity);
        if (stamp && accessPattern is not null)
        {
            accessPattern = NormalizeStampPattern(accessPattern);
        }

        if (accessPattern is null || accessPattern.Length != componentCount || accessPattern.Any(static c => c is not ('R' or 'W' or 'I' or 'V')))
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        bool explicitIds = componentCount != 0
            && (prefix.HasComponentIdSpan || prefix.ComponentIdCount == componentCount);
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
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        if (stamp)
        {
            if (genericName is null && !explicitIds)
            {
                return Reject(Unsupported, invocation, invocation, out diagnostic);
            }

            ParameterSyntax[] stampParameters = lambdaParameters
                .Skip(prefixCount + (hasEntity ? 1 : 0))
                .ToArray();
            if (stampParameters.Length != componentCount
                || stampParameters.Any(static parameter => !IsStampSyntax(parameter)))
            {
                return Reject(Unsupported, invocation, invocation, out diagnostic);
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
                return Reject(Unsupported, invocation, invocation, out diagnostic);
            }

            ContextModeKind callbackMode = CallbackReader.ContextMode(CallbackReader.ParameterRefKind(parameters[0]));
            if (!CallbackReader.AreCompatibleContextModes(contextMode, callbackMode))
            {
                return Reject(Unsupported, invocation, invocation, out diagnostic);
            }

            contextMode = NormalizeParallelContext(parallel, contextMode);
        }

        shape = new IterationModel(
            prefix.RegistrationBinding,
            hasEntity,
            hasContext,
            isFunctor: false,
            accessPattern,
            components,
            functorType: null,
            contextType: lambdaContextType,
            parallel: parallel,
            contextMode: contextMode,
            methodName: member.Name.Identifier.ValueText,
            hasEntityTarget: orderedQueryReceiver || prefix.HasTarget,
            hasQuery: orderedQueryReceiver || prefix.HasQuery,
            isStamp: stamp,
            typeBinding: genericName is not null ? TypeBindingKind.Generic : TypeBindingKind.CallbackInferred,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation),
            orderedQueryReceiver: orderedQueryReceiver,
            orderedWhereSource: orderedWhereSource);
        return true;
    }

    private static bool TryReadMethodGroupShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        ApiDescriptor descriptor,
        bool orderedQueryReceiver,
        PredicateModel? orderedWhereSource,
        out IterationModel? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        int callbackArgumentIndex = -1;
        IMethodSymbol? methodTarget = null;
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
            ExpressionSyntax expression = invocation.ArgumentList.Arguments[index].Expression;
            IMethodSymbol? candidate;
            bool resolved = expectedMethodParameterCount < 0
                ? CallbackReader.TryGetMethodGroupTarget(model, expression, out candidate)
                : CallbackReader.TryGetMethodGroupTarget(model, expression, expectedMethodParameterCount, expectedTypes, namedEntity, entityRef: true, out candidate);
            if (resolved)
            {
                callbackArgumentIndex = index;
                methodTarget = candidate;
                break;
            }
        }

        if (callbackArgumentIndex < 0 || methodTarget is null)
        {
            return false;
        }

        ArgumentSyntax callback = invocation.ArgumentList.Arguments[callbackArgumentIndex];
        if (!callback.RefKindKeyword.IsKind(SyntaxKind.None))
        {
            return false;
        }

        if (methodTarget.MethodKind != MethodKind.Ordinary
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
        if (!TryReadPrefix(model, invocation, descriptor, orderedQueryReceiver, callbackArgumentIndex, out InvocationCursorResult prefix))
        {
            return false;
        }
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
                || !GeneratorSupport.IsEntityRefType(methodTarget.Parameters[parameterIndex].Type))
            {
                return false;
            }

            hasEntity = true;
            parameterIndex++;
        }

        IParameterSymbol[] componentParameters = methodTarget.Parameters.Skip(parameterIndex).ToArray();
        if (!namedEntity && componentParameters.Any(static parameter =>
                GeneratorSupport.IsEntityType(parameter.Type) || GeneratorSupport.IsEntityRefType(parameter.Type)))
        {
            return false;
        }

        if (stamp && componentParameters.Any(static parameter =>
                !GeneratorSupport.IsStampType(parameter.Type)
                || parameter.RefKind is not RefKind.In && !GeneratorSupport.IsRefReadonly(parameter.RefKind)))
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        int expectedGenericCount = componentParameters.Length + (hasContext ? 1 : 0);
        if (genericName is not null && genericCount != expectedGenericCount)
        {
            return false;
        }

        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out _,
                genericName is null ? null : genericCount - (hasContext ? 1 : 0),
                componentIdCount == 0 ? null : componentIdCount,
                componentParameters.Length))
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
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

        if (stamp && genericName is null && !prefix.HasComponentIds)
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        string pattern = new(componentParameters.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray());
        if (stamp)
        {
            pattern = NormalizeStampPattern(pattern);
        }

        shape = new IterationModel(
            prefix.HasComponentIdSpan
                ? RegistrationBindingKind.Dynamic
                : componentIdCount == componentParameters.Length && componentIdCount != 0
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
            hasEntityTarget: orderedQueryReceiver || prefix.HasTarget,
            hasQuery: orderedQueryReceiver || prefix.HasQuery,
            isStamp: stamp,
            typeBinding: genericName is not null ? TypeBindingKind.Generic : TypeBindingKind.CallbackInferred,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation),
            orderedQueryReceiver: orderedQueryReceiver,
            orderedWhereSource: orderedWhereSource);
        return true;
    }

    private static bool TryReadFunctorShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        ApiDescriptor descriptor,
        bool orderedQueryReceiver,
        PredicateModel? orderedWhereSource,
        out IterationModel? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        int functorArgumentIndex = -1;
        INamedTypeSymbol? functorType = null;
        bool hasContext = false;
        bool hasEntity = false;
        ITypeSymbol? contextType = null;
        ContextModeKind functorPassMode = ContextModeKind.Ref;
        for (int index = invocation.ArgumentList.Arguments.Count - 1; index >= 0; index--)
        {
            ArgumentSyntax candidate = invocation.ArgumentList.Arguments[index];
            if (model.GetTypeInfo(candidate.Expression).Type is not INamedTypeSymbol candidateType
                || !CallbackReader.TryGetForEachMarker(candidateType, out hasContext, out hasEntity, out contextType))
            {
                continue;
            }

            functorArgumentIndex = index;
            functorType = candidateType;
            functorPassMode = CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(candidate));
            break;
        }

        if (functorArgumentIndex < 0 || functorType is null)
        {
            return false;
        }

        if (!GeneratorSupport.IsAccessibleSymbol(functorType))
        {
            return Reject(InaccessibleFunctor, invocation, functorType.Name, out diagnostic);
        }

        bool stamp = descriptor.Value == ValueDomain.Stamp;
        bool parallel = descriptor.Schedule == Schedule.Parallel;
        bool namedEntity = descriptor.HasEntity;
        if (namedEntity != hasEntity)
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        if (parallel && functorPassMode == ContextModeKind.RefReadonly)
        {
            functorPassMode = ContextModeKind.In;
        }

        if (!TryReadPrefix(model, invocation, descriptor, orderedQueryReceiver, functorArgumentIndex, out InvocationCursorResult prefix))
        {
            return false;
        }
        int componentIdCount = prefix.ComponentIdCount;
        int contextArgumentIndex = prefix.ContextIndex;
        ContextModeKind contextMode = prefix.ContextMode;
        if (hasContext)
        {
            if (invocation.ArgumentList.Arguments.Count <= contextArgumentIndex
                || invocation.ArgumentList.Arguments[contextArgumentIndex].Expression is null)
            {
                return Reject(Unsupported, invocation, invocation, out diagnostic);
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
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false, entityRef: true))
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
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }
        contextMode = NormalizeParallelContext(parallel, invokeContextMode);
        int prefixCount = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IParameterSymbol[] componentParameters = invoke.Parameters.Skip(prefixCount).ToArray();
        if (componentParameters.Length == 0 && !(hasEntity && !stamp))
        {
            // Component-only / stamp zero-arity falls through to FunctorAnchors.
            // Entity-aware sequential forms (MinimumArity 0) are still generated.
            return false;
        }

        if (stamp && componentParameters.Any(static parameter =>
                !GeneratorSupport.IsStampType(parameter.Type)
                || parameter.RefKind is not RefKind.In && !GeneratorSupport.IsRefReadonly(parameter.RefKind)))
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool genericSelectors = genericName is not null;
        if (stamp && !genericSelectors && !prefix.HasComponentIds)
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        string pattern = new(componentParameters.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray());
        string[] components = genericSelectors
            ? genericName!.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type is { } type ? GeneratorSupport.DisplayType(type) : argument.ToString())
                .ToArray()
            : componentParameters.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray();
        if (genericSelectors && components.Length != componentParameters.Length)
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }

        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out _,
                genericSelectors ? components.Length : null,
                componentIdCount == 0 ? null : componentIdCount,
                componentParameters.Length))
        {
            return Reject(Unsupported, invocation, invocation, out diagnostic);
        }
        shape = new IterationModel(
            prefix.RegistrationBinding,
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
            hasEntityTarget: orderedQueryReceiver || prefix.HasTarget,
            hasQuery: orderedQueryReceiver || prefix.HasQuery,
            isStamp: stamp,
            typeBinding: genericSelectors ? TypeBindingKind.Generic : TypeBindingKind.CallbackInferred,
            functorPassMode: functorPassMode,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation),
            orderedQueryReceiver: orderedQueryReceiver,
            orderedWhereSource: orderedWhereSource);
        return true;
    }

    internal static bool TryReadPrefix(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        bool orderedQueryReceiver,
        int callbackArgumentIndex,
        out InvocationCursorResult prefix)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        bool queryRequired = !orderedQueryReceiver
            && (arguments.Count == 0
                || !GeneratorSupport.IsEntityBatch(model.GetTypeInfo(arguments[0].Expression).Type));
        return new InvocationCursor(model, arguments, descriptor)
            .TryRead(callbackArgumentIndex, out prefix, queryRequired);
    }

    private static bool Reject(
        DiagnosticDescriptor descriptor,
        SyntaxNode syntax,
        object messageArgument,
        out Diagnostic? diagnostic)
    {
        diagnostic = Diagnostic.Create(descriptor, syntax.GetLocation(), messageArgument);
        return false;
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
    {
        if (parameter.Type?.ToString() is not ("Stamp" or "global::Delta.ECS.Stamp"))
        {
            return false;
        }

        RefKind refKind = CallbackReader.ParameterRefKind(parameter);
        return refKind is RefKind.In || GeneratorSupport.IsRefReadonly(refKind);
    }

    private static string NormalizeStampPattern(string pattern)
        => pattern.Replace('R', 'I');

    private static int FindCallbackArgumentIndex(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => arguments.IndexOf(arguments.First(static argument => argument.Expression is LambdaExpressionSyntax));

    private static string[] LambdaComponentTypes(
        SemanticModel model,
        LambdaExpressionSyntax? lambda,
        int prefixCount,
        bool hasEntity)
    {
        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        int start = prefixCount + (hasEntity ? 1 : 0);
        ITypeSymbol?[] types = parameters
            .Skip(start)
            .Select(parameter => parameter.Type is { } syntax ? model.GetTypeInfo(syntax).Type : null)
            .ToArray();
        return types.All(static type => type is not null)
            ? types.Select(static type => GeneratorSupport.DisplayType(type!)).ToArray()
            : Array.Empty<string>();
    }

    private static string? InferPattern(
        LambdaExpressionSyntax lambda,
        int componentCount,
        bool hasContext,
        bool hasEntity)
    {
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

}
