namespace Delta.ECS.Generators;

/// <summary>Template for generated extension containers.</summary>
internal static partial class GeneratorTemplates
{
    internal static string ExtensionTemplate(string name, bool isInternal, string body)
    {
        string visibility = isInternal ? "internal" : "public";
        return RenderBlock($"/// <summary>Generated extension methods for the APIs used by this compilation.</summary>\n{visibility} static class {name}", body);
    }
}
