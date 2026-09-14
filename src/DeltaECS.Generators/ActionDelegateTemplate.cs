namespace Delta.ECS.Generators;

/// <summary>Template for a generated callback delegate declaration.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel ActionDelegateTemplate(ApiModel api, string declaration)
        => new(api, declaration);
}
