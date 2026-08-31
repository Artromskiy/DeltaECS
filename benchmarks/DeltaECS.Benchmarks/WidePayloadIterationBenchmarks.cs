using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BenchmarkCategory("Iteration.WidePayloadPartialRead")]
// The archetype contains eight wide rows, while the callback consumes only
// the first and last row. This isolates wide storage from the actual read set.
public class WidePayloadPartialReadIterationBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)]
    public int Amount { get; set; }

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
            layouts.Register(typeof(WidePayload0), new SchemaId(206_000)),
            layouts.Register(typeof(WidePayload1), new SchemaId(206_001)),
            layouts.Register(typeof(WidePayload2), new SchemaId(206_002)),
            layouts.Register(typeof(WidePayload3), new SchemaId(206_003)),
            layouts.Register(typeof(WidePayload4), new SchemaId(206_004)),
            layouts.Register(typeof(WidePayload5), new SchemaId(206_005)),
            layouts.Register(typeof(WidePayload6), new SchemaId(206_006)),
            layouts.Register(typeof(WidePayload7), new SchemaId(206_007)),
        ];

        _world = new DeltaWorld(layouts, initialEntityCapacity: Amount);
        _entities = new DeltaEntity[Amount];
        _world.Create(_components, _entities);

        for (int index = 0; index < Amount; index++)
        {
            DeltaEntity entity = _entities[index];
            _world.Set(entity, _components[0], new WidePayload0 { Value = 1 });
            _world.Set(entity, _components[7], new WidePayload7 { Value = 8 });
        }

        var spec = QuerySpec.WhereAll(_components);
        _query = _world.CreateQuery(in spec);
    }

    [GlobalCleanup]
    public void Cleanup() => _world?.Dispose();

    [Benchmark(Baseline = true)]
    public int DeltaECS_WidePayloadPartialRead()
    {
        var checksum = 0;
        _world.ForEach(
            in _query,
            ref checksum,
            static (
                ref int checksum,
                ref readonly WidePayload0 payload0,
                ref readonly WidePayload1 payload1,
                ref readonly WidePayload2 payload2,
                ref readonly WidePayload3 payload3,
                ref readonly WidePayload4 payload4,
                ref readonly WidePayload5 payload5,
                ref readonly WidePayload6 payload6,
                ref readonly WidePayload7 payload7) =>
            {
                _ = payload1;
                _ = payload2;
                _ = payload3;
                _ = payload4;
                _ = payload5;
                _ = payload6;
                checksum += payload0.Value + payload7.Value;
            });

        return checksum == Amount * 9
            ? checksum
            : throw new InvalidOperationException($"wide payload checksum mismatch: {checksum} != {Amount * 9}");
    }
}

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload0 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload1 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload2 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload3 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload4 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload5 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload6 { public int Value; }

[StructLayout(LayoutKind.Sequential, Size = 256)]
internal struct WidePayload7 { public int Value; }
