using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Consumer-side file model produced after semantic API analysis.</summary>
internal sealed record GeneratedFileModel(
    string Namespace,
    ImmutableArray<string> Usings,
    ImmutableArray<string> Members)
{
    public ImmutableArray<string> Usings { get; init; } = Usings
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static value => value, StringComparer.Ordinal)
        .ToImmutableArray();
}
