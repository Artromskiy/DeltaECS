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
    private CursorWriteBinding<S0> _b0; private CursorWriteBinding<S1> _b1; private CursorWriteBinding<S2> _b2; private CursorWriteBinding<S3> _b3;
    private CursorWriteBinding<S4> _b4; private CursorWriteBinding<S5> _b5; private CursorWriteBinding<S6> _b6; private CursorWriteBinding<S7> _b7;
    private LegacyByteDenseReference _legacy = null!;

    private struct DenseState
    {
        public CursorWriteBinding<S0> B0;
        public CursorWriteBinding<S1> B1;
        public CursorWriteBinding<S2> B2;
        public CursorWriteBinding<S3> B3;
        public CursorWriteBinding<S4> B4;
        public CursorWriteBinding<S5> B5;
        public CursorWriteBinding<S6> B6;
        public CursorWriteBinding<S7> B7;
    }

    private static readonly QueryCursorAction<DenseState> s_iterate = Iterate;

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
        _b0 = _query.CursorBind<S0>(_components[0], RowAccess.Write); _b1 = _query.CursorBind<S1>(_components[1], RowAccess.Write);
        _b2 = _query.CursorBind<S2>(_components[2], RowAccess.Write); _b3 = _query.CursorBind<S3>(_components[3], RowAccess.Write);
        _b4 = _query.CursorBind<S4>(_components[4], RowAccess.Write); _b5 = _query.CursorBind<S5>(_components[5], RowAccess.Write);
        _b6 = _query.CursorBind<S6>(_components[6], RowAccess.Write); _b7 = _query.CursorBind<S7>(_components[7], RowAccess.Write);
        _legacy = new LegacyByteDenseReference(8, Amount, ChunkCapacity);
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array()
    {
        var state = new DenseState
        {
            B0 = _b0,
            B1 = _b1,
            B2 = _b2,
            B3 = _b3,
            B4 = _b4,
            B5 = _b5,
            B6 = _b6,
            B7 = _b7
        };
        _arrayWorld.QueryCursor(in _query, ref state, s_iterate);
    }

    private static void Iterate(ref DenseState state, ref DenseChunkCursor cursor)
    {
        var c0 = cursor.Resolve(state.B0);
        var c1 = cursor.Resolve(state.B1);
        var c2 = cursor.Resolve(state.B2);
        var c3 = cursor.Resolve(state.B3);
        var c4 = cursor.Resolve(state.B4);
        var c5 = cursor.Resolve(state.B5);
        var c6 = cursor.Resolve(state.B6);
        var c7 = cursor.Resolve(state.B7);
        while (cursor.MoveNext())
        {
            c0[cursor].X += c0[cursor].Y; c1[cursor].X += c1[cursor].Y; c2[cursor].X += c2[cursor].Y; c3[cursor].X += c3[cursor].Y;
            c4[cursor].X += c4[cursor].Y; c5[cursor].X += c5[cursor].Y; c6[cursor].X += c6[cursor].Y; c7[cursor].X += c7[cursor].Y;
        }
    }

    [Benchmark]
    public void LegacyByte() => _legacy.Iterate();
}
