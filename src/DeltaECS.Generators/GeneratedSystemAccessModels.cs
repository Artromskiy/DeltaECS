using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

/// <summary>Semantic access facts collected from one <c>ISystem</c>.</summary>
internal sealed record GeneratedSystemAccessModel(
    string Namespace,
    string TypeName,
    string HelperName,
    bool InjectProperty,
    ImmutableArray<GeneratedSystemQueryAccessModel> QueryAccesses,
    ImmutableArray<string> Reads,
    ImmutableArray<string> Writes,
    ImmutableArray<string> StampReads,
    ImmutableArray<string> Adds,
    ImmutableArray<string> Removes,
    bool ReadsTopology,
    bool WritesTopology,
    bool CreatesEntities,
    bool DestroysEntities,
    bool UnknownWorldAccess,
    bool UsesParallelExecutor);

/// <summary>Generated component access tied to a query field on a system.</summary>
internal sealed record GeneratedSystemQueryAccessModel(
    string QueryExpression,
    ImmutableArray<string> Reads,
    ImmutableArray<string> Writes,
    ImmutableArray<string> StampReads);

internal sealed class GeneratedSystemAccessAccumulator
{
    private readonly SortedSet<string> _reads = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _writes = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _stampReads = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _adds = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _removes = new(StringComparer.Ordinal);
    private readonly List<GeneratedSystemQueryAccessModel> _queryAccesses = new();

    internal bool ReadsTopology { get; private set; }
    internal bool WritesTopology { get; private set; }
    internal bool CreatesEntities { get; private set; }
    internal bool DestroysEntities { get; private set; }
    internal bool UnknownWorldAccess { get; private set; }
    internal bool UsesParallelExecutor { get; private set; }

    internal void Read(ITypeSymbol? type) => Add(_reads, type);

    internal void Write(ITypeSymbol? type) => Add(_writes, type);

    internal void StampRead(ITypeSymbol? type) => Add(_stampReads, type);

    internal void Add(ITypeSymbol? type) => Add(_adds, type);

    internal void Remove(ITypeSymbol? type) => Add(_removes, type);

    internal void Apply(IEnumerable<ITypeSymbol> types, Action<ITypeSymbol> access)
    {
        foreach (ITypeSymbol type in types)
        {
            access(type);
        }
    }

    internal void ReadTopology() => ReadsTopology = true;

    internal void WriteTopology() => WritesTopology = true;

    internal void CreateEntities() => CreatesEntities = true;

    internal void DestroyEntities() => DestroysEntities = true;

    internal void Unknown() => UnknownWorldAccess = true;

    internal void Parallel() => UsesParallelExecutor = true;

    internal void AddQueryAccess(string queryExpression, GeneratedSystemAccessAccumulator access)
    {
        if (access.UnknownWorldAccess)
        {
            UnknownWorldAccess = true;
            return;
        }

        if (access._reads.Count == 0 && access._writes.Count == 0 && access._stampReads.Count == 0)
        {
            return;
        }

        _queryAccesses.Add(new GeneratedSystemQueryAccessModel(
            queryExpression,
            access._reads.ToImmutableArray(),
            access._writes.ToImmutableArray(),
            access._stampReads.ToImmutableArray()));
    }

    internal GeneratedSystemAccessModel Build(string @namespace, string typeName, string helperName, bool injectProperty)
        => new(
            @namespace,
            typeName,
            helperName,
            injectProperty,
            _queryAccesses.ToImmutableArray(),
            _reads.ToImmutableArray(),
            _writes.ToImmutableArray(),
            _stampReads.ToImmutableArray(),
            _adds.ToImmutableArray(),
            _removes.ToImmutableArray(),
            ReadsTopology,
            WritesTopology,
            CreatesEntities,
            DestroysEntities,
            UnknownWorldAccess,
            UsesParallelExecutor);

    private void Add(SortedSet<string> values, ITypeSymbol? type)
    {
        if (type is null || type.TypeKind == TypeKind.Error)
        {
            UnknownWorldAccess = true;
            return;
        }

        if (GeneratorSupport.IsEntityType(type)
            || GeneratorSupport.IsStampType(type)
            || GeneratorSupport.IsEcsType(type, "ComponentId")
            )
        {
            return;
        }

        if (!GeneratorSupport.IsAccessibleType(type))
        {
            UnknownWorldAccess = true;
            return;
        }

        values.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }
}
