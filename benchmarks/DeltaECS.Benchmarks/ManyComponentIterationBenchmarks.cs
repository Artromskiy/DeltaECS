using BenchmarkDotNet.Attributes;
using Delta.ECS;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.ManyComponents")]
// This is a wide-row control for the tiled-span experiment. The component
// shape is Movement4 + 16 + 4 + 4 + 4 = 32 int component rows. All backends
// are intentionally omitted: the experiment compares the same Delta path
// before and after an internal generated-loop change.
public class ManyComponentIterationBenchmarks
{
    public int Amount { get; set; } = BenchmarkConfiguration.Amount;

    private DeltaWorld _world = null!;
    private Query _query;
    private ComponentId[] _components = null!;
    private DeltaEntity[] _entities = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _components =
        [
            layouts.Register(typeof(Movement4A), new SchemaId(203_000)),
            layouts.Register(typeof(Movement4B), new SchemaId(203_001)),
            layouts.Register(typeof(Movement4C), new SchemaId(203_002)),
            layouts.Register(typeof(Movement4D), new SchemaId(203_003)),
            layouts.Register(typeof(Many16_0), new SchemaId(203_004)),
            layouts.Register(typeof(Many16_1), new SchemaId(203_005)),
            layouts.Register(typeof(Many16_2), new SchemaId(203_006)),
            layouts.Register(typeof(Many16_3), new SchemaId(203_007)),
            layouts.Register(typeof(Many16_4), new SchemaId(203_008)),
            layouts.Register(typeof(Many16_5), new SchemaId(203_009)),
            layouts.Register(typeof(Many16_6), new SchemaId(203_010)),
            layouts.Register(typeof(Many16_7), new SchemaId(203_011)),
            layouts.Register(typeof(Many16_8), new SchemaId(203_012)),
            layouts.Register(typeof(Many16_9), new SchemaId(203_013)),
            layouts.Register(typeof(Many16_10), new SchemaId(203_014)),
            layouts.Register(typeof(Many16_11), new SchemaId(203_015)),
            layouts.Register(typeof(Many16_12), new SchemaId(203_016)),
            layouts.Register(typeof(Many16_13), new SchemaId(203_017)),
            layouts.Register(typeof(Many16_14), new SchemaId(203_018)),
            layouts.Register(typeof(Many16_15), new SchemaId(203_019)),
            layouts.Register(typeof(Many4A_0), new SchemaId(203_020)),
            layouts.Register(typeof(Many4A_1), new SchemaId(203_021)),
            layouts.Register(typeof(Many4A_2), new SchemaId(203_022)),
            layouts.Register(typeof(Many4A_3), new SchemaId(203_023)),
            layouts.Register(typeof(Many4B_0), new SchemaId(203_024)),
            layouts.Register(typeof(Many4B_1), new SchemaId(203_025)),
            layouts.Register(typeof(Many4B_2), new SchemaId(203_026)),
            layouts.Register(typeof(Many4B_3), new SchemaId(203_027)),
            layouts.Register(typeof(Many4C_0), new SchemaId(203_028)),
            layouts.Register(typeof(Many4C_1), new SchemaId(203_029)),
            layouts.Register(typeof(Many4C_2), new SchemaId(203_030)),
            layouts.Register(typeof(Many4C_3), new SchemaId(203_031)),
        ];

        _world = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _entities = new DeltaEntity[Amount];
        _world.Create(_components, _entities);

        for (int index = 0; index < Amount; index++)
        {
            DeltaEntity entity = _entities[index];
            _world.Set(entity, _components[0], new Movement4A { Value = 1 });
            _world.Set(entity, _components[1], new Movement4B { Value = 2 });
            _world.Set(entity, _components[2], new Movement4C { Value = 3 });
            _world.Set(entity, _components[3], new Movement4D { Value = 4 });
        }

        var spec = QuerySpec.WhereAll(_components);
        _query = _world.CreateQuery(in spec);
    }

    [GlobalCleanup]
    public void Cleanup() => _world?.Dispose();

    [Benchmark(Baseline = true)]
    public int DeltaECS_ManyComponents()
    {
        var checksum = 0;
        _world.ForEach(
            in _query,
            ref checksum,
            static (
                ref int checksum,
                ref Movement4A a,
                ref Movement4B b,
                ref Movement4C c,
                ref readonly Movement4D d,
                ref readonly Many16_0 m160,
                ref readonly Many16_1 m161,
                ref readonly Many16_2 m162,
                ref readonly Many16_3 m163,
                ref readonly Many16_4 m164,
                ref readonly Many16_5 m165,
                ref readonly Many16_6 m166,
                ref readonly Many16_7 m167,
                ref readonly Many16_8 m168,
                ref readonly Many16_9 m169,
                ref readonly Many16_10 m1610,
                ref readonly Many16_11 m1611,
                ref readonly Many16_12 m1612,
                ref readonly Many16_13 m1613,
                ref readonly Many16_14 m1614,
                ref readonly Many16_15 m1615,
                ref readonly Many4A_0 a40,
                ref readonly Many4A_1 a41,
                ref readonly Many4A_2 a42,
                ref readonly Many4A_3 a43,
                ref readonly Many4B_0 b40,
                ref readonly Many4B_1 b41,
                ref readonly Many4B_2 b42,
                ref readonly Many4B_3 b43,
                ref readonly Many4C_0 c40,
                ref readonly Many4C_1 c41,
                ref readonly Many4C_2 c42,
                ref readonly Many4C_3 c43) =>
            {
                int updatedA = a.Value + d.Value;
                int updatedB = b.Value + d.Value;
                a.Value = updatedA;
                b.Value = updatedB;
                c.Value = (updatedA + updatedB) / 2;
                checksum += a.Value + b.Value + c.Value + d.Value
                    + m160.Value + m161.Value + m162.Value + m163.Value
                    + m164.Value + m165.Value + m166.Value + m167.Value
                    + m168.Value + m169.Value + m1610.Value + m1611.Value
                    + m1612.Value + m1613.Value + m1614.Value + m1615.Value
                    + a40.Value + a41.Value + a42.Value + a43.Value
                    + b40.Value + b41.Value + b42.Value + b43.Value
                    + c40.Value + c41.Value + c42.Value + c43.Value;
            });

        return checksum;
    }
}

internal struct Many16_0 { public int Value; }
internal struct Many16_1 { public int Value; }
internal struct Many16_2 { public int Value; }
internal struct Many16_3 { public int Value; }
internal struct Many16_4 { public int Value; }
internal struct Many16_5 { public int Value; }
internal struct Many16_6 { public int Value; }
internal struct Many16_7 { public int Value; }
internal struct Many16_8 { public int Value; }
internal struct Many16_9 { public int Value; }
internal struct Many16_10 { public int Value; }
internal struct Many16_11 { public int Value; }
internal struct Many16_12 { public int Value; }
internal struct Many16_13 { public int Value; }
internal struct Many16_14 { public int Value; }
internal struct Many16_15 { public int Value; }
internal struct Many4A_0 { public int Value; }
internal struct Many4A_1 { public int Value; }
internal struct Many4A_2 { public int Value; }
internal struct Many4A_3 { public int Value; }
internal struct Many4B_0 { public int Value; }
internal struct Many4B_1 { public int Value; }
internal struct Many4B_2 { public int Value; }
internal struct Many4B_3 { public int Value; }
internal struct Many4C_0 { public int Value; }
internal struct Many4C_1 { public int Value; }
internal struct Many4C_2 { public int Value; }
internal struct Many4C_3 { public int Value; }
