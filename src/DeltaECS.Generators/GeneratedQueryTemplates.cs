using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for generated fluent query factories.</summary>
internal static class GeneratedQueryTemplates
{
    private static readonly (string Type, string Name, bool Query, string Result)[] _factories =
    {
        ("World", "world", false, "world.CreateQuery(additions)"),
        ("Query", "query", true,
            "GeneratedForEachRuntime.ComposeGeneratedQuery(in query, additions)")
    };

    internal static string Render(QueryModel model)
    {
        string hash = GeneratorSupport.StableName(model.Key);
        string factories = GeneratorTemplates.JoinNonEmpty(
            _factories.Select(factory => RenderFactory(model, factory)),
            "\n\n");
        string extension = GeneratorTemplates.ExtensionTemplate(
            $"GeneratedQueryExtensions_{hash}",
            isInternal: false,
            GeneratorTemplates.Indent(factories, "    "));
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            model.Namespace,
            GeneratorSupport.EcsNamespaceUsings(model.Namespace),
            ImmutableArray.Create(
                GeneratorTemplates.PrimaryComponentSetKeyDeclaration(model.Api.Selector.Arity),
                extension)));
    }

    private static string RenderFactory(
        QueryModel model,
        (string Type, string Name, bool Query, string Result) factory)
    {
        SignatureProjection slots = model.Api.Signature;
        string components = GeneratorTemplates.PrimaryComponentIds(
            factory.Name,
            GeneratorTemplates.Indexed(slots.Arity, index => slots.GenericType(index)).ToArray(),
            factory.Query,
            model.Namespace);
        string additions = $"QuerySpec additions = QuerySpec.{model.Kind}(components);";
        string declaration = $$"""
            public static Query {{model.Kind}}{{slots.GenericParameters()}}(this {{factory.Type}} {{factory.Name}})
            """.Trim();
        string body = $$"""
            global::System.ReadOnlySpan<ComponentId> components = {{components}};
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
