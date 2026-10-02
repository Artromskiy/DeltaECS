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
        string additions = $"QuerySpec additions = QuerySpec.{model.Kind}(components);";
        string selector = slots.HasExplicitIds ? slots.ComponentIdParameters() : string.Empty;
        string parameters = GeneratorTemplates.JoinNonEmpty(new[]
        {
            $"this {factory.Type} {factory.Name}",
            selector
        }, ", ");
        string generic = slots.HasGenericSelectors ? slots.GenericParameters() : string.Empty;
        var body = new List<string>();
        if (slots.HasDynamicIds)
        {
            body.Add($"GeneratedForEachRuntime.ValidateComponentIdCount(componentIds, {slots.Arity});");
        }

        if (slots.HasDynamicIds)
        {
            body.Add("global::System.ReadOnlySpan<ComponentId> components = componentIds;");
        }
        else if (slots.HasExplicitIds)
        {
            body.Add($"global::System.Span<ComponentId> components = stackalloc ComponentId[{slots.Arity}];");
            body.AddRange(GeneratorTemplates.Indexed(
                slots.Arity,
                index => $"components[{index}] = {slots.ComponentIdArgument(index)};"));
        }
        else
        {
            string components = GeneratorTemplates.PrimaryComponentIds(
                factory.Name,
                GeneratorTemplates.Indexed(slots.Arity, index => slots.GenericType(index)).ToArray(),
                factory.Query,
                model.Namespace);
            body.Add($"global::System.ReadOnlySpan<ComponentId> components = {components};");
        }

        if (slots.HasExplicitIds && slots.HasGenericSelectors)
        {
            body.AddRange(GeneratorTemplates.Indexed(
                slots.Arity,
                index => factory.Query
                    ? $"GeneratedForEachRuntime.ValidateComponentType<{slots.GenericType(index)}>(in {factory.Name}, {slots.ComponentIdArgument(index)});"
                    : $"GeneratedForEachRuntime.ValidateComponentType<{slots.GenericType(index)}>({factory.Name}, {slots.ComponentIdArgument(index)});"));
        }

        body.Add(additions);
        body.Add($"return {factory.Result};");
        string declaration = $$"""
            public static Query {{model.Kind}}{{generic}}({{parameters}})
            """.Trim();
        return GeneratorTemplates.Method(
            model.Api,
            declaration,
            string.Join("\n", body),
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
    }

}
