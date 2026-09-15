using System.Globalization;

namespace Delta.ECS.Generators;

/// <summary>Template for generated XML documentation attached to public API members.</summary>
internal static partial class GeneratorTemplates
{
    internal static string Documentation(ApiModel api)
    {
        string summary = api.Summary
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
        return string.Format(
            CultureInfo.InvariantCulture,
            """
                /// <summary>
                /// {0}
                /// </summary>
                """,
            summary).TrimEnd();
    }
}
