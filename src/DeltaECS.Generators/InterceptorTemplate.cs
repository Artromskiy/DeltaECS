namespace Delta.ECS.Generators;

/// <summary>Template for an intercepted generated call.</summary>
internal static partial class GeneratorTemplates
{
    internal static string InterceptorTemplate(string header, string body, string footer)
        => GeneratedSourceFormatter.Format($"{header}{body}{footer}");
}
