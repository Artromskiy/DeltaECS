using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for generated fluent query factories.</summary>
internal static class GeneratedQueryTemplates
{
    private static readonly (string Type, string Name, bool Query, bool QuerySpec, string Result)[] _factories =
    {
        ("World", "world", false, false, "world.CreateQuery(additions)"),
        ("Query", "query", true,
            false,
            "GeneratedForEachRuntime.ComposeGeneratedQuery(in query, additions)"),
        ("QuerySpec", "spec", false, true, string.Empty),
    };

    internal static string Render(QueryModel model)
    {
        string hash = GeneratorSupport.StableName(model.Key);
        IEnumerable<(string Type, string Name, bool Query, bool QuerySpec, string Result)> factoriesToRender =
            _factories.Where(factory => factory.QuerySpec == model.IsQuerySpecReceiver);
        string factories = GeneratorTemplates.JoinNonEmpty(
            factoriesToRender.Select(factory => RenderFactory(model, factory)),
            "\n\n");
        string extension = GeneratorTemplates.ExtensionTemplate(
            $"GeneratedQueryExtensions_{hash}",
            isInternal: false,
            GeneratorTemplates.Indent(factories, "    "));
        ImmutableArray<string> members = model.IsQuerySpecReceiver
            ? ImmutableArray.Create(extension)
            : ImmutableArray.Create(
                GeneratorTemplates.PrimaryComponentSetKeyDeclaration(model.Api.Selector.Arity),
                extension);
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            model.Namespace,
            GeneratorSupport.EcsNamespaceUsings(model.Namespace),
            members));
    }

    private static string RenderFactory(
        QueryModel model,
        (string Type, string Name, bool Query, bool QuerySpec, string Result) factory)
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

        body.Add(GeneratorTemplates.ComponentIdSpan(
            slots,
            factory.Name,
            model.Namespace,
            query: factory.Query));

        if (slots.HasExplicitIds && slots.HasGenericSelectors)
        {
            body.AddRange(GeneratorTemplates.Indexed(
                slots.Arity,
                index => factory.Query
                    ? $"GeneratedForEachRuntime.ValidateComponentType<{slots.GenericType(index)}>(in {factory.Name}, {slots.ComponentIdArgument(index)});"
                    : $"GeneratedForEachRuntime.ValidateComponentType<{slots.GenericType(index)}>({factory.Name}, {slots.ComponentIdArgument(index)});"));
        }

        if (factory.QuerySpec)
        {
            body.Add($"return {QuerySpecResult(model.Kind)};");
        }
        else
        {
            body.Add(additions);
            body.Add($"return {factory.Result};");
        }

        string returnType = factory.QuerySpec ? "QuerySpec" : "Query";
        string declaration = $$"""
            public static {{returnType}} {{model.Kind}}{{generic}}({{parameters}})
            """.Trim();
        return GeneratorTemplates.Method(
            model.Api,
            declaration,
            string.Join("\n", body),
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
    }

    private static string QuerySpecResult(string kind)
    {
        string filter = kind switch
        {
            "WhereAll" => "All",
            "WhereAny" => "Any",
            _ => "None",
        };
        return $"spec.With{filter}(components)";
    }

}
