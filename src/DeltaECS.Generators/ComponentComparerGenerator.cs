using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Generates typed ordered-query adapters for component comparer functors and callbacks.</summary>
[Generator]
public sealed class ComponentComparerGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidComparer = new(
        "DECSGEN009",
        "Invalid component comparer",
        "Component comparer '{0}' must expose one int Invoke method with paired read-only component parameters, optionally preceded by context and Entity parameters",
        "Component comparer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidCall = new(
        "DECSGEN010",
        "Invalid component comparer call",
        "'{0}' requires a component comparer callback or functor, optionally preceded by component registrations and context",
        "Component comparer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax invocation
                    && invocation.Expression is MemberAccessExpressionSyntax member
                    && member.Name.Identifier.ValueText is "OrderBy" or "ThenBy",
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
        var models = new Dictionary<string, ComponentComparerModel>(StringComparer.Ordinal);
        foreach (InvocationCandidate candidate in invocations)
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            SemanticModel semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryReadInvocation(semanticModel, invocation, out ComponentComparerModel? model, out bool isComparerCall))
            {
                if (isComparerCall)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidCall, invocation.GetLocation(),
                        ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText));
                }

                continue;
            }

            if (model is null)
            {
                if (isComparerCall)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidCall, invocation.GetLocation(),
                        ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText));
                }

                continue;
            }

            if (model.Signature is not { } signature)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidComparer, invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1].GetLocation(), model.FunctorType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                continue;
            }

            if (!models.ContainsKey(model.Key))
            {
                models.Add(model.Key, model);
            }
        }

        foreach (ComponentComparerModel model in models.Values.OrderBy(static model => model.Key, StringComparer.Ordinal))
        {
            context.AddSource(
                "GeneratedComponentComparer_" + GeneratorSupport.StableName(model.Key) + ".g.cs",
                ComponentComparerTemplates.Render(model));
        }
    }

    private static bool TryReadInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ComponentComparerModel? model,
        out bool isComparerCall)
    {
        model = null;
        isComparerCall = false;
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        string methodName = member.Name.Identifier.ValueText;
        if (methodName is not ("OrderBy" or "ThenBy"))
        {
            return false;
        }

        ITypeSymbol? receiverType = semanticModel.GetTypeInfo(member.Expression).Type;
        string expectedReceiver = methodName == "OrderBy" ? "Query" : "OrderedQuery";
        bool receiverMatches = receiverType is INamedTypeSymbol namedReceiver
            && namedReceiver.Name == expectedReceiver
            && namedReceiver.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;
        if (!receiverMatches
            && (methodName != "ThenBy" || !IsOrderedQueryExpression(member.Expression, semanticModel)))
        {
            return false;
        }

        ImmutableArray<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments.ToImmutableArray();
        if (arguments.IsEmpty)
        {
            return false;
        }

        ArgumentSyntax comparerArgument = arguments[arguments.Length - 1];
        ITypeSymbol? comparerSymbol = semanticModel.GetTypeInfo(comparerArgument.Expression).Type;
        if (comparerSymbol is not INamedTypeSymbol comparerType
            || !HasComparerMarker(comparerType))
        {
            return false;
        }

        isComparerCall = true;
        if (!comparerArgument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword)
            || !GeneratorSupport.IsAccessibleSymbol(comparerType)
            || comparerType.TypeArguments.Any(ContainsTypeParameter))
        {
            return true;
        }

        if (!TryReadInvoke(comparerType, out ComparerSignature? signature) || signature is null)
        {
            model = new ComponentComparerModel(comparerType, null, false);
            return true;
        }

        ImmutableArray<ArgumentSyntax> prefix = arguments.RemoveAt(arguments.Length - 1);
        ArgumentSyntax? contextArgument = null;
        if (signature.HasContext)
        {
            if (prefix.IsEmpty)
            {
                return true;
            }

            contextArgument = prefix[prefix.Length - 1];
            prefix = prefix.RemoveAt(prefix.Length - 1);
            if (!ContextArgumentMatches(semanticModel, contextArgument, signature))
            {
                return true;
            }
        }

        if (prefix.IsEmpty)
        {
            // Primary registrations are resolved by the generated extension.
        }
        else if (prefix.Length == 1 && IsComponentIdSpan(semanticModel.GetTypeInfo(prefix[0].Expression).Type))
        {
        }
        else if (prefix.Length == signature.ComponentTypes.Length
            && prefix.All(argument => IsComponentId(semanticModel.GetTypeInfo(argument.Expression).Type)))
        {
        }
        else
        {
            return true;
        }

        model = new ComponentComparerModel(comparerType, signature, contextArgument is not null);
        return true;
    }

    private static bool TryReadInvoke(INamedTypeSymbol comparerType, out ComparerSignature? signature)
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
            && (!IsEntity(invoke.Parameters[contextCount].Type)
                || invoke.Parameters[contextCount].RefKind != RefKind.None
                || !IsEntity(invoke.Parameters[contextCount + componentCount + 1].Type)
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

        signature = new ComparerSignature(
            invoke.Parameters.Skip(componentStart).Take(componentCount)
                .Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray(),
            hasContext,
            contextType,
            contextMode,
            hasEntity);
        return true;
    }

    private static bool HasComparerMarker(INamedTypeSymbol type)
        => type.AllInterfaces.Any(static candidate =>
            candidate.Name is "IComponentComparer" or "IComponentComparerEntity"
            && candidate.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace);

    private static bool HasEntityComparerMarker(INamedTypeSymbol type)
        => type.AllInterfaces.Any(static candidate =>
            candidate.Name == "IComponentComparerEntity"
            && candidate.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace);

    private static bool IsEntity(ITypeSymbol type)
        => type is INamedTypeSymbol named
            && named.Name == "Entity"
            && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;

    private static bool ContextArgumentMatches(SemanticModel model, ArgumentSyntax argument, ComparerSignature signature)
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

    private static string InvokeArguments(ComparerSignature signature, string contextName)
    {
        var arguments = new List<string>();
        if (signature.HasContext)
        {
            arguments.Add(SignatureProjection.ContextArgument(signature.ContextMode, contextName));
        }

        if (signature.HasEntity)
        {
            arguments.Add("leftEntity");
        }

        arguments.AddRange(Enumerable.Range(0, signature.ComponentTypes.Length)
            .Select(static index => $"in left{index}"));
        if (signature.HasEntity)
        {
            arguments.Add("rightEntity");
        }

        arguments.AddRange(Enumerable.Range(0, signature.ComponentTypes.Length)
            .Select(static index => $"in right{index}"));
        return string.Join(", ", arguments);
    }

    private static bool IsOrderedQueryExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        string methodName = member.Name.Identifier.ValueText;
        if (methodName == "OrderBy")
        {
            ITypeSymbol? sourceType = semanticModel.GetTypeInfo(member.Expression).Type;
            return sourceType is INamedTypeSymbol named
                && named.Name == "Query"
                && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;
        }

        return methodName == "ThenBy" && IsOrderedQueryExpression(member.Expression, semanticModel);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol)
        {
            return true;
        }

        return type is INamedTypeSymbol named && named.TypeArguments.Any(ContainsTypeParameter)
            || type is IArrayTypeSymbol array && ContainsTypeParameter(array.ElementType);
    }

    private static bool IsComponentId(ITypeSymbol? type)
        => type is INamedTypeSymbol named
            && named.Name == "ComponentId"
            && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;

    private static bool IsComponentIdSpan(ITypeSymbol? type)
        => type is INamedTypeSymbol named
            && named.Name is "ReadOnlySpan" or "Span"
            && named.ContainingNamespace.ToDisplayString() == "System"
            && named.TypeArguments.Length == 1
            && IsComponentId(named.TypeArguments[0]);

    private sealed class ComparerSignature
    {
        internal ComparerSignature(
            string[] componentTypes,
            bool hasContext,
            string? contextType,
            ContextModeKind contextMode,
            bool hasEntity)
        {
            ComponentTypes = componentTypes;
            HasContext = hasContext;
            ContextType = contextType;
            ContextMode = contextMode;
            HasEntity = hasEntity;
        }

        internal string[] ComponentTypes { get; }
        internal bool HasContext { get; }
        internal string? ContextType { get; }
        internal ContextModeKind ContextMode { get; }
        internal bool HasEntity { get; }
    }

    private sealed class ComponentComparerModel
    {
        internal ComponentComparerModel(INamedTypeSymbol functorType, ComparerSignature? signature, bool hasContextArgument)
        {
            FunctorType = functorType;
            Signature = signature;
            HasContextArgument = hasContextArgument;
        }

        internal INamedTypeSymbol FunctorType { get; }
        internal ComparerSignature? Signature { get; }
        internal bool HasContextArgument { get; }
        internal string[] ComponentTypes => Signature?.ComponentTypes ?? Array.Empty<string>();

        internal string Key => FunctorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            + "|" + string.Join("|", ComponentTypes)
            + "|" + Signature?.HasContext
            + "|" + Signature?.ContextType
            + "|" + Signature?.ContextMode
            + "|" + Signature?.HasEntity;
    }

    private static class ComponentComparerTemplates
    {
        internal static string Render(ComponentComparerModel model)
        {
            string hash = GeneratorSupport.StableName(model.Key);
            string comparerType = model.FunctorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            ComparerSignature signature = model.Signature!;
            string idFields = Join(model.ComponentTypes.Length, index => $"private readonly global::Delta.ECS.ComponentId _componentId{index};");
            string idAssignments = Join(model.ComponentTypes.Length, index => $"_componentId{index} = componentIds[{index}];");
            string idArguments = JoinCommaSeparated(model.ComponentTypes.Length, index => $"componentId{index}");
            string validation = Join(model.ComponentTypes.Length, index => $"world.ValidateGeneratedOrderedQueryKey<{model.ComponentTypes[index]}>(in query, _componentId{index});");
            string leftLocals = Join(model.ComponentTypes.Length, index => $"ref readonly {model.ComponentTypes[index]} left{index} = ref world.GetGeneratedOrderedQueryKey<{model.ComponentTypes[index]}>(left, _componentId{index});");
            string rightLocals = Join(model.ComponentTypes.Length, index => $"ref readonly {model.ComponentTypes[index]} right{index} = ref world.GetGeneratedOrderedQueryKey<{model.ComponentTypes[index]}>(right, _componentId{index});");
            string leftEntity = signature.HasEntity ? "global::Delta.ECS.Entity leftEntity = left;" : string.Empty;
            string rightEntity = signature.HasEntity ? "global::Delta.ECS.Entity rightEntity = right;" : string.Empty;
            string invokeArguments = InvokeArguments(signature, "_context");
            string primaryIds = string.Join(", ", model.ComponentTypes.Select(static componentType =>
                $"world.Layouts.GetPrimary(typeof({componentType}))"));
            string primaryOrderByIds = $"global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<{comparerType}>(in query, static world => new global::Delta.ECS.ComponentId[] {{ {primaryIds} }})";
            string primaryThenByIds = $"global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<{comparerType}>(in sourceQuery, static world => new global::Delta.ECS.ComponentId[] {{ {primaryIds} }})";
            string explicitIdParameters = JoinCommaSeparated(model.ComponentTypes.Length, index => $"global::Delta.ECS.ComponentId componentId{index}");
            string contextField = signature.HasContext ? $"private {signature.ContextType} _context;" : string.Empty;
            string contextConstructorParameter = signature.HasContext
                ? ", " + SignatureProjection.ContextParameter(signature.ContextMode, signature.ContextType!, "context")
                : string.Empty;
            string contextAssignment = signature.HasContext ? "_context = context;" : string.Empty;
            string primaryContextArgument = signature.HasContext ? ", " + SignatureProjection.ContextArgument(signature.ContextMode, "context") : string.Empty;
            string contextCallParameter = signature.HasContext
                ? ", " + SignatureProjection.ContextParameter(signature.ContextMode, signature.ContextType!, "context")
                : string.Empty;
            string contextCtorArgument = signature.HasContext ? ", " + SignatureProjection.ContextArgument(signature.ContextMode, "context") : string.Empty;
            string comparerNullCheck = model.FunctorType.IsReferenceType
                ? "global::Delta.ECS.GeneratedForEachRuntime.ThrowIfNull(comparer, nameof(comparer));"
                : string.Empty;
            string entityLeftAndRight = GeneratorTemplates.JoinNonEmpty(new[] { leftEntity, rightEntity });

            string source = $$"""
                // <auto-generated />
                #nullable enable

                namespace Delta.ECS;

                internal static class GeneratedComponentComparerExtensions_{{hash}}
                {
                    private sealed class Adapter : global::Delta.ECS.IGeneratedComponentComparer
                    {
                        private {{comparerType}} _comparer;
                        {{contextField}}
                        {{idFields}}

                        internal Adapter({{comparerType}} comparer, global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextConstructorParameter}})
                        {
                {{GeneratorTemplates.Indent(comparerNullCheck, "            ")}}
                            _comparer = comparer;
                {{GeneratorTemplates.Indent(contextAssignment, "            ")}}
                            global::Delta.ECS.GeneratedForEachRuntime.ValidateComponentIdCount(componentIds, {{model.ComponentTypes.Length}});
                {{GeneratorTemplates.Indent(idAssignments, "            ")}}
                        }

                        public void Validate(global::Delta.ECS.World world, in global::Delta.ECS.Query query)
                        {
                {{GeneratorTemplates.Indent(validation, "            ")}}
                        }

                        public int Compare(global::Delta.ECS.World world, global::Delta.ECS.Entity left, global::Delta.ECS.Entity right)
                        {
                {{GeneratorTemplates.Indent(leftLocals + "\n" + rightLocals, "            ")}}
                {{GeneratorTemplates.Indent(entityLeftAndRight, "            ")}}
                            return _comparer.Invoke({{invokeArguments}});
                        }
                    }

                    internal static global::Delta.ECS.OrderedQuery OrderBy(this global::Delta.ECS.Query query{{contextCallParameter}}, ref {{comparerType}} comparer)
                        => global::Delta.ECS.OrderedQuery.CreateGenerated(query,
                            new Adapter(comparer, {{primaryOrderByIds}}{{primaryContextArgument}}));

                    internal static global::Delta.ECS.OrderedQuery OrderBy(this global::Delta.ECS.Query query, {{explicitIdParameters}}{{contextCallParameter}}, ref {{comparerType}} comparer)
                        => global::Delta.ECS.OrderedQuery.CreateGenerated(query,
                            new Adapter(comparer, stackalloc global::Delta.ECS.ComponentId[] { {{idArguments}} }{{contextCtorArgument}}));

                    internal static global::Delta.ECS.OrderedQuery OrderBy(this global::Delta.ECS.Query query, global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextCallParameter}}, ref {{comparerType}} comparer)
                        => global::Delta.ECS.OrderedQuery.CreateGenerated(query, new Adapter(comparer, componentIds{{contextCtorArgument}}));

                    internal static global::Delta.ECS.OrderedQuery ThenBy(this global::Delta.ECS.OrderedQuery query{{contextCallParameter}}, ref {{comparerType}} comparer)
                    {
                        global::Delta.ECS.Query sourceQuery = query.SourceQuery;
                        return query.AppendGenerated(new Adapter(comparer, {{primaryThenByIds}}{{primaryContextArgument}}));
                    }

                    internal static global::Delta.ECS.OrderedQuery ThenBy(this global::Delta.ECS.OrderedQuery query, {{explicitIdParameters}}{{contextCallParameter}}, ref {{comparerType}} comparer)
                        => query.AppendGenerated(new Adapter(comparer, stackalloc global::Delta.ECS.ComponentId[] { {{idArguments}} }{{contextCtorArgument}}));

                    internal static global::Delta.ECS.OrderedQuery ThenBy(this global::Delta.ECS.OrderedQuery query, global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextCallParameter}}, ref {{comparerType}} comparer)
                        => query.AppendGenerated(new Adapter(comparer, componentIds{{contextCtorArgument}}));
                }
                """;
            return GeneratedSourceFormatter.Format(source);
        }

        private static string Join(int count, Func<int, string> render)
            => string.Join("\n", Enumerable.Range(0, count).Select(render));

        private static string JoinCommaSeparated(int count, Func<int, string> render)
            => string.Join(", ", Enumerable.Range(0, count).Select(render));
    }
}
