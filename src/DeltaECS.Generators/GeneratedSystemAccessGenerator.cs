using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Collects world/component access from <c>ISystem</c> implementations and,
/// for partial systems, supplies the required generated <c>Access</c> member.
/// </summary>
[Generator]
public sealed class GeneratedSystemAccessGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol?> systems = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is BaseTypeDeclarationSyntax { BaseList: not null },
                static (syntaxContext, _) => syntaxContext.SemanticModel.GetDeclaredSymbol(syntaxContext.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null)
            .Collect()
            .SelectMany(static (symbols, _) => symbols);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(systems.Collect()),
            static (productionContext, input) => Execute(input.Left, input.Right, productionContext));
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> candidates,
        SourceProductionContext context)
    {
        INamedTypeSymbol? systemInterface = compilation.GetTypeByMetadataName("Delta.ECS.Systems.ISystem");
        if (systemInterface is null
            || compilation.GetTypeByMetadataName("Delta.ECS.Systems.SystemAccess") is null)
        {
            return;
        }

        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol? candidate in candidates)
        {
            if (candidate is null
                || candidate.TypeKind != TypeKind.Class
                || candidate.Arity != 0
                || !seen.Add(candidate)
                || !Implements(candidate, systemInterface))
            {
                continue;
            }

            ImmutableArray<TypeDeclarationSyntax> declarations = candidate.DeclaringSyntaxReferences
                .Select(static reference => reference.GetSyntax())
                .OfType<TypeDeclarationSyntax>()
                .ToImmutableArray();
            if (declarations.IsDefaultOrEmpty)
            {
                continue;
            }

            var accumulator = new GeneratedSystemAccessAccumulator();
            foreach (TypeDeclarationSyntax declaration in declarations)
            {
                SemanticModel model = compilation.GetSemanticModel(declaration.SyntaxTree);
                foreach (InvocationExpressionSyntax invocation in declaration.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => IsOwnedBy(invocation, declaration)))
                {
                    ReadInvocation(model, invocation, accumulator);
                }
            }

            bool hasAccess = candidate.GetMembers("Access").Length != 0;
            bool injectProperty = !hasAccess
                && candidate.ContainingType is null
                && candidate.Arity == 0
                && declarations.All(static declaration => declaration is ClassDeclarationSyntax)
                && declarations.Any(static declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
            string key = candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string helperName = "GeneratedSystemAccess_" + GeneratorSupport.StableName(key);
            GeneratedSystemAccessModel access = accumulator.Build(
                candidate.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                candidate.Name,
                helperName,
                injectProperty);
            context.AddSource(helperName + ".g.cs", GeneratedSystemAccessTemplates.Render(access));
        }
    }

    private static bool Implements(INamedTypeSymbol candidate, INamedTypeSymbol systemInterface)
        => candidate.AllInterfaces.Any(interfaceType =>
            SymbolEqualityComparer.Default.Equals(interfaceType, systemInterface));

    private static bool IsOwnedBy(InvocationExpressionSyntax invocation, TypeDeclarationSyntax declaration)
        => invocation.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault() is { } owner
            && owner.SpanStart == declaration.SpanStart;

    private static void ReadInvocation(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        GeneratedSystemAccessAccumulator accumulator)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return;
        }

        string name = member.Name switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => member.Name.Identifier.ValueText
        };
        bool worldReceiver = GeneratorSupport.IsNamedType(model.GetTypeInfo(member.Expression).Type, "World");
        bool queryReceiver = IsQueryReceiver(model, member.Expression);
        if (ReadAccessor(name, model, invocation, member.Name, worldReceiver, accumulator))
        {
            return;
        }

        if (!ApiDescriptor.TryGet(name, out ApiDescriptor descriptor))
        {
            if (worldReceiver || queryReceiver)
            {
                accumulator.Unknown();
            }

            return;
        }

        bool whereTerminal = IsWhereInvocation(member.Expression, out InvocationExpressionSyntax? whereInvocation);
        if (descriptor.Family == GeneratedApiKind.QueryFactory)
        {
            if (!worldReceiver && !queryReceiver)
            {
                return;
            }

            accumulator.ReadTopology();
            ReadGenericTypes(model, member.Name, accumulator.Read);
            if (invocation.ArgumentList.Arguments.Count != 0)
            {
                accumulator.Unknown();
            }

            return;
        }

        if (descriptor.Family == GeneratedApiKind.Where)
        {
            if (worldReceiver)
            {
                accumulator.ReadTopology();
                ReadWherePredicate(model, invocation, descriptor.HasEntity, accumulator);
            }

            return;
        }

        if (descriptor.Family == GeneratedApiKind.Iteration)
        {
            if (whereTerminal)
            {
                ReadWherePredicate(model, whereInvocation!, whereInvocation!.Expression is MemberAccessExpressionSyntax whereMember
                    && whereMember.Name.Identifier.ValueText == "WhereEntity", accumulator);
                accumulator.ReadTopology();
                if (descriptor.Schedule == Schedule.Parallel)
                {
                    accumulator.Parallel();
                }

                ReadWhereTerminalIteration(model, invocation, descriptor.HasEntity, accumulator);
                return;
            }
            else if (!worldReceiver)
            {
                return;
            }

            accumulator.ReadTopology();
            if (descriptor.Schedule == Schedule.Parallel)
            {
                accumulator.Parallel();
            }

            ReadIteration(model, invocation, descriptor, accumulator);
            return;
        }

        if (descriptor.Family != GeneratedApiKind.Structural)
        {
            return;
        }

        if (whereTerminal)
        {
            ReadWherePredicate(model, whereInvocation!, whereInvocation!.Expression is MemberAccessExpressionSyntax whereMember
                && whereMember.Name.Identifier.ValueText == "WhereEntity", accumulator);
        }
        else if (!worldReceiver)
        {
            return;
        }

        ReadStructural(model, invocation, name, member.Name, accumulator);
    }

    private static void ReadIteration(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        GeneratedSystemAccessAccumulator accumulator)
    {
        GenericNameSyntax? genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
        ITypeSymbol[] genericTypes = genericName is null
            ? Array.Empty<ITypeSymbol>()
            : genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .Where(static type => type is not null)
                .Cast<ITypeSymbol>()
                .ToArray();

        int callbackIndex = FindCallbackIndex(model, invocation);
        if (callbackIndex < 0)
        {
            accumulator.Unknown();
            return;
        }

        if (!new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor)
            .TryRead(callbackIndex, out InvocationCursorResult cursor))
        {
            accumulator.Unknown();
            return;
        }

        if (cursor.ComponentIdCount != 0)
        {
            accumulator.Unknown();
        }

        ReadCallback(
            model,
            invocation.ArgumentList.Arguments[callbackIndex].Expression,
            genericTypes,
            descriptor.HasEntity,
            cursor.HasContext,
            descriptor.Value == ValueDomain.Stamp,
            accumulator);
    }

    private static void ReadWherePredicate(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool hasEntity,
        GeneratedSystemAccessAccumulator accumulator)
    {
        int callbackIndex = FindCallbackIndex(model, invocation);
        if (callbackIndex < 0)
        {
            accumulator.Unknown();
            return;
        }

        bool hasContext = callbackIndex > 1;
        ReadCallback(
            model,
            invocation.ArgumentList.Arguments[callbackIndex].Expression,
            Array.Empty<ITypeSymbol>(),
            hasEntity,
            hasContext,
            stamp: false,
            accumulator);
    }

    private static void ReadStructural(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        string name,
        NameSyntax memberName,
        GeneratedSystemAccessAccumulator accumulator)
    {
        GenericNameSyntax? genericName = memberName as GenericNameSyntax;
        if (genericName is null && name != "Destroy")
        {
            accumulator.Unknown();
        }
        else if (invocation.ArgumentList.Arguments.Any(argument =>
            GeneratorSupport.IsComponentId(model.GetTypeInfo(argument.Expression).Type)))
        {
            accumulator.Unknown();
        }

        ITypeSymbol[] types = genericName is null
            ? Array.Empty<ITypeSymbol>()
            : genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .Where(static type => type is not null)
                .Cast<ITypeSymbol>()
                .ToArray();
        switch (name)
        {
            case "Add":
                accumulator.WriteTopology();
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Add(type);
                    accumulator.Write(type);
                }

                break;
            case "Remove":
                accumulator.WriteTopology();
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Remove(type);
                }

                break;
            case "Set":
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Write(type);
                }

                break;
            case "Create":
                accumulator.WriteTopology();
                accumulator.CreateEntities();
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Add(type);
                }

                break;
            case "Destroy":
                accumulator.WriteTopology();
                accumulator.DestroyEntities();
                break;
        }
    }

    private static bool ReadAccessor(
        string name,
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        NameSyntax memberName,
        bool worldReceiver,
        GeneratedSystemAccessAccumulator accumulator)
    {
        if (name is not ("Get" or "GetRead" or "GetReadRef" or "GetRefRead" or "GetRef"
            or "TryGet" or "Has" or "TryGetComponentStamp"))
        {
            return false;
        }

        if (!worldReceiver)
        {
            return true;
        }

        GenericNameSyntax? genericName = memberName as GenericNameSyntax;
        ITypeSymbol[] types = genericName is null
            ? Array.Empty<ITypeSymbol>()
            : genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .Where(static type => type is not null)
                .Cast<ITypeSymbol>()
                .ToArray();
        if (genericName is null)
        {
            accumulator.Unknown();
        }
        else if (invocation.ArgumentList.Arguments.Any(argument =>
            GeneratorSupport.IsComponentId(model.GetTypeInfo(argument.Expression).Type)))
        {
            accumulator.Unknown();
        }

        switch (name)
        {
            case "GetRef":
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Write(type);
                }

                break;
            case "TryGetComponentStamp":
                foreach (ITypeSymbol type in types)
                {
                    accumulator.StampRead(type);
                }

                break;
            case "Has":
                accumulator.ReadTopology();
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Read(type);
                }

                break;
            default:
                foreach (ITypeSymbol type in types)
                {
                    accumulator.Read(type);
                }

                break;
        }

        return true;
    }

    private static void ReadWhereTerminalIteration(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool hasEntity,
        GeneratedSystemAccessAccumulator accumulator)
    {
        int callbackIndex = FindCallbackIndex(model, invocation);
        if (callbackIndex < 0)
        {
            accumulator.Unknown();
            return;
        }

        GenericNameSyntax? genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
        ITypeSymbol[] genericTypes = genericName is null
            ? Array.Empty<ITypeSymbol>()
            : genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .Where(static type => type is not null)
                .Cast<ITypeSymbol>()
                .ToArray();
        ReadCallback(
            model,
            invocation.ArgumentList.Arguments[callbackIndex].Expression,
            genericTypes,
            hasEntity,
            callbackIndex != 0,
            stamp: false,
            accumulator);
    }

    private static void ReadCallback(
        SemanticModel model,
        ExpressionSyntax expression,
        IReadOnlyList<ITypeSymbol> genericTypes,
        bool hasEntity,
        bool hasContext,
        bool stamp,
        GeneratedSystemAccessAccumulator accumulator)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
            int index = hasContext ? 1 : 0;
            if (hasEntity && index < parameters.Length)
            {
                index++;
            }

            for (int componentIndex = 0; index < parameters.Length; componentIndex++, index++)
            {
                ITypeSymbol? type = genericTypes.Count > componentIndex
                    ? genericTypes[componentIndex]
                    : model.GetDeclaredSymbol(parameters[index])?.GetSymbolType();
                AddCallbackType(type, CallbackReader.ParameterRefKind(parameters[index]), stamp, accumulator);
            }

            return;
        }

        ITypeSymbol? expressionType = model.GetTypeInfo(expression).Type;
        if (expressionType is INamedTypeSymbol functor
            && CallbackReader.TryGetForEachMarker(functor, out bool markerContext, out bool markerEntity, out ITypeSymbol? contextType))
        {
            hasContext = markerContext;
            hasEntity = markerEntity;
            IMethodSymbol[] methods = functor.GetMembers("Invoke")
                .OfType<IMethodSymbol>()
                .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false))
                .ToArray();
            if (methods.Length == 1)
            {
                ReadMethodParameters(methods[0], hasContext, hasEntity, genericTypes, stamp, accumulator);
                return;
            }
        }

        if (expressionType is INamedTypeSymbol delegateType
            && delegateType.DelegateInvokeMethod is { } delegateInvoke)
        {
            if (delegateType.Name == "QueryChunkAction"
                || delegateInvoke.Parameters.Any(parameter => GeneratorSupport.IsEcsType(parameter.Type, "QueryChunk")))
            {
                accumulator.Unknown();
                return;
            }

            ReadMethodParameters(delegateInvoke, hasContext, hasEntity, genericTypes, stamp, accumulator);
            return;
        }

        if (expressionType is INamedTypeSymbol whereFunctor
            && CallbackReader.HasWherePredicateMarker(whereFunctor))
        {
            IMethodSymbol[] methods = whereFunctor.GetMembers("Invoke")
                .OfType<IMethodSymbol>()
                .Where(static method => method.ReturnType.SpecialType == SpecialType.System_Boolean)
                .ToArray();
            if (methods.Length == 1)
            {
                IMethodSymbol method = methods[0];
                int index = method.Parameters.Length > 0
                    && method.Parameters[0].RefKind == RefKind.Ref
                    && !GeneratorSupport.IsEntityType(method.Parameters[0].Type)
                    ? 1
                    : 0;
                if (hasEntity && index < method.Parameters.Length
                    && GeneratorSupport.IsEntityType(method.Parameters[index].Type))
                {
                    index++;
                }

                for (; index < method.Parameters.Length; index++)
                {
                    IParameterSymbol parameter = method.Parameters[index];
                    AddCallbackType(parameter.Type, parameter.RefKind, stamp, accumulator);
                }

                return;
            }
        }

        if (CallbackReader.TryGetMethodGroupTarget(model, expression, out IMethodSymbol? callbackMethod)
            && callbackMethod is not null)
        {
            ReadMethodParameters(callbackMethod, hasContext, hasEntity, genericTypes, stamp, accumulator);
            return;
        }

        accumulator.Unknown();
    }

    private static void ReadMethodParameters(
        IMethodSymbol method,
        bool hasContext,
        bool hasEntity,
        IReadOnlyList<ITypeSymbol> genericTypes,
        bool stamp,
        GeneratedSystemAccessAccumulator accumulator)
    {
        int index = hasContext ? 1 : 0;
        if (hasEntity && index < method.Parameters.Length)
        {
            index++;
        }

        for (int componentIndex = 0; index < method.Parameters.Length; componentIndex++, index++)
        {
            IParameterSymbol parameter = method.Parameters[index];
            ITypeSymbol type = genericTypes.Count > componentIndex ? genericTypes[componentIndex] : parameter.Type;
            AddCallbackType(type, parameter.RefKind, stamp, accumulator);
        }
    }

    private static void AddCallbackType(
        ITypeSymbol? type,
        RefKind refKind,
        bool stamp,
        GeneratedSystemAccessAccumulator accumulator)
    {
        if (stamp)
        {
            accumulator.StampRead(type);
        }
        else if (refKind == RefKind.Ref)
        {
            accumulator.Write(type);
        }
        else
        {
            accumulator.Read(type);
        }
    }

    private static int FindCallbackIndex(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        for (int index = invocation.ArgumentList.Arguments.Count - 1; index >= 0; index--)
        {
            ExpressionSyntax expression = invocation.ArgumentList.Arguments[index].Expression;
            if (expression is LambdaExpressionSyntax
                || model.GetSymbolInfo(expression).Symbol is IMethodSymbol
                || CallbackReader.TryGetMethodGroupTarget(model, expression, out _)
                || model.GetTypeInfo(expression).Type is INamedTypeSymbol type
                    && (type.DelegateInvokeMethod is not null
                        || CallbackReader.TryGetForEachMarker(type, out _, out _, out _)
                        || CallbackReader.HasWherePredicateMarker(type)))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsWhereInvocation(
        ExpressionSyntax expression,
        out InvocationExpressionSyntax? whereInvocation)
    {
        whereInvocation = expression as InvocationExpressionSyntax;
        return whereInvocation?.Expression is MemberAccessExpressionSyntax member
            && member.Name.Identifier.ValueText is "Where" or "WhereEntity";
    }

    private static bool IsQueryReceiver(SemanticModel model, ExpressionSyntax expression)
    {
        if (GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "Query"))
        {
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax member
            && member.Name is GenericNameSyntax genericName
            && genericName.Identifier.ValueText is "WhereAll" or "WhereAny" or "WhereNone")
        {
            return IsWorldReceiver(model, member.Expression)
                || IsQueryReceiver(model, member.Expression);
        }

        if (expression is IdentifierNameSyntax identifier
            && model.GetSymbolInfo(identifier).Symbol is ILocalSymbol local
            && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax declarator
            && declarator.Initializer?.Value is ExpressionSyntax initializer)
        {
            return IsWorldReceiver(model, initializer)
                || IsQueryReceiver(model, initializer);
        }

        return false;
    }

    private static bool IsWorldReceiver(SemanticModel model, ExpressionSyntax expression)
        => GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "World");

    private static void ReadGenericTypes(
        SemanticModel model,
        NameSyntax name,
        Action<ITypeSymbol?> add)
    {
        if (name is not GenericNameSyntax genericName)
        {
            return;
        }

        foreach (TypeSyntax argument in genericName.TypeArgumentList.Arguments)
        {
            add(model.GetTypeInfo(argument).Type);
        }
    }
}

internal static class GeneratedSystemAccessSymbolExtensions
{
    internal static ITypeSymbol? GetSymbolType(this ISymbol? symbol)
        => symbol switch
        {
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };
}
