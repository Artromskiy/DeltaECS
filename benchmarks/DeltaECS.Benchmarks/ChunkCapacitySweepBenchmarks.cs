using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class DenseCapacitySweepBenchmarks
{
    [Params(1024, 2048, 4096, 8192)]
    public int ChunkCapacity { get; set; }

    private const int Amount = 100_000;
    private World _arrayWorld = null!;
    private Query _query;
    private ComponentId[] _components = Array.Empty<ComponentId>();
    private WriteAccess _b0; private WriteAccess _b1; private WriteAccess _b2; private WriteAccess _b3;
    private WriteAccess _b4; private WriteAccess _b5; private WriteAccess _b6; private WriteAccess _b7;
    private LegacyByteDenseReference _legacy = null!;

    private struct S0 { public float X; public float Y; }
    private struct S1 { public float X; public float Y; }
    private struct S2 { public float X; public float Y; }
    private struct S3 { public float X; public float Y; }
    private struct S4 { public float X; public float Y; }
    private struct S5 { public float X; public float Y; }
    private struct S6 { public float X; public float Y; }
    private struct S7 { public float X; public float Y; }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _components = new[]
        {
            layouts.Register(typeof(S0), new SchemaId(50_000)), layouts.Register(typeof(S1), new SchemaId(50_001)),
            layouts.Register(typeof(S2), new SchemaId(50_002)), layouts.Register(typeof(S3), new SchemaId(50_003)),
            layouts.Register(typeof(S4), new SchemaId(50_004)), layouts.Register(typeof(S5), new SchemaId(50_005)),
            layouts.Register(typeof(S6), new SchemaId(50_006)), layouts.Register(typeof(S7), new SchemaId(50_007))
        };

        _arrayWorld = new World(layouts, initialEntityCapacity: Amount, chunkCapacity: ChunkCapacity);
        var entities = new Entity[Amount];
        _arrayWorld.CreateBatch(_components, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            _arrayWorld.SetComponent(entities[i], _components[0], new S0 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[1], new S1 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[2], new S2 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[3], new S3 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[4], new S4 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[5], new S5 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[6], new S6 { X = 1, Y = 2 });
            _arrayWorld.SetComponent(entities[i], _components[7], new S7 { X = 1, Y = 2 });
        }

        var spec = QuerySpec.ForComponents(_components);
        _query = _arrayWorld.CreateQuery(in spec);
        _b0 = _query.AccessWrite(_components[0]); _b1 = _query.AccessWrite(_components[1]);
        _b2 = _query.AccessWrite(_components[2]); _b3 = _query.AccessWrite(_components[3]);
        _b4 = _query.AccessWrite(_components[4]); _b5 = _query.AccessWrite(_components[5]);
        _b6 = _query.AccessWrite(_components[6]); _b7 = _query.AccessWrite(_components[7]);
        _legacy = new LegacyByteDenseReference(8, Amount, ChunkCapacity);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array()
    {
        using var scope = _arrayWorld.OpenQuery(in _query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var c0 = slots.Get(_b0);
                var c1 = slots.Get(_b1);
                var c2 = slots.Get(_b2);
                var c3 = slots.Get(_b3);
                var c4 = slots.Get(_b4);
                var c5 = slots.Get(_b5);
                var c6 = slots.Get(_b6);
                var c7 = slots.Get(_b7);
                while (slots.MoveNext())
                {
                    c0.Ref<S0>(slots).X += c0.Ref<S0>(slots).Y; c1.Ref<S1>(slots).X += c1.Ref<S1>(slots).Y; c2.Ref<S2>(slots).X += c2.Ref<S2>(slots).Y; c3.Ref<S3>(slots).X += c3.Ref<S3>(slots).Y;
                    c4.Ref<S4>(slots).X += c4.Ref<S4>(slots).Y; c5.Ref<S5>(slots).X += c5.Ref<S5>(slots).Y; c6.Ref<S6>(slots).X += c6.Ref<S6>(slots).Y; c7.Ref<S7>(slots).X += c7.Ref<S7>(slots).Y;
                }
            }
        }
    }

    [Benchmark]
    public void LegacyByte() => _legacy.Iterate();
}
