using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates only the ForEach shapes actually used by a consumer assembly.
/// </summary>
[Generator]
public sealed class DemandDrivenForEachGenerator : IIncrementalGenerator
{
    private const string InterceptorNamespace = "Delta.ECS.Generated";
    private const int FirstDemandArity = 1;
    private const int MaxArity = 256;

    private static readonly DiagnosticDescriptor Unsupported = new(
        "DECSGEN001",
        "Unsupported ForEach shape",
        "ForEach shape '{0}' is not supported by the demand-driven generator",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TooManyComponents = new(
        "DECSGEN002",
        "ForEach arity is too large",
        "ForEach arity {0} exceeds the supported demand-driven limit of {1}",
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
            context.CompilationProvider.Combine(
                context.AnalyzerConfigOptionsProvider.Select(
                    static (provider, _) => IsInterceptionEnabled(provider.GlobalOptions))),
            static (productionContext, input) => Execute(input.Left, productionContext, input.Right));
    }

    private static void Execute(
        Compilation compilation,
        SourceProductionContext context,
        bool interceptorsEnabled)
    {
        bool profiling = compilation.GetTypeByMetadataName("DeltaECS.Profiling.ProfilerRuntime") is not null
            && compilation.GetTypeByMetadataName("DeltaECS.Profiling.ProfiledMethodMetadataAttribute") is not null;
        var shapes = new Dictionary<string, Shape>(StringComparer.Ordinal);
        var interceptionSites = new Dictionary<string, List<InterceptionSite>>(StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (tree.FilePath.EndsWith("ForEach.g.cs", StringComparison.Ordinal)
                || tree.FilePath.Contains("DemandForEach_", StringComparison.Ordinal))
            {
                continue;
            }

            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryReadShape(model, invocation, out Shape? shape, out Diagnostic? diagnostic))
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

                Shape candidate = shape;
                string key = candidate.Key;
                if (!shapes.ContainsKey(key))
                {
                    shapes.Add(key, candidate);
                }

                if (interceptorsEnabled && !candidate.IsFunctor)
                {
                    if (TryCreateInterceptionSite(model, invocation, candidate, tree, out InterceptionSite? site, out string? reason)
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
        }

        foreach (IGrouping<string, Shape> patternGroup in shapes.Values
            .OrderBy(static value => value.Key, StringComparer.Ordinal)
            .GroupBy(static value => value.Pattern, StringComparer.Ordinal))
        {
            bool renderContracts = true;
            foreach (Shape shape in patternGroup)
            {
                context.AddSource(
                    $"DemandForEach_{StableName(shape.Key)}.g.cs",
                    Render(shape, renderContracts, profiling));
                if (interceptionSites.TryGetValue(shape.Key, out List<InterceptionSite>? interceptorSites))
                {
                    foreach (InterceptionSite interceptorSite in interceptorSites)
                    {
                        context.AddSource(
                            $"DemandForEachInterceptor_{interceptorSite.Id}.g.cs",
                            RenderInterceptorSource(shape, interceptorSite));
                    }
                }
                if (!shape.IsFunctor)
                {
                    renderContracts = false;
                }
            }
        }
    }

    private static bool IsInterceptionEnabled(AnalyzerConfigOptions options)
        => options.TryGetValue("build_property.InterceptorsNamespaces", out string? namespaces)
            && namespaces
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(static value => string.Equals(value.Trim(), InterceptorNamespace, StringComparison.Ordinal));

    private static bool TryCreateInterceptionSite(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        Shape shape,
        SyntaxTree tree,
        out InterceptionSite? site,
        out string? reason)
    {
        site = null;
        reason = null;
        if (shape.Receiver != ReceiverKind.World)
        {
            reason = "only World.ForEach call sites are currently supported";
            return false;
        }

        LambdaExpressionSyntax? lambda = Lambda(invocation.ArgumentList.Arguments);
        IMethodSymbol? methodGroup = null;
        if (lambda is null)
        {
            if (invocation.ArgumentList.Arguments.Count == 0
                || !TryGetMethodGroupTarget(model, invocation.ArgumentList.Arguments.Last().Expression, out IMethodSymbol? resolvedMethod)
                || resolvedMethod is not { } methodTarget)
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

            if (!IsStaticMethodGroupExpression(model, invocation.ArgumentList.Arguments.Last().Expression))
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

            if (!IsAccessibleToGeneratedCode(methodTarget))
            {
                reason = "the static method group target is not accessible to generated callback code";
                return false;
            }

            if (methodTarget.Parameters.Any(parameter => !IsAccessibleToGeneratedCode(parameter.Type)))
            {
                reason = "a static method group parameter type is not accessible to generated callback code";
                return false;
            }

            for (INamedTypeSymbol? typeInChain = methodTarget.ContainingType;
                 typeInChain is not null;
                 typeInChain = typeInChain.ContainingType)
            {
                if (typeInChain.Arity != 0)
                {
                    reason = "generic containing types are not supported by the generated callback";
                    return false;
                }
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

        if (lambda is not null)
        {
            for (INamedTypeSymbol? typeInChain = model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType;
                 typeInChain is not null;
                 typeInChain = typeInChain.ContainingType)
            {
                if (typeInChain.Arity != 0)
                {
                    reason = "generic containing types are not supported by the generated callback";
                    return false;
                }
            }

            if (model.GetEnclosingSymbol(invocation.SpanStart) is IMethodSymbol enclosingMethod
                && enclosingMethod.IsGenericMethod)
            {
                reason = "generic containing methods are not supported by the generated callback";
                return false;
            }
        }

        if (lambda is not null && !AreLambdaReferencesAccessible(model, lambda, out reason))
        {
            return false;
        }

        InterceptableLocation? location = model.GetInterceptableLocation(invocation, default);
        if (location is null)
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
                if (!IsAccessibleToGeneratedCode(containingType))
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

        string id = StableName(shape.Key + "|" + location.Data);
        site = new InterceptionSite(
            id,
            shape,
            lambda,
            methodGroup,
            location.GetInterceptsLocationAttributeSyntax().ToString(),
            usings);
        return true;
    }

    private static bool AreLambdaReferencesAccessible(
        SemanticModel model,
        LambdaExpressionSyntax lambda,
        out string? reason)
    {
        foreach (ParameterSyntax parameter in LambdaParameters(lambda))
        {
            if (model.GetDeclaredSymbol(parameter) is IParameterSymbol parameterSymbol
                && !IsAccessibleToGeneratedCode(parameterSymbol.Type))
            {
                reason = "a lambda parameter type is not accessible to generated callback code";
                return false;
            }
        }

        foreach (SyntaxNode node in lambda.Body.DescendantNodesAndSelf())
        {
            ISymbol? symbol = model.GetSymbolInfo(node).Symbol;
            if (symbol is null
                or IParameterSymbol
                or ILocalSymbol
                or IRangeVariableSymbol)
            {
                continue;
            }

            if (symbol is ITypeParameterSymbol)
            {
                reason = "the lambda body references a type parameter";
                return false;
            }

            if (!IsAccessibleToGeneratedCode(symbol))
            {
                reason = "the lambda body references a private or protected symbol";
                return false;
            }
        }

        reason = null;
        return true;
    }

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out Shape? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name.Identifier.ValueText is not ("ForEach" or "ForEachEntity"))
        {
            return false;
        }

        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool hasLambda = invocation.ArgumentList.Arguments.Any(static argument => argument.Expression is LambdaExpressionSyntax);
        if (!hasLambda)
        {
            if (TryReadMethodGroupShape(model, invocation, member, out shape, out diagnostic))
            {
                return true;
            }

            return TryReadFunctorShape(model, invocation, member, out shape, out diagnostic);
        }

        if (genericName is null && !hasLambda && model.GetSymbolInfo(invocation).Symbol is not null)
        {
            return false;
        }

        ITypeSymbol? receiverType = model.GetTypeInfo(member.Expression).Type;
        ReceiverKind receiver = ReceiverKindFrom(receiverType);
        if (receiver == ReceiverKind.None)
        {
            return false;
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        var arguments = invocation.ArgumentList.Arguments;
        bool namedEntity = member.Name.Identifier.ValueText == "ForEachEntity";
        int refArgumentCount = arguments.Count(static argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
        const bool isFunctor = false;
        bool hasContext = refArgumentCount >= 1;
        int prefixCount = hasContext ? 1 : 0;
        bool implicitComponents = genericName is null;
        LambdaExpressionSyntax? lambda = Lambda(arguments);
        ParameterSyntax[] lambdaParameters = LambdaParameters(lambda);
        int lambdaParameterCount = lambdaParameters.Length;
        if (implicitComponents
            && namedEntity
            && lambdaParameterCount == prefixCount + 1
            && lambdaParameters[prefixCount].Type is null)
        {
            return false;
        }

        bool hasEntity = namedEntity && lambdaParameterCount > prefixCount
            && IsEntityParameter(model, lambdaParameters[prefixCount]);
        int componentCount = implicitComponents
            ? lambdaParameterCount - prefixCount - (hasEntity ? 1 : 0)
            : genericCount - prefixCount;
        if (implicitComponents && namedEntity && componentCount == 0)
        {
            return false;
        }
        if (componentCount < FirstDemandArity || componentCount < 0)
        {
            return false;
        }

        if (componentCount > MaxArity)
        {
            diagnostic = Diagnostic.Create(TooManyComponents, invocation.GetLocation(), componentCount, MaxArity);
            return false;
        }

        string? accessPattern;
        accessPattern = InferPattern(arguments, componentCount, hasContext, hasEntity);

        if (accessPattern is null || accessPattern.Length != componentCount || accessPattern.Any(static c => c is not ('R' or 'W' or 'I' or 'V')))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        bool explicitIds = CountComponentIds(model, arguments) == componentCount;
        if (!explicitIds && CountComponentIds(model, arguments) != 0)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        bool sequence = receiver is ReceiverKind.EntitySequence or ReceiverKind.FilteredEntitySequence;
        var typeArguments = genericName?.TypeArgumentList.Arguments
            .Select(argument => model.GetTypeInfo(argument).Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? argument.ToString())
            .ToArray()
            ?? LambdaComponentTypes(model, lambda, prefixCount, hasEntity);
        int componentStart = genericName is null ? 0 : prefixCount;
        var components = typeArguments.Skip(componentStart).ToArray();
        if (components.Length != componentCount)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
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
                ? model.GetTypeInfo(contextSyntax).Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;
        }

        shape = new Shape(
            receiver,
            sequence,
            explicitIds,
            hasEntity || LambdaHasEntity(model, arguments, componentCount, hasContext, isFunctor),
            hasContext,
            isFunctor,
            implicitComponents,
            accessPattern,
            components,
            functorType: null,
            contextType: lambdaContextType);
        return true;
    }

    private static bool TryReadMethodGroupShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out Shape? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ArgumentSyntax callback = invocation.ArgumentList.Arguments.Last();
        if (callback.RefKindKeyword.RawKind != 0
            || !TryGetMethodGroupTarget(model, callback.Expression, out IMethodSymbol? method)
            || method is not { } methodTarget)
        {
            return false;
        }

        ReceiverKind receiver = ReceiverKindFrom(model.GetTypeInfo(member.Expression).Type);
        if (receiver == ReceiverKind.None
            || methodTarget.MethodKind != MethodKind.Ordinary
            || methodTarget.Arity != 0
            || !methodTarget.ReturnsVoid
            || methodTarget.ContainingType is null
            || methodTarget.Parameters.Any(static parameter => parameter.IsParams
                || !IsSupportedComponentRefKind(parameter.RefKind)))
        {
            return false;
        }

        GenericNameSyntax? genericName = member.Name as GenericNameSyntax;
        bool namedEntity = member.Name.Identifier.ValueText == "ForEachEntity";
        bool hasContext = invocation.ArgumentList.Arguments
            .Any(static argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
        int parameterIndex = 0;
        if (hasContext)
        {
            if (methodTarget.Parameters.Length == 0
                || methodTarget.Parameters[0].RefKind != RefKind.Ref)
            {
                return false;
            }

            parameterIndex++;
        }

        bool hasEntity = false;
        if (namedEntity)
        {
            if (methodTarget.Parameters.Length <= parameterIndex
                || methodTarget.Parameters[parameterIndex].RefKind != RefKind.None
                || !IsEntityType(methodTarget.Parameters[parameterIndex].Type))
            {
                return false;
            }

            hasEntity = true;
            parameterIndex++;
        }

        IParameterSymbol[] componentParameters = methodTarget.Parameters.Skip(parameterIndex).ToArray();
        if (componentParameters.Length < FirstDemandArity
            || componentParameters.Length > MaxArity)
        {
            return false;
        }

        int genericCount = genericName?.TypeArgumentList.Arguments.Count ?? 0;
        int expectedGenericCount = componentParameters.Length + (hasContext ? 1 : 0);
        if (genericName is not null && genericCount != expectedGenericCount)
        {
            return false;
        }

        string[] components;
        string? contextType = hasContext
            ? methodTarget.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
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
                    || !SymbolEqualityComparer.Default.Equals(requestedType, expectedType))
                {
                    return false;
                }
            }

            components = new string[componentParameters.Length];
            for (int index = 0; index < components.Length; index++)
            {
                ITypeSymbol requestedType = requestedTypes[requestedComponentStart + index] is { } type
                    ? type
                    : throw new InvalidOperationException("Validated component type was unexpectedly unavailable.");
                components[index] = requestedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            if (hasContext)
            {
                contextType = requestedTypes[0] is { } requestedContextType
                    ? requestedContextType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : throw new InvalidOperationException("Validated context type was unexpectedly unavailable.");
            }
        }
        else
        {
            components = componentParameters
                .Select(static parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToArray();
        }

        int componentIdCount = CountComponentIds(model, invocation.ArgumentList.Arguments);
        if (componentIdCount != 0 && componentIdCount != componentParameters.Length)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        shape = new Shape(
            receiver,
            receiver is ReceiverKind.EntitySequence or ReceiverKind.FilteredEntitySequence,
            componentIdCount == componentParameters.Length && componentIdCount != 0,
            hasEntity,
            hasContext,
            isFunctor: false,
            implicitComponents: genericName is null,
            new string(componentParameters.Select(static parameter => PatternLetter(parameter.RefKind)).ToArray()),
            components,
            functorType: null,
            contextType);
        return true;
    }

    private static bool TryReadFunctorShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out Shape? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        ReceiverKind receiver = ReceiverKindFrom(model.GetTypeInfo(member.Expression).Type);
        if (receiver == ReceiverKind.None || invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ArgumentSyntax functorArgument = invocation.ArgumentList.Arguments.Last();
        if (!functorArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            || model.GetTypeInfo(functorArgument.Expression).Type is not INamedTypeSymbol functorType)
        {
            return false;
        }

        if (!TryGetFunctorMarker(functorType, out bool hasContext, out bool hasEntity, out ITypeSymbol? contextType))
        {
            return false;
        }

        if (!IsAccessibleToGeneratedCode(functorType))
        {
            diagnostic = Diagnostic.Create(InaccessibleFunctor, invocation.GetLocation(), functorType.Name);
            return false;
        }

        bool namedEntity = member.Name.Identifier.ValueText == "ForEachEntity";
        if (namedEntity != hasEntity)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        IMethodSymbol[] invokes = functorType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && method.ReturnsVoid)
            .Where(method => HasValidFunctorPrefix(method, hasContext, hasEntity, contextType))
            .Where(method => method.Parameters.Skip((hasContext ? 1 : 0) + (hasEntity ? 1 : 0)).All(
                static parameter => IsSupportedComponentRefKind(parameter.RefKind)))
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
        int prefixCount = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IParameterSymbol[] componentParameters = invoke.Parameters.Skip(prefixCount).ToArray();
        if (componentParameters.Length > MaxArity)
        {
            diagnostic = Diagnostic.Create(TooManyComponents, invocation.GetLocation(), componentParameters.Length, MaxArity);
            return false;
        }

        int componentIdCount = CountComponentIds(model, invocation.ArgumentList.Arguments);
        if (componentIdCount != 0 && componentIdCount != componentParameters.Length)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        string pattern = new(componentParameters.Select(static parameter => PatternLetter(parameter.RefKind)).ToArray());
        string[] components = componentParameters
            .Select(static parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToArray();
        bool sequence = receiver is ReceiverKind.EntitySequence or ReceiverKind.FilteredEntitySequence;
        shape = new Shape(
            receiver,
            sequence,
            componentIdCount != 0,
            hasEntity,
            hasContext,
            isFunctor: true,
            implicitComponents: false,
            pattern,
            components,
            functorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            contextType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        return true;
    }

    private static bool TryGetFunctorMarker(
        INamedTypeSymbol functorType,
        out bool hasContext,
        out bool hasEntity,
        out ITypeSymbol? contextType)
    {
        hasContext = false;
        hasEntity = false;
        contextType = null;
        INamedTypeSymbol[] markers = functorType.AllInterfaces
            .Where(static type => type.ContainingNamespace.ToDisplayString() == "Delta.ECS")
            .Where(static type => type.Name is "IForEach" or "IForEachEntity" or "IForEachContext" or "IForEachContextEntity")
            .ToArray();
        if (markers.Length != 1)
        {
            return false;
        }

        INamedTypeSymbol marker = markers[0];
        hasContext = marker.Name is "IForEachContext" or "IForEachContextEntity";
        hasEntity = marker.Name is "IForEachEntity" or "IForEachContextEntity";
        contextType = hasContext ? marker.TypeArguments[0] : null;
        return true;
    }

    private static bool HasValidFunctorPrefix(
        IMethodSymbol method,
        bool hasContext,
        bool hasEntity,
        ITypeSymbol? contextType)
    {
        int index = 0;
        if (hasContext)
        {
            if (method.Parameters.Length == 0
                || method.Parameters[0].RefKind != RefKind.Ref
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType))
            {
                return false;
            }

            index++;
        }

        if (hasEntity)
        {
            if (method.Parameters.Length <= index
                || method.Parameters[index].RefKind != RefKind.None
                || method.Parameters[index].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::Delta.ECS.Entity")
            {
                return false;
            }

            index++;
        }

        return method.Parameters.Length >= index;
    }

    private static string FunctorSignature(IMethodSymbol method)
        => string.Join(string.Empty, method.Parameters.Select(static parameter => PatternLetter(parameter.RefKind)));

    private static bool IsSupportedComponentRefKind(RefKind refKind)
        => refKind is RefKind.None
            or RefKind.In
            or RefKind.Ref
            or RefKind.RefReadOnly
            or RefKind.RefReadOnlyParameter;

    private static char PatternLetter(RefKind refKind)
        => refKind switch
        {
            RefKind.Ref => 'W',
            RefKind.In => 'I',
            RefKind.RefReadOnly or RefKind.RefReadOnlyParameter => 'R',
            _ => 'V'
        };

    private static bool IsAccessibleToGeneratedCode(INamedTypeSymbol functorType)
    {
        for (INamedTypeSymbol? type = functorType; type is not null; type = type.ContainingType)
        {
            if (!IsAccessibilityAllowed(type.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAccessibleToGeneratedCode(ISymbol symbol)
    {
        if (!IsAccessibilityAllowed(symbol.DeclaredAccessibility))
        {
            return false;
        }

        if (symbol is ITypeSymbol type)
        {
            return IsAccessibleToGeneratedCode(type);
        }

        for (INamedTypeSymbol? containingType = symbol.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            if (!IsAccessibilityAllowed(containingType.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAccessibleToGeneratedCode(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsAccessibleToGeneratedCode(array.ElementType);
            case IPointerTypeSymbol pointer:
                return IsAccessibleToGeneratedCode(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (!IsAccessibilityAllowed(named.DeclaredAccessibility)
                    || (named.ContainingType is not null && !IsAccessibleToGeneratedCode(named.ContainingType)))
                {
                    return false;
                }

                return named.TypeArguments.All(IsAccessibleToGeneratedCode);
            default:
                return true;
        }
    }

    private static bool IsAccessibilityAllowed(Accessibility accessibility)
        => accessibility is not Accessibility.Private
            and not Accessibility.Protected
            and not Accessibility.ProtectedAndInternal;

    private static int CountComponentIds(SemanticModel model, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        int count = 0;
        foreach (ArgumentSyntax argument in arguments)
        {
            ITypeSymbol? type = model.GetTypeInfo(argument.Expression).Type;
            if (type?.Name == "ComponentId" && type.ContainingNamespace.ToDisplayString() == "Delta.ECS")
            {
                count++;
            }
        }

        return count;
    }

    private static LambdaExpressionSyntax? Lambda(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

    private static ParameterSyntax[] LambdaParameters(LambdaExpressionSyntax? lambda)
        => lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.ToArray(),
            _ => Array.Empty<ParameterSyntax>()
        };

    private static bool IsEntityParameter(SemanticModel model, ParameterSyntax parameter)
    {
        ITypeSymbol? type = parameter.Type is null ? null : model.GetTypeInfo(parameter.Type).Type;
        return type is not null && IsEntityType(type);
    }

    private static bool IsEntityType(ITypeSymbol type)
        => type.Name == "Entity" && type.ContainingNamespace.ToDisplayString() == "Delta.ECS";

    private static bool IsStaticMethodGroupExpression(SemanticModel model, ExpressionSyntax expression)
    {
        if (expression is IdentifierNameSyntax)
        {
            return true;
        }

        if (expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        ISymbol? receiver = model.GetSymbolInfo(member.Expression).Symbol;
        return receiver is INamedTypeSymbol or INamespaceSymbol or IAliasSymbol;
    }

    private static bool TryGetMethodGroupTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        out IMethodSymbol? method)
    {
        var members = model.GetMemberGroup(expression);
        if (members.Length == 1 && members[0] is IMethodSymbol resolvedMethod)
        {
            method = resolvedMethod;
            return true;
        }

        method = null;
        return false;
    }

    private static string MethodGroupTarget(IMethodSymbol method)
    {
        if (method.ContainingType is not { } containingType)
        {
            throw new ArgumentException("The method group target must belong to a type.", nameof(method));
        }

        return containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name;
    }

    private static string[] LambdaComponentTypes(
        SemanticModel model,
        LambdaExpressionSyntax? lambda,
        int prefixCount,
        bool hasEntity)
    {
        ParameterSyntax[] parameters = LambdaParameters(lambda);
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

            result[index] = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

        var parameters = LambdaParameters(lambda);
        int start = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);

        var result = new char[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            result[index] = parameters.Length > index + start
                ? PatternLetter(parameters[index + start])
                : 'W';
        }

        return new string(result);
    }

    private static char PatternLetter(ParameterSyntax parameter)
    {
        bool hasRef = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword));
        bool hasReadOnly = parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        if (hasRef && hasReadOnly)
        {
            return 'R';
        }

        if (hasRef)
        {
            return 'W';
        }

        return parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword))
            ? 'I'
            : 'V';
    }

    private static bool LambdaHasEntity(
        SemanticModel model,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int componentCount,
        bool hasContext,
        bool isFunctor)
    {
        if (isFunctor)
        {
            return false;
        }

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
        return type?.Name == "Entity" && type.ContainingNamespace.ToDisplayString() == "Delta.ECS";
    }

    private static ReceiverKind ReceiverKindFrom(ITypeSymbol? type)
    {
        string name = type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
        return name switch
        {
            "global::Delta.ECS.World" => ReceiverKind.World,
            "global::Delta.ECS.EntitySequence" => ReceiverKind.EntitySequence,
            "global::Delta.ECS.FilteredEntitySequence" => ReceiverKind.FilteredEntitySequence,
            _ => ReceiverKind.None
        };
    }

    private static string Render(
        Shape shape,
        bool renderContracts,
        bool profiling)
    {
        var source = new StringBuilder(32 * 1024);
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("namespace Delta.ECS;");
        source.AppendLine();
        if (renderContracts && !shape.IsFunctor && shape.Components.Length > 0)
        {
            RenderContracts(source, shape);
        }
        if (shape.Sequence)
        {
            RenderInvoker(source, shape, profiling);
        }

        RenderExtensions(source, shape, profiling);
        return source.ToString();
    }

    private static void RenderContracts(StringBuilder source, Shape shape)
    {
        string generic = shape.IsFunctor ? string.Empty : GenericTypes(shape.Components.Length);
        string parameters = RefParameters(shape.Pattern);
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        source.Append("public delegate void ").Append(TypeWithGenericArgs("ForEachAction" + suffix, generic)).Append('(')
            .Append(parameters).AppendLine(");");
        source.Append("public delegate void ").Append(TypeWithGenericArgs("ForEachEntityAction" + suffix, generic)).Append('(')
            .Append(JoinParameters("Entity entity", parameters)).AppendLine(");");
        source.Append("public delegate void ").Append(TypeWithGenericArgs("ForEachContextAction" + suffix, JoinGeneric("TContext", generic))).Append('(')
            .Append(JoinParameters("ref TContext context", parameters)).AppendLine(");");
        source.Append("public delegate void ").Append(TypeWithGenericArgs("ForEachContextEntityAction" + suffix, JoinGeneric("TContext", generic))).Append('(')
            .Append(JoinParameters("ref TContext context, Entity entity", parameters)).AppendLine(");");
    }

    private static void RenderInvoker(StringBuilder source, Shape shape, bool profiling)
    {
        string generic = shape.IsFunctor || shape.ImplicitComponents ? string.Empty : GenericTypes(shape.Components.Length);
        string name = InvokerName(shape);
        string actionType = ActionType(shape);
        string stateGeneric = StateGeneric(shape, generic);
        source.Append("internal struct ").Append(name).Append(stateGeneric).AppendLine(" : IGeneratedSequenceInvoker");
        source.AppendLine("{");
        if (shape.HasContext)
        {
            source.Append("    private ").Append(ContextType(shape)).AppendLine(" _context;");
        }

        if (shape.IsFunctor)
        {
            source.Append("    private ").Append(shape.FunctorType).AppendLine(" _functor;");
        }
        else
        {
            source.Append("    private readonly ").Append(actionType).AppendLine(" _action;");
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("    private readonly int _access").Append(index).AppendLine(";");
        }

        source.Append("    internal ").Append(ConstructorName(name)).Append('(');
        var constructorParameters = new List<string>();
        if (shape.HasContext)
        {
            constructorParameters.Add(ContextType(shape) + " context");
        }

        if (shape.IsFunctor)
        {
            constructorParameters.Add(shape.FunctorType + " functor");
        }
        else
        {
            constructorParameters.Add(actionType + " action");
        }

        string accessParameters = AccessParameters(shape.Pattern);
        if (accessParameters.Length > 0)
        {
            constructorParameters.Add(accessParameters);
        }

        source.Append(string.Join(", ", constructorParameters)).AppendLine(")");
        source.AppendLine("    {");
        if (shape.HasContext)
        {
            source.AppendLine("        _context = context;");
        }

        if (shape.IsFunctor)
        {
            source.AppendLine("        _functor = functor;");
        }
        else
        {
            source.AppendLine("        _action = action;");
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("        _access").Append(index).Append(" = access").Append(index).AppendLine(";");
        }
        source.AppendLine("    }");

        RenderSequenceInvoke(source, shape, name + stateGeneric, profiling);
        if (shape.HasContext)
        {
            source.Append("    internal ").Append(ContextType(shape)).AppendLine(" Context => _context;");
        }

        if (shape.IsFunctor)
        {
            source.Append("    internal ").Append(shape.FunctorType).AppendLine(" Functor => _functor;");
        }

        source.AppendLine("}");
    }

    private static void RenderSequenceInvoke(
        StringBuilder source,
        Shape shape,
        string invokerType,
        bool profiling)
    {
        string profileName = invokerType + ".Invoke(ref GeneratedSequenceCursor)";
        int methodId = StableProfileMethodId(profileName);
        if (profiling)
        {
            source.Append("    [global::DeltaECS.Profiling.ProfiledMethodMetadataAttribute(")
                .Append(methodId).Append(", \"").Append(profileName).AppendLine("\")]");
        }

        source.AppendLine("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.AppendLine("    public void Invoke(ref GeneratedSequenceCursor cursor)");
        source.AppendLine("    {");
        string indent = "        ";
        if (profiling)
        {
            source.Append("        global::DeltaECS.Profiling.ProfilerRuntime.Enter(").Append(methodId).AppendLine(");");
            source.AppendLine("        try");
            source.AppendLine("        {");
            indent = "            ";
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            string componentType = ComponentType(shape, index);
            char mode = shape.Pattern[index];
            source.Append(indent);
            if (mode == 'W')
            {
                source.Append("ref ");
            }
            else if (mode is 'R' or 'I')
            {
                source.Append("ref readonly ");
            }

            source.Append(componentType).Append(" component").Append(index)
                .Append(" = ");
            if (mode is 'R' or 'I' or 'W')
            {
                source.Append("ref ");
            }

            source.Append("cursor.GetGenerated")
                .Append(IsWrite(mode) ? "Write" : "Read")
                .Append("Reference<")
                .Append(componentType)
                .Append(">(_access")
                .Append(index)
                .AppendLine(");");
        }
        source.Append(indent);
        AppendClosedInvocation(source, shape, "_action", "_functor", "_context", "component", "cursor.Entity");
        source.AppendLine(";");
        if (profiling)
        {
            source.AppendLine("        }");
            source.AppendLine("        finally");
            source.AppendLine("        {");
            source.Append("            global::DeltaECS.Profiling.ProfilerRuntime.Leave(").Append(methodId).AppendLine(");");
            source.AppendLine("        }");
        }

        source.AppendLine("    }");
    }

    private static int StableProfileMethodId(string methodName)
    {
        unchecked
        {
            uint hash = 2_166_136_261;
            foreach (char character in methodName)
            {
                hash ^= character;
                hash *= 16_777_619;
            }

            return (int)(hash & 0x7FFF_FFFF);
        }
    }

    private static void AppendInvocation(StringBuilder source, Shape shape, string valuesPrefix, string cursor, bool sequence)
    {
        var invocationArguments = new List<string>();
        if (shape.HasContext)
        {
            invocationArguments.Add("ref _context");
        }

        if (shape.HasEntity)
        {
            invocationArguments.Add(sequence ? "cursor.Entity" : "slots.CurrentEntity");
        }
        if (shape.IsFunctor)
        {
            source.Append("_functor.Invoke(");
        }
        else
        {
            source.Append("_action(");
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            string componentType = ComponentType(shape, index);
            invocationArguments.Add(InvocationPrefix(shape.Pattern[index]) + valuesPrefix + index + ".Ref<" + componentType + ">(" + cursor + ")");
        }

        source.Append(string.Join(", ", invocationArguments)).Append(')');
    }

    private static void AppendClosedInvocation(
        StringBuilder source,
        Shape shape,
        string actionName,
        string functorName,
        string contextName,
        string componentPrefix,
        string entityExpression)
    {
        var invocationArguments = new List<string>();
        if (shape.HasContext)
        {
            invocationArguments.Add("ref " + contextName);
        }

        if (shape.HasEntity)
        {
            invocationArguments.Add(entityExpression);
        }

        source.Append(shape.IsFunctor ? functorName + ".Invoke(" : actionName + "(");
        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            invocationArguments.Add(
                InvocationPrefix(shape.Pattern[index])
                + componentPrefix
                + index
            );
        }

        source.Append(string.Join(", ", invocationArguments)).Append(')');
    }

    private static void RenderExtensions(
        StringBuilder source,
        Shape shape,
        bool profiling)
    {
        string className = "DemandForEachExtensions_" + StableName(shape.Key);
        string generic = shape.ImplicitComponents ? string.Empty : GenericTypes(shape.Components.Length);
        string ids = shape.ExplicitIds ? ComponentParameters(shape.Components.Length) : string.Empty;
        string idArguments = shape.ExplicitIds ? ComponentNames(shape.Components.Length) : PrimaryArguments(shape);
        string closedIdArguments = shape.ExplicitIds ? ClosedComponentNames(shape.Components.Length) : string.Empty;
        string accessArguments = AccessArguments(shape.Pattern);
        string setup = shape.Sequence
            ? AccessSetup(shape, idArguments, closed: false)
            : string.Empty;
        string name = InvokerName(shape);
        string stateGeneric = StateGeneric(shape, generic);
        string callback = ActionType(shape);
        string componentParameters = shape.ExplicitIds
            ? ", " + ClosedComponentParameters(shape.Components.Length)
            : string.Empty;
        string receiver = shape.Sequence
            ? (shape.Receiver == ReceiverKind.FilteredEntitySequence ? "FilteredEntitySequence" : "EntitySequence")
            : "World";
        string prefix = shape.Sequence
            ? $"this {receiver} sequence"
            : "this World world";
        string query = shape.Sequence ? string.Empty : ", in Query query";
        string contextParameter = shape.HasContext
            ? ", ref " + ContextType(shape) + " context"
            : string.Empty;
        string genericPrefix = shape.IsFunctor
            ? string.Empty
            : TypeParameterList(shape.ImplicitComponents ? string.Empty : shape.HasContext ? JoinGeneric("TContext", generic) : generic);
        source.Append(shape.IsFunctor ? "internal static class " : "public static class ").Append(className).AppendLine();
        source.AppendLine("{");
        string? closedMethodName = null;
        if (!shape.Sequence)
        {
            closedMethodName = "ExecuteClosed_" + StableName(shape.Key);
            RenderClosedDenseMethod(source, shape, closedMethodName, closedIdArguments, componentParameters);
        }

        RenderExtensionMethod(
            source,
            shape,
            prefix,
            query,
            contextParameter,
            ids,
            callback,
            name,
            stateGeneric,
            genericPrefix,
            setup,
            accessArguments,
            className,
            profiling,
            closedMethodName);

        source.AppendLine("}");
    }

    private static string RenderInterceptorSource(
        Shape shape,
        InterceptionSite site)
    {
        var source = new StringBuilder(16 * 1024);
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("#pragma warning disable CS0436 // Demand-generated callback contracts can also arrive through a referenced consumer assembly.");
        if (!site.Usings.Any(static value => value.Trim() is "using Delta.ECS;" or "using global::Delta.ECS;"))
        {
            source.AppendLine("using global::Delta.ECS;");
        }
        foreach (string usingDirective in site.Usings)
        {
            source.AppendLine(usingDirective);
        }

        source.AppendLine("namespace Delta.ECS.Generated");
        source.AppendLine("{");
        source.AppendLine();
        source.Append("file static class ForEachInterceptor_").Append(site.Id).AppendLine();
        source.AppendLine("{");
        RenderInterceptedCallback(source, site);
        RenderInterceptedClosedMethod(source, shape, site);
        RenderInterceptor(source, shape, site);

        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("namespace System.Runtime.CompilerServices");
        source.AppendLine("{");
        source.AppendLine();
        source.AppendLine("file sealed class InterceptsLocationAttribute : global::System.Attribute");
        source.AppendLine("{");
        source.AppendLine("    internal InterceptsLocationAttribute(int version, string data) { }");
        source.AppendLine("}");
        source.AppendLine("}");
        return source.ToString();
    }

    private static void RenderInterceptedCallback(StringBuilder source, InterceptionSite site)
    {
        Shape shape = site.Shape;
        string callbackName = "InvokeInterceptedCallback_" + site.Id;
        string[] parameters = InterceptedParameterNames(site);
        source.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("private static void ").Append(callbackName).Append('(');
        var declarations = new List<string>();
        int parameterIndex = 0;
        if (shape.HasContext)
        {
            declarations.Add("ref " + InterceptedContextType(shape) + " " + parameters[parameterIndex]);
            parameterIndex++;
        }

        if (shape.HasEntity)
        {
            declarations.Add("global::Delta.ECS.Entity " + parameters[parameterIndex]);
            parameterIndex++;
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            declarations.Add(
                ParameterPrefix(shape.Pattern[index])
                + shape.Components[index]
                + " "
                + parameters[parameterIndex + index]);
        }

        source.Append(string.Join(", ", declarations)).AppendLine(")");
        source.AppendLine("{");
        if (site.Lambda is { } lambda && lambda.Body is BlockSyntax block)
        {
            AppendIndented(source, block.ToString(), "    ");
        }
        else if (site.Lambda is { } expressionLambda)
        {
            source.Append("    ").Append(expressionLambda.Body).AppendLine(";");
        }
        else
        {
            source.Append("    ").Append(site.MethodGroupTarget).Append('(');
            var invocationArguments = new List<string>();
            parameterIndex = 0;
            if (shape.HasContext)
            {
                invocationArguments.Add("ref " + parameters[parameterIndex]);
                parameterIndex++;
            }

            if (shape.HasEntity)
            {
                invocationArguments.Add(parameters[parameterIndex]);
                parameterIndex++;
            }

            for (int index = 0; index < shape.Pattern.Length; index++)
            {
                invocationArguments.Add(InvocationPrefix(shape.Pattern[index]) + parameters[parameterIndex + index]);
            }

            source.Append(string.Join(", ", invocationArguments)).AppendLine(");");
        }

        source.AppendLine("}");
        source.AppendLine();
    }

    private static void RenderInterceptedClosedMethod(
        StringBuilder source,
        Shape shape,
        InterceptionSite site)
    {
        var closedShape = new Shape(
            ReceiverKind.World,
            sequence: false,
            shape.ExplicitIds,
            shape.HasEntity,
            shape.HasContext,
            isFunctor: true,
            implicitComponents: true,
            shape.Pattern,
            shape.Components,
            functorType: null,
            shape.ContextType);
        string[] parameters = InterceptedParameterNames(site);
        string methodName = "ExecuteInterceptedClosed_" + site.Id;
        string callbackName = "InvokeInterceptedCallback_" + site.Id;
        string componentParameters = closedShape.ExplicitIds
            ? ", " + ClosedComponentParameters(closedShape.Components.Length)
            : string.Empty;
        string contextParameter = closedShape.HasContext
            ? ", ref " + InterceptedContextType(closedShape) + " " + parameters[0]
            : string.Empty;

        source.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("private static void ").Append(methodName)
            .Append("(global::Delta.ECS.World world, in global::Delta.ECS.Query query")
            .Append(componentParameters)
            .Append(contextParameter)
            .AppendLine(")");
        source.AppendLine("{");
        source.Append("    using var execution = GeneratedForEachRuntime.OpenDense(world, in query, hasWrites: ")
            .Append(BoolHasWrites(closedShape.Pattern)).AppendLine(");");
        source.Append("    ").Append(AccessSetup(
            closedShape,
            closedShape.ExplicitIds ? ClosedComponentNames(closedShape.Components.Length) : string.Empty,
            closed: true,
            prepared: true));
        source.AppendLine("    while (execution.MoveNextTrusted(out var slots))");
        source.AppendLine("    {");
        for (int index = 0; index < closedShape.Pattern.Length; index++)
        {
            string componentType = closedShape.Components[index];
            source.Append("        ref ").Append(componentType).Append(" row").Append(index)
                .Append(" = ref slots.GetGenerated")
                .Append(IsWrite(closedShape.Pattern[index]) ? "Write" : "Read")
                .Append("Reference<").Append(componentType).Append(">(access").Append(index).AppendLine(");");
        }

        source.AppendLine("        int count = slots.Count;");
        source.AppendLine("        for (int index = 0; index < count; index++)");
        source.AppendLine("        {");
        int parameterIndex = 0;
        if (closedShape.HasContext)
        {
            parameterIndex++;
        }

        if (closedShape.HasEntity)
        {
            source.Append("            global::Delta.ECS.Entity ").Append(parameters[parameterIndex])
                .Append(" = slots.EntityAt(index);").AppendLine();
            parameterIndex++;
        }

        for (int index = 0; index < closedShape.Pattern.Length; index++)
        {
            string componentType = closedShape.Components[index];
            source.Append("            ")
                .Append(IsWrite(closedShape.Pattern[index]) ? "ref " : "ref readonly ")
                .Append(componentType).Append(' ').Append(parameters[parameterIndex + index])
                .Append(" = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row")
                .Append(index).AppendLine(", index);");
        }

        var invocationArguments = new List<string>();
        parameterIndex = 0;
        if (closedShape.HasContext)
        {
            invocationArguments.Add("ref " + parameters[parameterIndex]);
            parameterIndex++;
        }

        if (closedShape.HasEntity)
        {
            invocationArguments.Add(parameters[parameterIndex]);
            parameterIndex++;
        }

        for (int index = 0; index < closedShape.Pattern.Length; index++)
        {
            invocationArguments.Add(InvocationPrefix(closedShape.Pattern[index]) + parameters[parameterIndex + index]);
        }

        source.Append("            ").Append(callbackName).Append('(')
            .Append(string.Join(", ", invocationArguments)).AppendLine(");");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine();
    }

    private static string[] InterceptedParameterNames(InterceptionSite site)
    {
        if (site.Lambda is { } lambda)
        {
            return LambdaParameters(lambda)
                .Select(static parameter => parameter.Identifier.ValueText)
                .ToArray();
        }

        var names = new List<string>();
        if (site.Shape.HasContext)
        {
            names.Add("context");
        }

        if (site.Shape.HasEntity)
        {
            names.Add("entity");
        }

        names.AddRange(Enumerable.Range(0, site.Shape.Pattern.Length).Select(static index => "component" + index));
        return names.ToArray();
    }

    private static void RenderInterceptor(
        StringBuilder source,
        Shape shape,
        InterceptionSite site)
    {
        source.Append("    ").AppendLine(site.Attribute);
        source.Append("    internal static void Intercept_").Append(site.Id).Append("(");
        var parameters = new List<string> { "this global::Delta.ECS.World world", "in global::Delta.ECS.Query query" };
        if (shape.ExplicitIds)
        {
            parameters.Add(
                string.Join(", ", Enumerable.Range(0, shape.Components.Length)
                    .Select(static index => "global::Delta.ECS.ComponentId component" + index)));
        }

        if (shape.HasContext)
        {
            parameters.Add("ref " + InterceptedContextType(shape) + " context");
        }

        parameters.Add("global::Delta.ECS." + ConcreteActionType(shape) + " _");
        source.Append(string.Join(", ", parameters)).AppendLine(")");
        source.AppendLine("    {");
        source.Append("        ExecuteInterceptedClosed_")
            .Append(site.Id)
            .Append("(world, in query");
        if (shape.ExplicitIds)
        {
            source.Append(", ").Append(ComponentNames(shape.Components.Length));
        }

        if (shape.HasContext)
        {
            source.Append(", ref context");
        }

        source.AppendLine(");");
        source.AppendLine("    }");
        source.AppendLine();
    }

    private static string ConcreteActionType(Shape shape)
    {
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        string generic = string.Join(", ", shape.Components);
        if (shape.HasContext)
        {
            generic = JoinGeneric(InterceptedContextType(shape), generic);
            return TypeWithGenericArgs(
                (shape.HasEntity ? "ForEachContextEntityAction" : "ForEachContextAction") + suffix,
                generic);
        }

        return TypeWithGenericArgs(
            (shape.HasEntity ? "ForEachEntityAction" : "ForEachAction") + suffix,
            generic);
    }

    private static string InterceptedContextType(Shape shape)
        => shape.ContextType ?? "global::System.Object";

    private static void AppendIndented(StringBuilder source, string text, string indent)
    {
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            source.Append(indent).AppendLine(line);
        }
    }

    private static void RenderClosedDenseMethod(
        StringBuilder source,
        Shape shape,
        string methodName,
        string ids,
        string componentParameters)
    {
        string generic = shape.IsFunctor || shape.ImplicitComponents ? string.Empty : GenericTypes(shape.Components.Length);
        string genericPrefix = TypeParameterList(
            shape.IsFunctor || shape.ImplicitComponents
                ? string.Empty
                : shape.HasContext
                    ? JoinGeneric("TContext", generic)
                    : generic);
        string contextParameter = shape.HasContext
            ? ", ref " + ContextType(shape) + " context"
            : string.Empty;
        string callbackParameter = shape.IsFunctor
            ? ", ref " + shape.FunctorType + " functor"
            : ", " + ActionType(shape) + " action";

        source.AppendLine("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    private static void ").Append(methodName).Append(genericPrefix)
            .Append("(World world, in Query query")
            .Append(componentParameters)
            .Append(contextParameter)
            .Append(callbackParameter)
            .AppendLine(")");
        source.AppendLine("    {");
        source.Append("        using var execution = GeneratedForEachRuntime.OpenDense(world, in query, hasWrites: ")
            .Append(BoolHasWrites(shape.Pattern)).AppendLine(");");
        source.Append("        ").Append(AccessSetup(shape, ids, closed: true, prepared: true));
        source.AppendLine("        while (execution.MoveNextTrusted(out var slots))");
        source.AppendLine("        {");
        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            string componentType = ComponentType(shape, index);
            source.Append("                ")
                .Append("ref ")
                .Append(componentType)
                .Append(" row")
                .Append(index)
                .Append(" = ref slots.GetGenerated")
                .Append(IsWrite(shape.Pattern[index]) ? "Write" : "Read")
                .Append("Reference<")
                .Append(componentType)
                .Append(">(access")
                .Append(index)
                .AppendLine(");");
        }

        source.AppendLine("                int count = slots.Count;");
        source.AppendLine("                for (int index = 0; index < count; index++)");
        source.AppendLine("                {");
        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            string componentType = ComponentType(shape, index);
            source.Append("                    ")
                .Append(IsWrite(shape.Pattern[index]) ? "ref " : "ref readonly ")
                .Append(componentType)
                .Append(" component")
                .Append(index)
                .Append(" = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row")
                .Append(index)
                .AppendLine(", index);");
        }
        source.Append("                    ");
        AppendClosedInvocation(source, shape, "action", "functor", "context", "component", "slots.EntityAt(index)");
        source.AppendLine(";");
        source.AppendLine("                }");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void RenderExtensionMethod(
        StringBuilder source,
        Shape shape,
        string prefix,
        string query,
        string contextParameter,
        string ids,
        string callback,
        string invokerName,
        string stateGeneric,
        string genericPrefix,
        string setup,
        string accessArguments,
        string className,
        bool profiling,
        string? closedMethodName)
    {
        string componentPart = string.IsNullOrEmpty(ids) ? string.Empty : $", {ids}";
        string methodName = shape.HasEntity ? "ForEachEntity" : "ForEach";
        string profileName = className + "." + methodName;
        if (shape.IsFunctor)
        {
            string functor = ", ref " + shape.FunctorType + " functor";
            string signature = $"internal static void {methodName}{genericPrefix}({prefix}{query}{contextParameter}{componentPart}{functor})";
            string body = BuildBody(
                shape,
                accessArguments,
                invokerName,
                stateGeneric,
                setup,
                "functor",
                hasAction: false,
                profiling,
                StableProfileMethodId(profileName),
                closedMethodName);
            AppendMethod(source, signature, body, null, profiling ? profileName : null);
            return;
        }

        string callbackParameter = $", {callback} action";
        string visibility = shape.ImplicitComponents ? "internal" : "public";
        string signatureDelegate = $"{visibility} static void {methodName}{genericPrefix}({prefix}{query}{contextParameter}{componentPart}{callbackParameter})";
        string delegateBody = BuildBody(
            shape,
            accessArguments,
            invokerName,
            stateGeneric,
            setup,
            "action",
            hasAction: true,
            profiling,
            StableProfileMethodId(profileName),
            closedMethodName);
        AppendMethod(source, signatureDelegate, delegateBody, null, profiling ? profileName : null);
    }

    private static string BuildBody(
        Shape shape,
        string accessArguments,
        string invokerName,
        string stateGeneric,
        string setup,
        string callbackName,
        bool hasAction,
        bool profiling,
        int methodId,
        string? closedMethodName)
    {
        string[] access = accessArguments.Split(new[] { ", " }, StringSplitOptions.None);
        var accesses = new StringBuilder();
        for (int index = 0; index < access.Length; index++)
        {
            if (index > 0)
            {
                accesses.Append(", ");
            }

            accesses.Append(access[index]);
        }

        string invoke;
        if (shape.Sequence)
        {
            invoke = "sequence.GeneratedWorld.ExecuteGeneratedSequence(sequence.GeneratedEntities, in query, ref invoker, hasWrites: " + BoolHasWrites(shape.Pattern) + ");";
        }
        else
        {
            var closedArguments = new List<string> { "world", "in query" };
            if (shape.ExplicitIds)
            {
                closedArguments.Add(ComponentNames(shape.Components.Length));
            }

            if (shape.HasContext)
            {
                closedArguments.Add("ref context");
            }

            closedArguments.Add(shape.IsFunctor ? "ref functor" : "action");
            if (accesses.Length > 0 && closedMethodName is null)
            {
                closedArguments.Add(accesses.ToString());
            }

            invoke = closedMethodName + "(" + string.Join(", ", closedArguments) + ");";
        }
        var body = new StringBuilder();
        body.AppendLine("{");
        string indent = "    ";
        if (profiling)
        {
            body.Append("    global::DeltaECS.Profiling.ProfilerRuntime.Enter(").Append(methodId).AppendLine(");");
            body.AppendLine("    try");
            body.AppendLine("    {");
            indent = "        ";
        }

        if (hasAction)
        {
            body.Append(indent).AppendLine("ArgumentNullException.ThrowIfNull(action);");
        }

        body.Append(indent).Append(setup);
        var constructorArguments = new List<string>();
        if (shape.HasContext)
        {
            constructorArguments.Add("context");
        }

        constructorArguments.Add(callbackName);
        if (accesses.Length > 0)
        {
            constructorArguments.Add(accesses.ToString());
        }

        if (shape.Sequence)
        {
            body.Append(indent).Append("var invoker = new ").Append(invokerName).Append(stateGeneric).Append('(')
                .Append(string.Join(", ", constructorArguments)).AppendLine(");");
            body.Append(indent).Append(invoke).AppendLine();
            if (shape.HasContext)
            {
                body.Append(indent).AppendLine("context = invoker.Context;");
            }

            if (shape.IsFunctor)
            {
                body.Append(indent).AppendLine("functor = invoker.Functor;");
            }
        }
        else
        {
            body.Append(indent).Append(invoke).AppendLine();
        }

        if (profiling)
        {
            body.AppendLine("    }");
            body.AppendLine("    finally");
            body.AppendLine("    {");
            body.Append("        global::DeltaECS.Profiling.ProfilerRuntime.Leave(").Append(methodId).AppendLine(");");
            body.AppendLine("    }");
        }

        body.AppendLine("}");
        return body.ToString();
    }

    private static void AppendMethod(
        StringBuilder source,
        string signature,
        string body,
        string? constraint,
        string? profileName)
    {
        if (profileName is not null)
        {
            source.Append("    [global::DeltaECS.Profiling.ProfiledMethodMetadataAttribute(")
                .Append(StableProfileMethodId(profileName)).Append(", \"").Append(profileName).AppendLine("\")]");
        }

        source.AppendLine("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    ").AppendLine(signature);
        if (constraint is not null)
        {
            source.Append("        ").AppendLine(constraint);
        }

        foreach (string line in body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            source.Append("    ").AppendLine(line);
        }
    }

    private static string AccessSetup(Shape shape, string ids, bool closed, bool prepared = false)
    {
        string query = string.Empty;
        if (shape.Receiver == ReceiverKind.FilteredEntitySequence)
        {
            query = "Query query = sequence.GeneratedQuery;";
        }
        else if (shape.Receiver == ReceiverKind.EntitySequence)
        {
            query = "Query query = sequence.GeneratedWorld.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { " + ids + " }));";
        }
        string owner = shape.Sequence
            ? "sequence.GeneratedWorld"
            : "world";
        var result = new StringBuilder();
        if (query.Length > 0)
        {
            result.Append(query).Append(' ');
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            result.Append("var access").Append(index).Append(" = GeneratedForEachRuntime.");
            if (prepared)
            {
                result.Append("GetPrepared").Append(IsWrite(shape.Pattern[index]) ? "Write" : "Read").Append("Access");
            }
            else if (closed)
            {
                result.Append("Create").Append(IsWrite(shape.Pattern[index]) ? "Write" : "Read").Append("Access");
            }
            else
            {
                result.Append("Access").Append(IsWrite(shape.Pattern[index]) ? "Write" : "Read");
            }

            result.Append('(');
            if (!prepared)
            {
                result.Append(owner).Append(", ");
            }

            result.Append("in query, ");
            if (shape.ExplicitIds)
            {
                result.Append(ComponentArgument(ids, index)).Append(", ");
            }

            result.Append("typeof(")
                .Append(ComponentType(shape, index)).AppendLine(") ); ");
        }

        return result.ToString();
    }

    private static string ComponentArgument(string ids, int index)
    {
        string[] values = ids.Split(new[] { ", " }, StringSplitOptions.None);
        return values[index];
    }

    private static string PrimaryArguments(Shape shape)
    {
        var result = new string[shape.Components.Length];
        string owner = shape.Sequence
            ? "sequence.GeneratedWorld"
            : "world";
        for (int index = 0; index < shape.Components.Length; index++)
        {
            string componentType = ComponentType(shape, index);
            result[index] = owner + ".Layouts.GetPrimary(typeof(" + componentType + "))";
        }

        return string.Join(", ", result);
    }

    private static string AccessArguments(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = "access" + index;
        }

        return string.Join(", ", result);
    }

    private static string ComponentParameters(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "ComponentId component" + index;
        }

        return string.Join(", ", result);
    }

    private static string ComponentNames(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "component" + index;
        }

        return string.Join(", ", result);
    }

    private static string ClosedComponentNames(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "componentId" + index;
        }

        return string.Join(", ", result);
    }

    private static string ClosedComponentParameters(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "ComponentId componentId" + index;
        }

        return string.Join(", ", result);
    }

    private static string RefParameters(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = ParameterPrefix(pattern[index]) + "T" + (index + 1) + " component" + index;
        }

        return string.Join(", ", result);
    }

    private static string AccessParameters(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = "int access" + index;
        }

        return string.Join(", ", result);
    }

    private static string ClosedAccessParameters(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = (IsWrite(pattern[index]) ? "WriteAccess" : "ReadAccess") + " access" + index;
        }

        return string.Join(", ", result);
    }

    private static string ActionType(Shape shape)
    {
        string generic = shape.ImplicitComponents
            ? string.Join(", ", shape.Components)
            : GenericTypes(shape.Components.Length);
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        if (shape.HasContext)
        {
            return TypeWithGenericArgs(
                shape.HasEntity ? "ForEachContextEntityAction" + suffix : "ForEachContextAction" + suffix,
                JoinGeneric(shape.ImplicitComponents ? ContextType(shape) : "TContext", generic));
        }

        return TypeWithGenericArgs(shape.HasEntity ? "ForEachEntityAction" + suffix : "ForEachAction" + suffix, generic);
    }

    private static string StateGeneric(Shape shape, string generic)
        => TypeParameterList(
            shape.IsFunctor || shape.ImplicitComponents
                ? string.Empty
                : shape.HasContext
                    ? JoinGeneric("TContext", generic)
                    : generic);

    private static string InvokerName(Shape shape) => "DemandForEachInvoker_" + StableName(shape.Key);

    private static string GenericTypes(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "T" + (index + 1);
        }

        return string.Join(", ", result);
    }

    private static string ComponentType(Shape shape, int index)
        => shape.IsFunctor || shape.ImplicitComponents
            ? shape.Components[index]
            : "T" + (index + 1);

    private static string ContextType(Shape shape)
        => shape.IsFunctor || shape.ImplicitComponents
            ? shape.ContextType ?? "TContext"
            : "TContext";

    private static string JoinGeneric(params string[] values)
        => string.Join(", ", values.Where(static value => !string.IsNullOrEmpty(value)));

    private static string TypeWithGenericArgs(string name, string generic)
        => string.IsNullOrEmpty(generic) ? name : name + "<" + generic + ">";

    private static string TypeParameterList(string generic)
        => string.IsNullOrEmpty(generic) ? string.Empty : "<" + generic + ">";

    private static string JoinParameters(string prefix, string parameters)
        => string.IsNullOrEmpty(parameters) ? prefix : prefix + ", " + parameters;

    private static string ConstructorName(string name)
    {
        int index = name.IndexOf('<');
        return index < 0 ? name : name.Substring(0, index);
    }

    private static bool IsAllWrite(string pattern) => pattern.All(static value => value == 'W');

    private static bool IsWrite(char mode) => mode == 'W';

    private static string ParameterPrefix(char mode)
        => mode switch
        {
            'R' => "ref readonly ",
            'W' => "ref ",
            'I' => "in ",
            _ => string.Empty
        };

    private static string InvocationPrefix(char mode)
        => mode is 'R' or 'I' ? "in " : mode == 'W' ? "ref " : string.Empty;

    private static string BoolHasWrites(string pattern) => pattern.Contains('W') ? "true" : "false";

    private static string StableName(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char value in key)
            {
                hash = (hash ^ value) * 16777619;
            }

            return hash.ToString("X8");
        }
    }

    private enum ReceiverKind
    {
        None,
        World,
        EntitySequence,
        FilteredEntitySequence
    }

    private sealed class InterceptionSite
    {
        internal InterceptionSite(
            string id,
            Shape shape,
            LambdaExpressionSyntax? lambda,
            IMethodSymbol? methodGroup,
            string attribute,
            string[] usings)
        {
            Id = id;
            Shape = shape;
            Lambda = lambda;
            MethodGroupTarget = methodGroup is null ? null : MethodGroupTarget(methodGroup);
            Attribute = attribute;
            Usings = usings;
        }

        internal string Id { get; }
        internal Shape Shape { get; }
        internal LambdaExpressionSyntax? Lambda { get; }
        internal string? MethodGroupTarget { get; }
        internal string Attribute { get; }
        internal string[] Usings { get; }
    }

    private sealed class Shape
    {
        public Shape(
            ReceiverKind receiver,
            bool sequence,
            bool explicitIds,
            bool hasEntity,
            bool hasContext,
            bool isFunctor,
            bool implicitComponents,
            string pattern,
            string[] components,
            string? functorType,
            string? contextType)
        {
            Receiver = receiver;
            Sequence = sequence;
            ExplicitIds = explicitIds;
            HasEntity = hasEntity;
            HasContext = hasContext;
            IsFunctor = isFunctor;
            ImplicitComponents = implicitComponents;
            Pattern = pattern;
            Components = components;
            FunctorType = functorType;
            ContextType = contextType;
        }

        public ReceiverKind Receiver { get; }
        public bool Sequence { get; }
        public bool ExplicitIds { get; }
        public bool HasEntity { get; }
        public bool HasContext { get; }
        public bool IsFunctor { get; }
        public bool ImplicitComponents { get; }
        public string Pattern { get; }
        public string[] Components { get; }
        public string? FunctorType { get; }
        public string? ContextType { get; }
        public string Key => $"{Receiver}|{ExplicitIds}|{HasEntity}|{HasContext}|{IsFunctor}|{ImplicitComponents}|{Pattern}|{FunctorType}|{ContextType}|{string.Join(";", Components)}";
    }
}
