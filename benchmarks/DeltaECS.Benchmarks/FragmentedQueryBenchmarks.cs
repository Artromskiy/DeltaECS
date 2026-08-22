using System;
using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

/// <summary>
/// Delta-only feature lane: deliberately stresses the cached query's
/// archetype-signature matching with many small, fragmented archetypes.
/// Arch/Friflo are not included because their public APIs do not expose the
/// same deterministic arbitrary-signature construction and comparison here.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("DeltaOnlyFeatureLane", "FragmentedQuery")]
public class DeltaOnlyFragmentedQueryBenchmarks
{
    [Params(8, 64, 256, 1024)]
    public int ArchetypeSignatures { get; set; }

    [Params(1, 10, 50, 100)]
    public int MatchingPercent { get; set; }

    private World _world = null!;
    private Query _query;
    private ComponentId _required;
    private ReadRequest<FragmentValue> _valueBinding;
    private int _expectedMatches;

    [GlobalSetup]
    public void Setup()
    {
        if (ArchetypeSignatures is not (8 or 64 or 256 or 1024))
        {
            throw new ArgumentOutOfRangeException(nameof(ArchetypeSignatures));
        }

        var layouts = new ComponentLayoutRegistry();
        // One required component plus eleven independent bits gives 2,048
        // distinct masks, so the 1,024-signature case remains genuinely
        // fragmented instead of silently reusing signatures.
        var components = new ComponentId[12];
        for (var i = 0; i < components.Length; i++)
        {
            components[i] = layouts.Register<FragmentValue>(new SchemaId((ulong)(61_000 + i)));
        }

        _required = components[0];
        if (MatchingPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MatchingPercent));
        }

        _expectedMatches = (int)Math.Round(ArchetypeSignatures * (MatchingPercent / 100d), MidpointRounding.AwayFromZero);
        _world = new World(layouts, initialEntityCapacity: ArchetypeSignatures, chunkCapacity: 1);

        // Every signature has one entity and a unique deterministic mask. The
        // required component is present in exactly the requested prefix.
        for (var signatureIndex = 0; signatureIndex < ArchetypeSignatures; signatureIndex++)
        {
            var rest = signatureIndex + 1;
            var includeRequired = signatureIndex < _expectedMatches;
            var mask = includeRequired ? (rest << 1) | 1 : rest << 1;
            var signature = BuildSignature(components, mask);
            var entity = _world.Create(signature);
            for (var componentIndex = 0; componentIndex < signature.Length; componentIndex++)
            {
                _world.SetComponent(entity, signature[componentIndex], new FragmentValue { Value = signatureIndex + 1 });
            }
        }

        var spec = QuerySpec.ForComponents(_required);
        _query = _world.CreateQuery(in spec);
        _valueBinding = _query.Access<FragmentValue>(_required, AccessMode.Read);
    }

    [Benchmark]
    public int DeltaOnly_QueryAndIteration()
    {
        var state = new FragmentQueryState { Value = _valueBinding };
        _world.Query(in _query, ref state, static (ref FragmentQueryState s, ref QueryChunkCursor cursor) =>
        {
            var values = cursor.Get<FragmentValue>(s.Value);
            while (cursor.MoveNext())
            {
                s.Matches++;
                s.Checksum += values[cursor].Value;
            }
        });

        if (state.Matches != _expectedMatches)
        {
            throw new InvalidOperationException($"Fragmented query matched {state.Matches}, expected {_expectedMatches} ({MatchingPercent}% of {ArchetypeSignatures} signatures).");
        }

        return state.Checksum;
    }

    [Benchmark]
    public int DeltaOnly_QueryChunkDispatch()
    {
        var state = new FragmentQueryState { Value = _valueBinding };
        _world.Query(in _query, ref state, static (ref FragmentQueryState s, ref QueryChunkCursor cursor) =>
        { while (cursor.MoveNext()) s.Matches++; });

        if (state.Matches != _expectedMatches)
        {
            throw new InvalidOperationException($"Fragmented query dispatched {state.Matches}, expected {_expectedMatches} ({MatchingPercent}% of {ArchetypeSignatures} signatures).");
        }

        return state.Matches;
    }

    [Benchmark]
    public int DeltaOnly_ColdPlan()
    {
        var state = new FragmentQueryState();
        var spec = QuerySpec.ForComponents(_required);
        var coldQuery = _world.CreateQuery(in spec);
        var valueBinding = coldQuery.Access<FragmentValue>(_required, AccessMode.Read);
        state.Value = valueBinding;
        _world.Query(in coldQuery, ref state, static (ref FragmentQueryState s, ref QueryChunkCursor cursor) =>
        {
            var values = cursor.Get<FragmentValue>(s.Value);
            while (cursor.MoveNext())
            {
                s.Matches++;
                s.Checksum += values[cursor].Value;
            }
        });

        if (state.Matches != _expectedMatches)
        {
            throw new InvalidOperationException($"Fragmented cold query matched {state.Matches}, expected {_expectedMatches} ({MatchingPercent}% of {ArchetypeSignatures} signatures).");
        }

        return state.Checksum;
    }

    private static ComponentId[] BuildSignature(ComponentId[] components, int mask)
    {
        var count = 0;
        for (var bit = 0; bit < components.Length; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                count++;
            }
        }

        var signature = new ComponentId[count];
        var index = 0;
        for (var bit = 0; bit < components.Length; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                signature[index++] = components[bit];
            }
        }

        return signature;
    }

    private struct FragmentValue
    {
        public int Value;
    }

    private struct FragmentQueryState
    {
        public int Matches;
        public int Checksum;
        public ReadRequest<FragmentValue> Value;
    }
}
