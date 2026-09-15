using System.Collections.Immutable;
using System.Linq;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for generated fluent query factories.</summary>
internal static class GeneratedQueryTemplates
{
    internal static string Render(QueryModel model)
    {
        string hash = GeneratorSupport.StableName(model.Key);
        string members = GeneratorTemplates.JoinNonEmpty(
            new[]
            {
                RenderFactory(model, "World", "world", "world.CreateQuery"),
                RenderFactory(model, "Query", "query", "GeneratedForEachRuntime.ComposeGeneratedQuery")
            },
            "\n\n");
        RenderModel extension = GeneratorTemplates.ExtensionTemplate(
            model.Api,
            $"GeneratedQueryExtensions_{hash}",
            isInternal: false,
            GeneratorTemplates.Indent(members, "    "));
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            "Delta.ECS",
            ImmutableArray<string>.Empty,
            ImmutableArray.Create(extension)));
    }

    private static string RenderFactory(
        QueryModel model,
        string receiverType,
        string receiverName,
        string createCall)
    {
        string components = string.Join(
            "\n",
            Enumerable.Range(0, model.Arity).Select(index => receiverType == "World"
                ? $"components[{index}] = world.Layouts.GetPrimary<T{index + 1}>();"
                : $"components[{index}] = GeneratedForEachRuntime.GetGeneratedPrimary<T{index + 1}>(in query);"));
        string additions = $"QuerySpec additions = QuerySpec.{model.Kind}(components);";
        string result = receiverType == "World"
            ? $"return {createCall}(additions);"
            : $"return {createCall}(in {receiverName}, additions);";
        string declaration = $$"""
            public static Query {{model.Kind}}<{{GeneratorSupport.GenericTypes(model.Arity)}}>(this {{receiverType}} {{receiverName}})
            """.Trim();
        string body = $$"""
            global::System.Span<ComponentId> components = stackalloc ComponentId[{{model.Arity}}];
            {{components}}
            {{additions}}
            {{result}}
            """;
        return GeneratorTemplates.Method(
            model.Api,
            declaration,
            body,
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
    }

}
