using System;
using System.Collections.Generic;
using System.Linq;
namespace Delta.ECS.Generators;

/// <summary>Shared helpers for the pure generated-source templates.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel RenderMember(ApiModel api, string source)
        => new(api, source);

    internal static string JoinNonEmpty(IEnumerable<string> fragments, string separator = "\n")
        => string.Join(separator, fragments.Where(static fragment => !string.IsNullOrWhiteSpace(fragment)));

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

}
