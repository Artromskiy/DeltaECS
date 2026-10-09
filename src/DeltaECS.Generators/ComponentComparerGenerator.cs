using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Generates typed ordered-query adapters for component comparer functors and callbacks.</summary>
[Generator]
public sealed class ComponentComparerGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidComparer = GeneratorDiagnostics.Error(
        "DECSGEN009",
        "Invalid component comparer",
        "Component comparer '{0}' must expose one int Invoke method with paired read-only component parameters, optionally preceded by context and Entity parameters",
        "Component comparer");
    private static readonly DiagnosticDescriptor InvalidCall = GeneratorDiagnostics.Error(
        "DECSGEN010",
        "Invalid component comparer call",
        "'{0}' requires a component comparer callback or functor, optionally preceded by component registrations and context",
        "Component comparer");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => OrderedQueryInvocationGrammar.IsCandidate(node),
                static (syntax, _) => new InvocationCandidate((InvocationExpressionSyntax)syntax.Node))
            .Collect();

        context.RegisterSourceOutput(context.CompilationProvider.Combine(invocations), static (productionContext, input) =>
            Execute(input.Left, input.Right, productionContext));
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationCandidate> invocations,
        SourceProductionContext context)
    {
        var models = new ShapeRegistry<ComponentComparerModel>(static model => model.Key);
        var whereSources = new PredicateSourceRegistry();
        foreach (InvocationCandidate candidate in invocations)
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            SemanticModel semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryReadInvocation(semanticModel, invocation, out ComponentComparerModel? model, out bool isComparerCall, out PredicateModel? whereSource)
                || !isComparerCall)
            {
                continue;
            }

            if (model is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidCall, invocation.GetLocation(),
                    ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText));
                continue;
            }

            if (model.Signature is not { } signature)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidComparer,
                    invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1].GetLocation(),
                    model.FunctorDisplayName));
                continue;
            }

            ComponentComparerModel registeredModel = models.GetOrAdd(model);
            if (whereSource is not null)
            {
                whereSources.Add(registeredModel.Key, whereSource);
            }
        }

        foreach (ComponentComparerModel model in models.Ordered())
        {
            context.AddSource(
                "GeneratedComponentComparer_" + GeneratorSupport.StableName(model.Key) + ".g.cs",
                ComponentComparerTemplates.Render(model, whereSources.Get(model.Key)));
        }
    }

    private static bool TryReadInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ComponentComparerModel? model,
        out bool isComparerCall,
        out PredicateModel? whereSource)
    {
        model = null;
        isComparerCall = false;
        whereSource = null;
        if (!OrderedQueryInvocationGrammar.TryReadMethod(
                semanticModel,
                invocation,
                out _,
                out ApiDescriptor descriptor,
                out whereSource))
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        ArgumentSyntax comparerArgument = arguments[arguments.Count - 1];
        ITypeSymbol? comparerSymbol = semanticModel.GetTypeInfo(comparerArgument.Expression).Type;
        if (comparerSymbol is not INamedTypeSymbol comparerType
            || !HasComparerMarker(comparerType))
        {
            return false;
        }

        isComparerCall = true;
        if (!comparerArgument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword)
            || !GeneratorSupport.IsAccessibleSymbol(comparerType)
            || comparerType.TypeArguments.Any(GeneratorSupport.ContainsTypeParameter))
        {
            return true;
        }

        if (!TryReadInvoke(comparerType, out ComponentComparerSignature? signature) || signature is null)
        {
            model = CreateModel(comparerType, null);
            return true;
        }

        if (!OrderedQueryInvocationGrammar.TryReadArguments(
                semanticModel,
                invocation,
                descriptor,
                signature.ComponentTypes.Length,
                signature.HasContext,
                out InvocationCursorResult selection,
                out _))
        {
            return true;
        }

        ArgumentSyntax? contextArgument = signature.HasContext
            ? arguments[selection.ContextIndex]
            : null;
        if (contextArgument is not null && !ContextArgumentMatches(semanticModel, contextArgument, signature))
        {
            return true;
        }

        model = CreateModel(comparerType, signature);
        return true;
    }

    private static ComponentComparerModel CreateModel(INamedTypeSymbol comparerType, ComponentComparerSignature? signature)
        => new(
            comparerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            comparerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            comparerType.IsReferenceType,
            signature);

    private static bool TryReadInvoke(INamedTypeSymbol comparerType, out ComponentComparerSignature? signature)
    {
        signature = null;
        IMethodSymbol[] invokeMethods = comparerType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic)
            .ToArray();
        if (invokeMethods.Length != 1)
        {
            return false;
        }

        IMethodSymbol invoke = invokeMethods[0];
        bool hasEntity = HasEntityComparerMarker(comparerType);
        int parameterCount = invoke.Parameters.Length;
        if (invoke.IsGenericMethod
            || invoke.ReturnType.SpecialType != SpecialType.System_Int32
            || invoke.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            || parameterCount < (hasEntity ? 4 : 2))
        {
            return false;
        }

        bool hasContext = (parameterCount - (hasEntity ? 2 : 0) & 1) != 0;
        int contextCount = hasContext ? 1 : 0;
        int componentCount = (parameterCount - contextCount - (hasEntity ? 2 : 0)) / 2;
        if (componentCount == 0)
        {
            return false;
        }

        int componentStart = contextCount + (hasEntity ? 1 : 0);
        int rightStart = componentStart + componentCount + (hasEntity ? 1 : 0);
        ContextModeKind contextMode = ContextModeKind.Ref;
        string? contextType = null;
        if (hasContext)
        {
            IParameterSymbol context = invoke.Parameters[0];
            contextMode = CallbackReader.ContextMode(context.RefKind);
            if (!GeneratorSupport.IsAccessibleSymbol(context.Type)
                || context.RefKind is not (RefKind.Ref or RefKind.In or RefKind.None)
                    && !GeneratorSupport.IsRefReadonly(context.RefKind))
            {
                return false;
            }

            contextType = GeneratorSupport.DisplayType(context.Type);
        }

        if (hasEntity
            && (!GeneratorSupport.IsEntityType(invoke.Parameters[contextCount].Type)
                || invoke.Parameters[contextCount].RefKind != RefKind.None
                || !GeneratorSupport.IsEntityType(invoke.Parameters[contextCount + componentCount + 1].Type)
                || invoke.Parameters[contextCount + componentCount + 1].RefKind != RefKind.None))
        {
            return false;
        }

        for (int index = 0; index < componentCount; index++)
        {
            IParameterSymbol left = invoke.Parameters[componentStart + index];
            IParameterSymbol right = invoke.Parameters[rightStart + index];
            if (left.RefKind != RefKind.In
                || right.RefKind != RefKind.In
                || !GeneratorSupport.IsAccessibleSymbol(left.Type)
                || !GeneratorSupport.IsAccessibleSymbol(right.Type)
                || !SymbolEqualityComparer.Default.Equals(left.Type, right.Type))
            {
                return false;
            }
        }

        signature = new ComponentComparerSignature(
            invoke.Parameters.Skip(componentStart).Take(componentCount)
                .Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray(),
            hasContext,
            contextType,
            contextMode,
            hasEntity);
        return true;
    }

    private static bool HasComparerMarker(INamedTypeSymbol type) => type.AllInterfaces.Any(static candidate =>
            candidate.Name is "IComponentComparer" or "IComponentComparerEntity"
            && candidate.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace);

    private static bool HasEntityComparerMarker(INamedTypeSymbol type) => type.AllInterfaces.Any(static candidate =>
            candidate.Name == "IComponentComparerEntity"
            && candidate.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace);

    private static bool ContextArgumentMatches(SemanticModel model, ArgumentSyntax argument, ComponentComparerSignature signature)
    {
        ITypeSymbol? contextType = model.GetTypeInfo(argument.Expression).Type;
        if (contextType is null
            || GeneratorSupport.DisplayType(contextType)
            != signature.ContextType)
        {
            return false;
        }

        RefKind argumentMode = CallbackReader.ArgumentRefKind(argument);
        return CallbackReader.AreCompatibleContextModes(signature.ContextMode, CallbackReader.ContextMode(argumentMode));
    }

}
