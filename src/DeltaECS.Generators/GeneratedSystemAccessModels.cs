using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

/// <summary>Semantic access facts collected from one <c>ISystem</c>.</summary>
internal sealed class GeneratedSystemAccessModel
{
    internal GeneratedSystemAccessModel(
        string @namespace,
        string typeName,
        string helperName,
        bool injectProperty,
        ImmutableArray<string> reads,
        ImmutableArray<string> writes,
        ImmutableArray<string> stampReads,
        ImmutableArray<string> adds,
        ImmutableArray<string> removes,
        bool readsTopology,
        bool writesTopology,
        bool createsEntities,
        bool destroysEntities,
        bool unknownWorldAccess,
        bool usesParallelExecutor)
    {
        Namespace = @namespace;
        TypeName = typeName;
        HelperName = helperName;
        InjectProperty = injectProperty;
        Reads = reads;
        Writes = writes;
        StampReads = stampReads;
        Adds = adds;
        Removes = removes;
        ReadsTopology = readsTopology;
        WritesTopology = writesTopology;
        CreatesEntities = createsEntities;
        DestroysEntities = destroysEntities;
        UnknownWorldAccess = unknownWorldAccess;
        UsesParallelExecutor = usesParallelExecutor;
    }

    internal string Namespace { get; }
    internal string TypeName { get; }
    internal string HelperName { get; }
    internal bool InjectProperty { get; }
    internal ImmutableArray<string> Reads { get; }
    internal ImmutableArray<string> Writes { get; }
    internal ImmutableArray<string> StampReads { get; }
    internal ImmutableArray<string> Adds { get; }
    internal ImmutableArray<string> Removes { get; }
    internal bool ReadsTopology { get; }
    internal bool WritesTopology { get; }
    internal bool CreatesEntities { get; }
    internal bool DestroysEntities { get; }
    internal bool UnknownWorldAccess { get; }
    internal bool UsesParallelExecutor { get; }
}

internal sealed class GeneratedSystemAccessAccumulator
{
    private readonly SortedSet<string> _reads = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _writes = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _stampReads = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _adds = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _removes = new(StringComparer.Ordinal);

    internal bool ReadsTopology { get; private set; }
    internal bool WritesTopology { get; private set; }
    internal bool CreatesEntities { get; private set; }
    internal bool DestroysEntities { get; private set; }
    internal bool UnknownWorldAccess { get; private set; }
    internal bool UsesParallelExecutor { get; private set; }

    internal void Read(ITypeSymbol? type)
        => Add(_reads, type);

    internal void Write(ITypeSymbol? type)
        => Add(_writes, type);

    internal void StampRead(ITypeSymbol? type)
        => Add(_stampReads, type);

    internal void Add(ITypeSymbol? type)
        => Add(_adds, type);

    internal void Remove(ITypeSymbol? type)
        => Add(_removes, type);

    internal void ReadTopology()
        => ReadsTopology = true;

    internal void WriteTopology()
        => WritesTopology = true;

    internal void CreateEntities()
        => CreatesEntities = true;

    internal void DestroyEntities()
        => DestroysEntities = true;

    internal void Unknown()
        => UnknownWorldAccess = true;

    internal void Parallel()
        => UsesParallelExecutor = true;

    internal GeneratedSystemAccessModel Build(
        string @namespace,
        string typeName,
        string helperName,
        bool injectProperty)
        => new(
            @namespace,
            typeName,
            helperName,
            injectProperty,
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
