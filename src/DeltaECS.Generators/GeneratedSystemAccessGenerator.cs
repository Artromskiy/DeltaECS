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
            string namespaceName = candidate.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
                ? containingNamespace.ToDisplayString()
                : string.Empty;
            GeneratedSystemAccessModel access = accumulator.Build(
                namespaceName,
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
        bool worldReceiver = GeneratorSupport.IsWorldReceiver(model, member.Expression);
        bool queryReceiver = GeneratorSupport.IsQueryReceiver(model, member.Expression);
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

        bool whereTerminal = InvocationGrammar.IsWhereInvocation(member.Expression, out InvocationExpressionSyntax? whereInvocation);
        if (descriptor.Family is GeneratedApiKind.QueryFactory or GeneratedApiKind.Where)
        {
            if (descriptor.Family == GeneratedApiKind.QueryFactory
                ? !worldReceiver && !queryReceiver
                : !worldReceiver)
            {
                return;
            }

            accumulator.ReadTopology();
            if (descriptor.Family == GeneratedApiKind.QueryFactory)
            {
                if (invocation.ArgumentList.Arguments.Count != 0)
                {
                    accumulator.Unknown();
                }
            }
            else
            {
                ReadWhereCallback(model, invocation, descriptor.HasEntity, isTerminal: false, accumulator);
            }

            return;
        }

        if (descriptor.Family == GeneratedApiKind.Iteration)
        {
            if (whereTerminal)
            {
                ReadWhereCallback(model, whereInvocation!, whereInvocation!.Expression is MemberAccessExpressionSyntax whereMember
                    && whereMember.Name.Identifier.ValueText == "WhereEntity", isTerminal: false, accumulator);
                accumulator.ReadTopology();
                if (descriptor.Schedule == Schedule.Parallel)
                {
                    accumulator.Parallel();
                }

                ReadWhereCallback(model, invocation, descriptor.HasEntity, isTerminal: true, accumulator);
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
            ReadWhereCallback(model, whereInvocation!, whereInvocation!.Expression is MemberAccessExpressionSyntax whereMember
                && whereMember.Name.Identifier.ValueText == "WhereEntity", isTerminal: false, accumulator);
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
        ITypeSymbol[] genericTypes = GeneratorSupport.GenericArgumentTypes(model, genericName);

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

        if (cursor.HasComponentIds)
        {
            accumulator.Unknown();
        }

        string queryExpression = string.Empty;
        bool queryScoped = !cursor.HasComponentIds
            && cursor.HasQuery
            && TryGetReadOnlyQueryField(model, invocation, cursor.QueryArgumentIndex, out queryExpression);
        GeneratedSystemAccessAccumulator callbackAccess = queryScoped
            ? new GeneratedSystemAccessAccumulator()
            : accumulator;
        ReadCallback(
            model,
            invocation.ArgumentList.Arguments[callbackIndex].Expression,
            genericTypes,
            descriptor.HasEntity,
            cursor.HasContext,
            descriptor.Value == ValueDomain.Stamp,
            callbackAccess);
        if (queryScoped)
        {
            accumulator.AddQueryAccess(queryExpression, callbackAccess);
        }
    }

    private static bool TryGetReadOnlyQueryField(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        int queryArgumentIndex,
        out string queryExpression)
    {
        queryExpression = string.Empty;
        if (queryArgumentIndex < 0
            || queryArgumentIndex >= invocation.ArgumentList.Arguments.Count)
        {
            return false;
        }

        ExpressionSyntax expression = invocation.ArgumentList.Arguments[queryArgumentIndex].Expression;
        if (model.GetSymbolInfo(expression).Symbol is not IFieldSymbol { IsReadOnly: true } field
            || !GeneratorSupport.IsEcsType(field.Type, "Query")
            || !SymbolEqualityComparer.Default.Equals(
                field.ContainingType,
                model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType))
        {
            return false;
        }

        queryExpression = expression.ToString();
        return true;
    }

    private static void ReadWhereCallback(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool hasEntity,
        bool isTerminal,
        GeneratedSystemAccessAccumulator accumulator)
    {
        int callbackIndex = FindCallbackIndex(model, invocation);
        if (callbackIndex < 0)
        {
            accumulator.Unknown();
            return;
        }

        ITypeSymbol[] genericTypes = isTerminal
            ? GeneratorSupport.GenericArgumentTypes(
                model,
                (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax)
            : Array.Empty<ITypeSymbol>();
        ReadCallback(
            model,
            invocation.ArgumentList.Arguments[callbackIndex].Expression,
            genericTypes,
            hasEntity,
            isTerminal ? callbackIndex != 0 : callbackIndex > 1,
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
        ITypeSymbol[] types = ReadGenericTypes(
            model,
            invocation,
            genericName,
            accumulator,
            required: name != "Destroy");
        accumulator.WriteTopology();
        if (name == "Destroy")
        {
            accumulator.DestroyEntities();
            return;
        }

        if (name == "Create")
        {
            accumulator.CreateEntities();
        }

        accumulator.Apply(types, name == "Remove" ? accumulator.Remove : accumulator.Add);
        if (name == "Add")
        {
            accumulator.Apply(types, accumulator.Write);
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
        ITypeSymbol[] types = ReadGenericTypes(model, invocation, genericName, accumulator, required: true);

        if (name == "Has")
        {
            accumulator.ReadTopology();
        }

        Action<ITypeSymbol> access = name switch
        {
            "GetRef" => accumulator.Write,
            "TryGetComponentStamp" => accumulator.StampRead,
            _ => accumulator.Read
        };
        accumulator.Apply(types, access);

        return true;
    }

    private static ITypeSymbol[] ReadGenericTypes(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        GenericNameSyntax? genericName,
        GeneratedSystemAccessAccumulator accumulator,
        bool required)
    {
        if ((required && genericName is null)
            || invocation.ArgumentList.Arguments.Any(argument =>
                GeneratorSupport.IsComponentId(model.GetTypeInfo(argument.Expression).Type)))
        {
            accumulator.Unknown();
        }

        return GeneratorSupport.GenericArgumentTypes(model, genericName);
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
            IParameterSymbol?[] parameters = CallbackReader.LambdaParameters(lambda)
                .Select(parameter => model.GetDeclaredSymbol(parameter) as IParameterSymbol)
                .ToArray();
            int index = hasContext ? 1 : 0;
            if (hasEntity && index < parameters.Length)
            {
                index++;
            }

            ReadComponentParameters(parameters, index, genericTypes, stamp, accumulator);
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
                .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false, entityRef: true))
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

                ReadComponentParameters(method.Parameters, index, genericTypes, stamp, accumulator);
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

        ReadComponentParameters(method.Parameters, index, genericTypes, stamp, accumulator);
    }

    private static void ReadComponentParameters(
        IReadOnlyList<IParameterSymbol?> parameters,
        int start,
        IReadOnlyList<ITypeSymbol> genericTypes,
        bool stamp,
        GeneratedSystemAccessAccumulator accumulator)
    {
        for (int index = start, componentIndex = 0; index < parameters.Count; index++, componentIndex++)
        {
            IParameterSymbol? parameter = parameters[index];
            ITypeSymbol? type = genericTypes.Count > componentIndex
                ? genericTypes[componentIndex]
                : parameter?.Type;
            AddCallbackType(type, parameter?.RefKind ?? RefKind.None, stamp, accumulator);
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

}
