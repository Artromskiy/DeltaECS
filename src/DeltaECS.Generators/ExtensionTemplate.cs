namespace Delta.ECS.Generators;

/// <summary>Template for generated extension containers.</summary>
internal static partial class GeneratorTemplates
{
    internal static string ExtensionTemplate(
        string name,
        bool isInternal,
        string body)
    {
        string visibility = isInternal ? "internal" : "public";
        return RenderBlock($"{visibility} static class {name}", body);
    }
}
