using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates typed World factories and composable Query extensions only for
/// the WhereAll, WhereAny and WhereNone shapes used by a consumer compilation.
/// </summary>
[Generator]
public sealed class GeneratedQueryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            GeneratorPipeline.ShapeProvider<QueryModel>(
                    context,
                    static syntaxContext => TryReadShape(
                        syntaxContext.SemanticModel,
                        (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax)syntaxContext.Node,
                        out QueryModel? shape)
                        ? shape
                        : null)
                .Where(static shape => shape is not null)
                .Select(static (shape, _) => shape!)
                .Collect(),
            static (productionContext, shapes) => GeneratorPipeline.EmitShapes<QueryModel>(
                shapes,
                productionContext,
                "GeneratedQuery_",
                static shape => shape.Key,
                static shape => GeneratedQueryGenerator.Render(shape)));
    }

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out QueryModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not GenericNameSyntax genericName
            || genericName.Identifier.ValueText is not ("WhereAll" or "WhereAny" or "WhereNone")
            || invocation.ArgumentList.Arguments.Count != 0)
        {
            return false;
        }

        if (!IsWorldReceiver(model, member.Expression)
            && !IsQueryReceiver(model, member.Expression))
        {
            return false;
        }

        int arity = genericName.TypeArgumentList.Arguments.Count;
        if (arity < 1)
        {
            return false;
        }

        for (int index = 0; index < arity; index++)
        {
            ITypeSymbol? type = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[index]).Type;
            if (type is null
                || type.TypeKind == TypeKind.Error
                || type is ITypeParameterSymbol
                || !GeneratorSupport.IsAccessibleType(type))
            {
                return false;
            }
        }

        shape = new QueryModel(genericName.Identifier.ValueText, arity);
        return true;
    }

    private static bool IsWorldReceiver(SemanticModel model, ExpressionSyntax expression)
        => GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "World");

    private static bool IsQueryReceiver(SemanticModel model, ExpressionSyntax expression)
    {
        if (GeneratorSupport.IsNamedType(model.GetTypeInfo(expression).Type, "Query"))
        {
            return true;
        }

        // Earlier generated extension calls are not part of the input
        // compilation's semantic model, so recognize a fluent factory chain
        // syntactically as well as a resolved Query receiver.
        if (expression is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax member
            && member.Name is GenericNameSyntax genericName
            && genericName.Identifier.ValueText is ("WhereAll" or "WhereAny" or "WhereNone"))
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

    private static string Render(QueryModel shape)
    {
        RenderModel member = GeneratorTemplates.ExtensionTemplate(
            shape.Api,
            "GeneratedQueryExtensions_" + GeneratorSupport.StableName(shape.Key),
            isInternal: false,
            source =>
        {
            RenderFactory(source, shape, "World", "world", "world.CreateQuery");
            RenderFactory(source, shape, "Query", "query", "GeneratedForEachRuntime.ComposeGeneratedQuery");
        });
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            "Delta.ECS",
            ImmutableArray<string>.Empty,
            ImmutableArray.Create(member)));
    }

    private static void RenderFactory(
        StringBuilder source,
        QueryModel shape,
        string receiverType,
        string receiverName,
        string createCall)
    {
        source.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    public static Query ")
            .Append(shape.Kind)
            .Append('<')
            .Append(GeneratorSupport.GenericTypes(shape.Arity))
            .Append(">(this ")
            .Append(receiverType)
            .Append(' ')
            .Append(receiverName)
            .AppendLine(")");
        source.AppendLine("    {");
        source.Append("        global::System.Span<ComponentId> components = stackalloc ComponentId[")
            .Append(shape.Arity.ToString(CultureInfo.InvariantCulture))
            .AppendLine("];");

        for (int index = 0; index < shape.Arity; index++)
        {
            source.Append("        components[")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("] = ")
                .Append(receiverType == "World"
                    ? "world.Layouts.GetPrimary<T"
                    : "GeneratedForEachRuntime.GetGeneratedPrimary<T")
                .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                .Append(receiverType == "World"
                    ? ">();"
                    : ">(in query);")
                .AppendLine();
        }

        source.Append("        QuerySpec additions = QuerySpec.")
            .Append(shape.Kind)
            .AppendLine("(components);");
        if (receiverType == "World")
        {
            source.Append("        return ").Append(createCall).AppendLine("(additions);");
        }
        else
        {
            source.Append("        return ").Append(createCall).Append("(in ").Append(receiverName).AppendLine(", additions);");
        }

        source.AppendLine("    }");
    }

    private sealed class QueryModel
    {
        internal QueryModel(string kind, int arity)
        {
            Kind = kind;
            Arity = arity;
            Api = new ApiModel(
                OperationKind.QueryFactory,
                TargetKind.Query,
                QueryMode.None,
                new SelectorModel(
                    SelectorKind.Generic,
                    GeneratorSupport.ComponentModels(arity, SelectorKind.Generic, AccessKind.RowRead)),
                new ContextModel(ContextModeKind.None, null),
                null,
                new ExecutionModel(ExecutionKind.Dense, ValueKind.Component),
                kind);
        }

        internal string Kind { get; }
        internal int Arity { get; }
        internal ApiModel Api { get; }
        internal string Key => Api.SignatureKey;
    }
}
