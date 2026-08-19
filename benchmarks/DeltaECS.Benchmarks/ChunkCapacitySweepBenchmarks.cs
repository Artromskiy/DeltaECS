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
    private QueryHandle _query;
    private ComponentId[] _components = Array.Empty<ComponentId>();
    private WriteRowBinding<S0> _b0; private WriteRowBinding<S1> _b1; private WriteRowBinding<S2> _b2; private WriteRowBinding<S3> _b3;
    private WriteRowBinding<S4> _b4; private WriteRowBinding<S5> _b5; private WriteRowBinding<S6> _b6; private WriteRowBinding<S7> _b7;
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
        _b0 = _query.Bind<S0>(_components[0], RowAccess.Write); _b1 = _query.Bind<S1>(_components[1], RowAccess.Write);
        _b2 = _query.Bind<S2>(_components[2], RowAccess.Write); _b3 = _query.Bind<S3>(_components[3], RowAccess.Write);
        _b4 = _query.Bind<S4>(_components[4], RowAccess.Write); _b5 = _query.Bind<S5>(_components[5], RowAccess.Write);
        _b6 = _query.Bind<S6>(_components[6], RowAccess.Write); _b7 = _query.Bind<S7>(_components[7], RowAccess.Write);
        _legacy = new LegacyByteDenseReference(8, Amount, ChunkCapacity);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array()
    {
        using var chunks = _arrayWorld.QueryChunks(in _query, QueryAccess.Write);
        while (chunks.MoveNext())
        {
            var lease = chunks.Current;
            var c0 = lease.GetRow(_b0); var c1 = lease.GetRow(_b1);
            var c2 = lease.GetRow(_b2); var c3 = lease.GetRow(_b3);
            var c4 = lease.GetRow(_b4); var c5 = lease.GetRow(_b5);
            var c6 = lease.GetRow(_b6); var c7 = lease.GetRow(_b7);
            for (var i = c0.Length - 1; i >= 0; i--)
            {
                c0[i].X += c0[i].Y; c1[i].X += c1[i].Y; c2[i].X += c2[i].Y; c3[i].X += c3[i].Y;
                c4[i].X += c4[i].Y; c5[i].X += c5[i].Y; c6[i].X += c6[i].Y; c7[i].X += c7[i].Y;
            }
        }
    }

    [Benchmark]
    public void LegacyByte() => _legacy.Iterate();
}
