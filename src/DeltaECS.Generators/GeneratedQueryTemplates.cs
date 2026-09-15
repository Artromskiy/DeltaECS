using System.Collections.Immutable;
using System.Globalization;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for generated fluent query factories.</summary>
internal static class GeneratedQueryTemplates
{
    private static readonly (string Type, string Name, string Resolver, string Result)[] _factories =
    {
        ("World", "world", "world.Layouts.GetPrimary<T{0}>()", "world.CreateQuery(additions)"),
        ("Query", "query", "GeneratedForEachRuntime.GetGeneratedPrimary<T{0}>(in query)",
            "GeneratedForEachRuntime.ComposeGeneratedQuery(in query, additions)")
    };

    internal static string Render(QueryModel model)
    {
        string hash = GeneratorSupport.StableName(model.Key);
        string members = GeneratorTemplates.JoinNonEmpty(
            _factories.Select(factory => RenderFactory(model, factory)),
            "\n\n");
        string extension = GeneratorTemplates.ExtensionTemplate(
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
        (string Type, string Name, string Resolver, string Result) factory)
    {
        SignatureProjection slots = model.Api.Signature;
        string components = GeneratorTemplates.JoinIndexed(
            slots.Arity,
            index => $"components[{index}] = {string.Format(CultureInfo.InvariantCulture, factory.Resolver, index + 1)};",
            "\n");
        string additions = $"QuerySpec additions = QuerySpec.{model.Kind}(components);";
        string declaration = $$"""
            public static Query {{model.Kind}}{{slots.GenericParameters()}}(this {{factory.Type}} {{factory.Name}})
            """.Trim();
        string body = $$"""
            global::System.Span<ComponentId> components = stackalloc ComponentId[{{slots.Arity}}];
            {{components}}
            {{additions}}
            return {{factory.Result}};
            """;
        return GeneratorTemplates.Method(
            model.Api,
            declaration,
            body,
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
    }

}
