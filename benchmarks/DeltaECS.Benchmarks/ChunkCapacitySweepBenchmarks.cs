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

    internal struct S0 { public float X; public float Y; }
    internal struct S1 { public float X; public float Y; }
    internal struct S2 { public float X; public float Y; }
    internal struct S3 { public float X; public float Y; }
    internal struct S4 { public float X; public float Y; }
    internal struct S5 { public float X; public float Y; }
    internal struct S6 { public float X; public float Y; }
    internal struct S7 { public float X; public float Y; }

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
        _arrayWorld.Create(_components, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            _arrayWorld.Set(entities[i], _components[0], new S0 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[1], new S1 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[2], new S2 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[3], new S3 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[4], new S4 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[5], new S5 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[6], new S6 { X = 1, Y = 2 });
            _arrayWorld.Set(entities[i], _components[7], new S7 { X = 1, Y = 2 });
        }

        var spec = QuerySpec.WhereAll(_components);
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
        _arrayWorld.ForEach(
            in _query,
            static (ref S0 c0, ref S1 c1, ref S2 c2, ref S3 c3,
                ref S4 c4, ref S5 c5, ref S6 c6, ref S7 c7) =>
            {
                c0.X += c0.Y; c1.X += c1.Y; c2.X += c2.Y; c3.X += c3.Y;
                c4.X += c4.Y; c5.X += c5.Y; c6.X += c6.Y; c7.X += c7.Y;
            });
    }

    [Benchmark]
    public void LegacyByte() => _legacy.Iterate();
}
