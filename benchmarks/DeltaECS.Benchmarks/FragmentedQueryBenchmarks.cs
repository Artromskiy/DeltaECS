using System;
using BenchmarkDotNet.Attributes;
using DVG.ECS;

namespace DVG.ECS.Benchmarks;

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
    [Params(8, 64, 256)]
    public int ArchetypeSignatures { get; set; }

    [Params(0.25, 0.50, 0.75)]
    public double MatchingDensity { get; set; }

    private World _world = null!;
    private QueryHandle _query;
    private ComponentId _required;
    private int _expectedMatches;

    [GlobalSetup]
    public void Setup()
    {
        if (ArchetypeSignatures is not (8 or 64 or 256))
        {
            throw new ArgumentOutOfRangeException(nameof(ArchetypeSignatures));
        }

        var layouts = new ComponentLayoutRegistry();
        var components = new ComponentId[10];
        for (var i = 0; i < components.Length; i++)
        {
            components[i] = layouts.Register<FragmentValue>(new SchemaId((ulong)(61_000 + i)));
        }

        _required = components[0];
        _expectedMatches = (int)(ArchetypeSignatures * MatchingDensity);
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

        var description = QueryDescription.ForComponents(_required);
        _query = _world.CreateQuery(in description);
    }

    [Benchmark]
    public int DeltaOnly_QueryAndIteration()
    {
        var state = new FragmentQueryState();
        _world.Query(in _query, QueryAccess.Read, ref state, static (ref FragmentQueryState s, ref DenseChunkLeaseView lease) =>
        {
            var values = lease.GetComponentRow<FragmentValue>(0);
            for (var slotIndex = 0; slotIndex < lease.SlotCount; slotIndex++)
            {
                if (!lease.IsAllSlotsActive && !lease.IsActiveSlot(slotIndex))
                {
                    continue;
                }

                s.Matches++;
                s.Checksum += values[slotIndex].Value;
            }
        });

        if (state.Matches != _expectedMatches)
        {
            throw new InvalidOperationException($"Fragmented query matched {state.Matches}, expected {_expectedMatches}.");
        }

        return state.Checksum;
    }

    [Benchmark]
    public int DeltaOnly_QueryChunkDispatch()
    {
        var state = new FragmentQueryState();
        _world.Query(in _query, QueryAccess.Read, ref state, static (ref FragmentQueryState s, ref DenseChunkLeaseView lease)
            => s.Matches += lease.SlotCount);

        if (state.Matches != _expectedMatches)
        {
            throw new InvalidOperationException($"Fragmented query dispatched {state.Matches}, expected {_expectedMatches}.");
        }

        return state.Matches;
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
    }
}
