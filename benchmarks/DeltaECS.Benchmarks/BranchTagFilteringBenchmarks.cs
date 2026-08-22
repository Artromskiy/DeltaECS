using System;
using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

public enum TagFilterDistribution
{
    Clustered,
    Random
}

/// <summary>
/// Exercises Delta's overlay-tag branch at several densities. Setup creates
/// the exact same number of tagged entities for clustered and deterministic
/// random layouts; no random work is performed in a measured method.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("DeltaOnlyFeatureLane", "TagFiltering")]
public class DeltaEcsTagFilteringBenchmarks
{
    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(0.10, 0.50, 0.90)]
    public double TaggedDensity { get; set; }

    [Params(TagFilterDistribution.Clustered, TagFilterDistribution.Random)]
    public TagFilterDistribution Distribution { get; set; }

    private World _world = null!;
    private Query _query;
    private ComponentId _valueComponent;
    private WriteRequest<TagFilterValue> _writeBinding;
    private ReadRequest<TagFilterValue> _readBinding;
    private TagId _tag;
    private int _expectedTaggedCount;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _valueComponent = layouts.Register<TagFilterValue>(new SchemaId(62_000));
        _tag = new TagId(62_001);
        _world = new World(layouts, initialEntityCapacity: Amount);

        var entities = new Entity[Amount];
        _world.CreateBatch(new[] { _valueComponent }, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            _world.SetComponent(entities[i], _valueComponent, new TagFilterValue { X = 1, Y = 2 });
        }

        var taggedCount = (int)(Amount * TaggedDensity);
        _expectedTaggedCount = taggedCount;
        var tagged = BuildTaggedIndices(Amount, taggedCount, Distribution);
        for (var i = 0; i < tagged.Length; i++)
        {
            _world.AddTag(entities[tagged[i]], _tag);
        }

        var spec = new QuerySpec(
            new[] { _valueComponent },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            new[] { _tag },
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        _query = _world.CreateQuery(in spec);
        _writeBinding = _query.Access<TagFilterValue>(_valueComponent, AccessMode.Write);
        _readBinding = _query.Access<TagFilterValue>(_valueComponent, AccessMode.Read);
    }

    /// <summary>Mask/query path plus a useful component update.</summary>
    [Benchmark]
    public int Delta_TagQueryAndIteration()
    {
        var state = new TagFilterState { UpdateValues = true, WriteValue = _writeBinding, ReadValue = _readBinding };
        _world.Query(in _query, ref state, static (ref TagFilterState s, ref QueryChunkCursor cursor)
            => IterateTagged(ref s, ref cursor));
        return TagFilterGuard.CountAndChecksum(state, _expectedTaggedCount);
    }

    /// <summary>Mask/query path with active-slot counting separated from payload work.</summary>
    [Benchmark]
    public int Delta_TagQueryMaskOnly()
    {
        var state = new TagFilterState { ReadValue = _readBinding };
        _world.Query(in _query, ref state, static (ref TagFilterState s, ref QueryChunkCursor cursor)
            => IterateTagged(ref s, ref cursor));
        return TagFilterGuard.CountAndChecksum(state, _expectedTaggedCount);
    }

    private static void IterateTagged(ref TagFilterState state, ref QueryChunkCursor lease)
    {
        if (state.UpdateValues)
        {
            var values = lease.Get(state.WriteValue);
            while (lease.MoveNext())
            {
                if (!lease.IsActiveSlot(lease.CurrentIndex)) continue;
                state.TaggedCount++;
                values[lease].X += values[lease].Y * 0.5f;
                state.Checksum += values[lease].X;
            }
        }
        else
        {
            var values = lease.Get(state.ReadValue);
            while (lease.MoveNext())
            {
                if (!lease.IsActiveSlot(lease.CurrentIndex)) continue;
                state.TaggedCount++;
                state.Checksum += values[lease].X;
            }
        }
    }

    private static int[] BuildTaggedIndices(int amount, int taggedCount, TagFilterDistribution distribution)
    {
        var indices = new int[taggedCount];
        if (distribution == TagFilterDistribution.Clustered)
        {
            for (var i = 0; i < taggedCount; i++)
            {
                indices[i] = i;
            }

            return indices;
        }

        // Deterministic partial Fisher-Yates shuffle. This runs only during setup.
        var candidates = new int[amount];
        for (var i = 0; i < candidates.Length; i++)
        {
            candidates[i] = i;
        }

        var random = 0x13579BDFu;
        for (var i = 0; i < taggedCount; i++)
        {
            random = unchecked(random * 1_664_525u + 1_013_904_223u);
            var offset = (int)(random % (uint)(amount - i));
            var selected = i + offset;
            (candidates[i], candidates[selected]) = (candidates[selected], candidates[i]);
            indices[i] = candidates[i];
        }

        return indices;
    }

    internal struct TagFilterValue
    {
        public float X;
        public float Y;
    }

    internal struct TagFilterState
    {
        public bool UpdateValues;
        public int TaggedCount;
        public double Checksum;
        public WriteRequest<TagFilterValue> WriteValue;
        public ReadRequest<TagFilterValue> ReadValue;
    }
}

internal static class TagFilterGuard
{
    public static int CountAndChecksum(DeltaEcsTagFilteringBenchmarks.TagFilterState state, int expectedCount)
    {
        if (state.TaggedCount != expectedCount)
        {
            throw new InvalidOperationException($"Tag-filter benchmark touched {state.TaggedCount} entities, expected {expectedCount}.");
        }

        if (double.IsNaN(state.Checksum) || double.IsInfinity(state.Checksum))
        {
            throw new InvalidOperationException("Tag-filter benchmark checksum is not finite.");
        }

        GC.KeepAlive(state.Checksum);
        return state.TaggedCount;
    }
}
