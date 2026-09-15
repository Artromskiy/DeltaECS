namespace Delta.ECS.Generators;

/// <summary>Shared helpers for the pure generated-source templates.</summary>
internal static partial class GeneratorTemplates
{
    internal static string JoinNonEmpty(IEnumerable<string> fragments, string separator = "\n")
        => string.Join(separator, fragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)));

    internal static string JoinIndexed(int count, Func<int, string> render, string separator = ", ")
        => string.Join(separator, Enumerable.Range(0, count).Select(render));

    internal static IEnumerable<string> Indexed(int count, Func<int, string> render)
        => Enumerable.Range(0, count).Select(render);

    internal static IEnumerable<string> Indexed(int count, Func<int, bool> include, Func<int, string> render) => Enumerable.Range(0, count).Where(include).Select(render);

    internal static IEnumerable<int> WriteIndices(IReadOnlyList<ComponentModel> components) => Enumerable.Range(0, components.Count).Where(index => components[index].IsWrite);
    internal static string WriteSpan(IEnumerable<ComponentModel> components)
    {
        string indices = string.Join(", ", components.Select((component, index) => component.IsWrite ? $"GeneratedForEachRuntime.GetWriteQueryComponentIndex(access{index})" : string.Empty).Where(static value => value.Length != 0));
        return indices.Length == 0 ? "global::System.ReadOnlySpan<int>.Empty" : $"stackalloc int[] {{ {indices} }}";
    }
    internal static string RowReference(string componentType, ComponentModel component, int index, string indent = "") => $"{indent}ref {componentType} row{index} = ref slots.GetGenerated{(component.IsWrite ? "Write" : "Read")}Reference<{componentType}>(_access{index});";
    internal static string ElementReference(string componentType, int index, string indent = "") => $"{indent}ref {componentType} component{index} = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref row{index}, index);";

    internal static string Method(ApiModel api, string declaration, string body, string? attributes = null)
        => RenderBlock(
            JoinNonEmpty(new[] { Documentation(api), attributes ?? string.Empty, declaration }),
            body);

    internal static string Declaration(ApiModel api, string declaration, string? attributes = null)
        => JoinNonEmpty(new[] { Documentation(api), attributes ?? string.Empty, declaration });

    internal static string RenderBlock(string declaration, string body)
    {
        string content = body.Trim();
        if (content.Length == 0)
        {
            return $"{declaration}\n{{\n}}";
        }

        return $$"""
            {{declaration}}
            {
            {{Indent(content, "    ")}}
            }
            """;
    }

    internal static string Indent(string text, string indent)
        => string.Join(
            "\n",
            text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.Length == 0 ? line : indent + line));

    internal static string ValueInitializer(
        string name,
        SignatureProjection slots,
        string genericPrefix,
        string writeMethod,
        string visibility = "private")
    {
        string generic = slots.GenericList(genericPrefix);
        string fields = JoinIndexed(slots.Arity, index => $"private readonly ComponentId component{index};\nprivate readonly {slots.GenericType(index, genericPrefix)} value{index};", "\n");
        string parameters = JoinIndexed(slots.Arity, index => $"ComponentId component{index}, in {slots.GenericType(index, genericPrefix)} value{index}");
        string assignments = JoinIndexed(slots.Arity, index => $"this.component{index} = component{index};\nthis.value{index} = value{index};", "\n");
        string writes = JoinIndexed(slots.Arity, index => $"writer.{writeMethod}(component{index}, in value{index});", "\n");
        string body = JoinNonEmpty(new[]
        {
            RenderBlock($"internal {name}({parameters})", assignments),
            RenderBlock("public void Initialize(ref global::Delta.ECS.GeneratedComponentValueWriter writer)", writes)
        }, "\n\n");
        return RenderBlock(
            $"{visibility} struct {name}<{generic}> : global::Delta.ECS.IGeneratedComponentValueInitializer",
            JoinNonEmpty(new[] { fields, body }, "\n\n"));
    }

}
