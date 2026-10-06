using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Emits visitor dispatchers for runtime-selected generic API calls.</summary>
[Generator]
public sealed class GeneratedGenericBindingsGenerator : IIncrementalGenerator
{
    private static readonly HashSet<string> FunctorMethods = new(StringComparer.Ordinal)
    {
        "ForEach", "ForEachEntity", "ForEachParallel", "ForEachEntityParallel",
    };

    private static readonly DiagnosticDescriptor UnsupportedGenericConstraints = GeneratorDiagnostics.Error(
        "DECSGEN008",
        "Unsupported generic constraints",
        "Open generic type '{0}' uses constraints unsupported by runtime-selected generic dispatch. Supported constraints are value type, unmanaged, reference type, nullable reference type, and public parameterless constructor.",
        "Generic bindings");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var discoveries = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntax, _) => ReadDiscovery(syntax))
            .Where(static discovery => discovery is not null)
            .Select(static (discovery, _) => discovery!)
            .Collect();

        var markedComponents = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Delta.ECS.DeltaEcsComponentAttribute",
                static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
                static (syntax, _) => (INamedTypeSymbol)syntax.TargetSymbol)
            .Collect();

        context.RegisterSourceOutput(discoveries.Combine(markedComponents).Combine(context.CompilationProvider), static (output, input) =>
        {
            ImmutableArray<GenericBindingDiscovery> discovered = input.Left.Left;
            ImmutableArray<INamedTypeSymbol> attributedTypes = input.Left.Right;

            var componentTypes = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
            var componentDefinitions = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var functorDefinitions = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            bool hasStructGenericTypeList = false;

            foreach (GenericBindingDiscovery discovery in discovered)
            {
                hasStructGenericTypeList |= discovery.IsStructGenericTypeList;
                if (discovery.RegisteredComponentType is ITypeSymbol registeredType
                    && IsClosedType(registeredType)
                    && GeneratorSupport.IsAccessibleSymbol(registeredType))
                {
                    AddIfMissing(componentTypes, GeneratorSupport.DisplayType(registeredType), registeredType);
                }

                if (discovery.GenericComponentDefinition is INamedTypeSymbol componentDefinition)
                {
                    if (!GenericTypeConstraintSupport.TryGetSupported(componentDefinition, out _))
                    {
                        ReportUnsupportedConstraints(output, componentDefinition, discovery.Location);
                    }
                    else if (GeneratorSupport.IsAccessibleSymbol(componentDefinition))
                    {
                        string key = GeneratorSupport.DisplayType(componentDefinition.ConstructUnboundGenericType());
                        AddIfMissing(componentDefinitions, key, componentDefinition);
                    }
                }

                if (discovery.GenericFunctorDefinition is INamedTypeSymbol functorDefinition)
                {
                    if (!GenericTypeConstraintSupport.TryGetSupported(functorDefinition, out _))
                    {
                        ReportUnsupportedConstraints(output, functorDefinition, discovery.Location);
                    }
                    else
                    {
                        string key = GeneratorSupport.DisplayType(functorDefinition.ConstructUnboundGenericType());
                        AddIfMissing(functorDefinitions, key, functorDefinition);
                    }
                }
            }

            foreach (INamedTypeSymbol componentType in attributedTypes)
            {
                if (!componentType.IsGenericType && GeneratorSupport.IsAccessibleSymbol(componentType))
                {
                    AddIfMissing(componentTypes, GeneratorSupport.DisplayType(componentType), componentType);
                }
            }

            var componentDispatchers = new List<GenericComponentDispatcherBinding>();
            var componentRegistrationArities = new HashSet<int>();
            foreach (INamedTypeSymbol definition in componentDefinitions.Values)
            {
                if (definition.Arity > 1)
                {
                    componentRegistrationArities.Add(definition.Arity);
                }

                _ = GenericTypeConstraintSupport.TryGetSupported(definition, out ImmutableArray<GenericTypeParameterConstraint> constraints);
                string openTypeName = GeneratorSupport.DisplayType(definition.ConstructUnboundGenericType());
                componentDispatchers.Add(new GenericComponentDispatcherBinding(
                    openTypeName,
                    "GenericComponentDispatcher_" + GeneratorSupport.StableName(openTypeName),
                    constraints,
                    definition.IsValueType,
                    definition.IsUnmanagedType,
                    definition.IsReferenceType,
                    GenericTypeConstraintSupport.HasPublicParameterlessConstructor(definition)));
            }

            var functorDispatchers = new List<GenericFunctorDispatcherBinding>();
            foreach (INamedTypeSymbol definition in functorDefinitions.Values)
            {
                GenericFunctorModel? model = GenericFunctorGenerator.CreateModel(definition);
                if (model is null)
                {
                    continue;
                }

                _ = GenericTypeConstraintSupport.TryGetSupported(definition, out ImmutableArray<GenericTypeParameterConstraint> constraints);
                string openTypeName = GeneratorSupport.DisplayType(definition.ConstructUnboundGenericType());
                functorDispatchers.Add(new GenericFunctorDispatcherBinding(
                    openTypeName,
                    "GenericFunctorDispatcher_" + GeneratorSupport.StableName(openTypeName),
                    "global::Delta.ECS.Generated.GenericFunctorExecutor_" + GeneratorSupport.StableName(model.TypeName),
                    constraints));
            }

            if (componentDispatchers.Count == 0
                && functorDispatchers.Count == 0
                && componentRegistrationArities.Count == 0
                && componentTypes.Count == 0
                && !hasStructGenericTypeList)
            {
                return;
            }

            bool needsModuleInitializerAttribute = input.Right.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is null;
            var generatedTypeTokens = componentTypes.Values
                .Select(type => new GeneratedTypeTokenBinding(
                    GeneratorSupport.DisplayType(type),
                    "RegisteredComponentTypeToken_" + GeneratorSupport.StableName(GeneratorSupport.DisplayType(type)),
                    type.IsValueType,
                    type.IsUnmanagedType,
                    type.IsReferenceType,
                    type is INamedTypeSymbol namedType && GenericTypeConstraintSupport.HasPublicParameterlessConstructor(namedType)))
                .ToArray();
            output.AddSource("GeneratedGenericBindings.g.cs", GenericBindingTemplates.Render(
                componentDispatchers,
                functorDispatchers,
                generatedTypeTokens,
                componentRegistrationArities,
                needsModuleInitializerAttribute));
        });
    }

    private static void ReportUnsupportedConstraints(SourceProductionContext output, INamedTypeSymbol definition, Location location)
        => output.ReportDiagnostic(Diagnostic.Create(
            UnsupportedGenericConstraints,
            location,
            GeneratorSupport.DisplayType(definition.ConstructUnboundGenericType())));

    private static GenericBindingDiscovery? ReadDiscovery(GeneratorSyntaxContext syntax)
    {
        var invocation = (InvocationExpressionSyntax)syntax.Node;
        IMethodSymbol? method = syntax.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (method is null)
        {
            return ReadGenericComponentRegistrationDiscovery(invocation, syntax.SemanticModel)
                ?? ReadFunctorDiscovery(invocation, syntax.SemanticModel);
        }

        if (IsLayoutRegistry(method.ContainingType) || IsGeneratedComponentRegistrationExtensions(method.ContainingType))
        {
            if (method.IsGenericMethod && method.Name == "Register" && method.TypeArguments.Length == 1)
            {
                return new GenericBindingDiscovery(method.TypeArguments[0], null, null, invocation.GetLocation());
            }

            return ReadGenericComponentRegistrationDiscovery(invocation, syntax.SemanticModel);
        }

        return ReadStructGenericTypeListDiscovery(method, invocation.GetLocation())
            ?? ReadFunctorDiscovery(invocation, syntax.SemanticModel);
    }

    private static GenericBindingDiscovery? ReadStructGenericTypeListDiscovery(IMethodSymbol method, Location location)
    {
        IMethodSymbol definition = method.OriginalDefinition;
        if (definition.Name != "Invoke"
            || definition.ContainingType.TypeKind != TypeKind.Interface
            || !definition.ReturnsVoid
            || definition.Parameters.Length != 0
            || definition.TypeParameters.Length != 1
            || !(definition.TypeParameters[0].HasValueTypeConstraint
                || definition.TypeParameters[0].HasUnmanagedTypeConstraint)
            || method.TypeArguments.Length != 1)
        {
            return null;
        }

        ITypeSymbol componentType = method.TypeArguments[0];
        if (!componentType.IsValueType
            || !IsClosedType(componentType)
            || !GeneratorSupport.IsAccessibleSymbol(componentType))
        {
            return null;
        }

        // A closed invocation is the compile-time type list for struct-generic actions.
        return new GenericBindingDiscovery(componentType, null, null, location, IsStructGenericTypeList: true);
    }

    private static bool IsClosedType(ITypeSymbol type)
        => type switch
        {
            ITypeParameterSymbol => false,
            IArrayTypeSymbol array => IsClosedType(array.ElementType),
            IPointerTypeSymbol pointer => IsClosedType(pointer.PointedAtType),
            INamedTypeSymbol namedType => !namedType.IsUnboundGenericType
                && namedType.TypeArguments.All(IsClosedType),
            _ => true,
        };

    private static GenericBindingDiscovery? ReadGenericComponentRegistrationDiscovery(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (GetMethodName(invocation.Expression) != "Register"
            || invocation.ArgumentList.Arguments.Count < 3
            || !TryGetOpenGenericType(invocation.ArgumentList.Arguments[0].Expression, semanticModel, out INamedTypeSymbol? definition))
        {
            return null;
        }

        return new GenericBindingDiscovery(null, definition, null, invocation.GetLocation());
    }

    private static GenericBindingDiscovery? ReadFunctorDiscovery(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (!FunctorMethods.Contains(GetMethodName(invocation.Expression)))
        {
            return null;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        for (int typeIndex = 0; typeIndex < arguments.Count; typeIndex++)
        {
            if (!TryGetOpenGenericType(arguments[typeIndex].Expression, semanticModel, out INamedTypeSymbol? definition)
                || typeIndex < definition.Arity)
            {
                continue;
            }

            if (!GenericTypeConstraintSupport.TryGetSupported(definition, out _))
            {
                return new GenericBindingDiscovery(null, null, definition, invocation.GetLocation());
            }

            if (GenericFunctorGenerator.CreateModel(definition) is not null)
            {
                return new GenericBindingDiscovery(null, null, definition, invocation.GetLocation());
            }
        }

        return null;
    }

    private static bool TryGetOpenGenericType(ExpressionSyntax expression, SemanticModel semanticModel, out INamedTypeSymbol definition)
    {
        if (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            return TryGetOpenGenericType(parenthesized.Expression, semanticModel, out definition);
        }

        if (expression is TypeOfExpressionSyntax typeOf
            && semanticModel.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol { IsUnboundGenericType: true } type)
        {
            definition = type.OriginalDefinition;
            return true;
        }

        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is ILocalSymbol or IFieldSymbol)
        {
            foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
            {
                SyntaxNode declaration = reference.GetSyntax();
                ExpressionSyntax? initializer = declaration switch
                {
                    VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                    PropertyDeclarationSyntax property => property.Initializer?.Value,
                    _ => null,
                };
                if (initializer is not null)
                {
                    return TryGetOpenGenericType(initializer, semanticModel.Compilation.GetSemanticModel(initializer.SyntaxTree), out definition);
                }
            }
        }

        definition = null!;
        return false;
    }

    private static bool IsLayoutRegistry(INamedTypeSymbol? type)
        => type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Delta.ECS.ComponentLayoutRegistry";

    private static bool IsGeneratedComponentRegistrationExtensions(INamedTypeSymbol? type)
        => type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Delta.ECS.GeneratedGenericComponentRegistrationExtensions";

    private static string GetMethodName(ExpressionSyntax expression)
        => expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => string.Empty,
        };

    private static void AddIfMissing<TValue>(Dictionary<string, TValue> dictionary, string key, TValue value)
    {
        if (!dictionary.ContainsKey(key))
        {
            dictionary.Add(key, value);
        }
    }

    private sealed record GenericBindingDiscovery(
        ITypeSymbol? RegisteredComponentType,
        INamedTypeSymbol? GenericComponentDefinition,
        INamedTypeSymbol? GenericFunctorDefinition,
        Location Location,
        bool IsStructGenericTypeList = false);
}
