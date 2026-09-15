using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Consumer-side file model produced after semantic API analysis.</summary>
internal sealed class GeneratedFileModel
{
    internal GeneratedFileModel(
        string namespaceName,
        ImmutableArray<string> usings,
        ImmutableArray<string> members)
    {
        Namespace = namespaceName;
        Usings = usings
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        Members = members;
    }

    internal string Namespace { get; }
    internal ImmutableArray<string> Usings { get; }
    internal ImmutableArray<string> Members { get; }
}
