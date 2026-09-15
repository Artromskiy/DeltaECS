namespace Delta.ECS.Generators;

/// <summary>Template for a generated Where view.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel WhereViewTemplate(
        ApiModel api,
        string declaration,
        string body)
        => new(api, RenderBlock(declaration, body));
}
