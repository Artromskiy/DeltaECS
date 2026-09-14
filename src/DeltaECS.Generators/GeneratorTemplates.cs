using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Delta.ECS.Generators;

/// <summary>Shared helpers for the pure generated-source templates.</summary>
internal static partial class GeneratorTemplates
{
    internal static string RenderMember(Action<StringBuilder> render)
    {
        var source = new StringBuilder(8 * 1024);
        render(source);
        return source.ToString();
    }

    internal static RenderModel RenderMember(ApiModel api, Action<StringBuilder> render)
        => new(api, RenderMember(render));

    internal static string JoinLines(IEnumerable<string> lines)
        => string.Join("\n", lines);

    internal static string RenderParameters(
        IEnumerable<ComponentModel> components,
        Func<ComponentModel, string> render)
        => string.Join(", ", components.Select(render));

    private static string RenderBlock(string declaration, string body)
    {
        string newline = body.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
        return $$"""
            {{declaration}}
            {
            {{body}}{{newline}}}
            """;
    }

    private static string RenderLines(ImmutableArray<string> lines)
        => lines.IsDefaultOrEmpty ? string.Empty : JoinLines(lines);
}
