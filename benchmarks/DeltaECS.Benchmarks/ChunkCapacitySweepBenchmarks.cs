using BenchmarkDotNet.Attributes;
using DVG.ECS;

namespace DVG.ECS.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class DenseCapacitySweepBenchmarks
{
    [Params(1024, 2048, 4096, 8192)]
    public int ChunkCapacity { get; set; }

    private const int Amount = 100_000;
    private World _arrayWorld = null!;
    private QueryHandle _query;
    private ComponentId[] _components = Array.Empty<ComponentId>();
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
            layouts.Register<S0>(new SchemaId(50_000)), layouts.Register<S1>(new SchemaId(50_001)),
            layouts.Register<S2>(new SchemaId(50_002)), layouts.Register<S3>(new SchemaId(50_003)),
            layouts.Register<S4>(new SchemaId(50_004)), layouts.Register<S5>(new SchemaId(50_005)),
            layouts.Register<S6>(new SchemaId(50_006)), layouts.Register<S7>(new SchemaId(50_007))
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

        var description = QueryDescription.ForComponents(_components);
        _query = _arrayWorld.CreateQuery(in description);
        _legacy = new LegacyByteDenseReference(8, Amount, ChunkCapacity);
    }

    [Benchmark]
    public void DeltaECS_Array()
    {
        using var chunks = _arrayWorld.QueryChunks(in _query, QueryAccess.Write);
        while (chunks.MoveNext())
        {
            var lease = chunks.Current;
            var c0 = lease.GetComponentRow<S0>(0); var c1 = lease.GetComponentRow<S1>(1);
            var c2 = lease.GetComponentRow<S2>(2); var c3 = lease.GetComponentRow<S3>(3);
            var c4 = lease.GetComponentRow<S4>(4); var c5 = lease.GetComponentRow<S5>(5);
            var c6 = lease.GetComponentRow<S6>(6); var c7 = lease.GetComponentRow<S7>(7);
            for (var i = 0; i < c0.Length; i++)
            {
                c0[i].X += c0[i].Y; c1[i].X += c1[i].Y; c2[i].X += c2[i].Y; c3[i].X += c3[i].Y;
                c4[i].X += c4[i].Y; c5[i].X += c5[i].Y; c6[i].X += c6[i].Y; c7[i].X += c7[i].Y;
            }
        }
    }

    [Benchmark(Baseline = true)]
    public void LegacyByte() => _legacy.Iterate();
}
