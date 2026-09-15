namespace Delta.ECS.Generators;

/// <summary>Template for generated extension containers.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel ExtensionTemplate(
        ApiModel api,
        string name,
        bool isInternal,
        string body)
    {
        string visibility = isInternal ? "internal" : "public";
        return new RenderModel(
            api,
            RenderBlock($"{visibility} static class {name}", body));
    }
}
