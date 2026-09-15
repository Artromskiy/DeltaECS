namespace Delta.ECS.Generators;

/// <summary>Template for a generated query invoker.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel InvokerTemplate(InvokerModel model)
        => new(model.Api, model.Source);
}
