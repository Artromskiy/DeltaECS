using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Friflo.Engine.ECS;
using DeltaEntity = Delta.ECS.Entity;
using ArchComponentType = Arch.Core.Utils.ComponentType;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class DistinctDenseComparisonBenchmarks
{
    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(1, 2, 4, 8)]
    public int ComponentCount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteAccess _d0Binding; private WriteAccess _d1Binding; private WriteAccess _d2Binding; private WriteAccess _d3Binding;
    private WriteAccess _d4Binding; private WriteAccess _d5Binding; private WriteAccess _d6Binding; private WriteAccess _d7Binding;
    private DeltaEntity[] _deltaEntities = Array.Empty<DeltaEntity>();
    private LegacyByteDenseReference _legacy = null!;
    private Arch.Core.World _archWorld = null!;
    private Arch.Core.QueryDescription _archQuery;
    private ArchComponentType[] _archComponents = Array.Empty<ArchComponentType>();
    private EntityStore _frifloWorld = null!;
    private ArchetypeQuery<F0> _frifloQ1 = null!;
    private ArchetypeQuery<F0, F1> _frifloQ2 = null!;
    private ArchetypeQuery<F0, F1, F2, F3> _frifloQ4 = null!;
    private ArchetypeQuery<F0, F1, F2, F3, F4> _frifloQ8 = null!;

    private static readonly ArchComponentType[] s_archTypes =
    {
        typeof(A0), typeof(A1), typeof(A2), typeof(A3),
        typeof(A4), typeof(A5), typeof(A6), typeof(A7)
    };

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaComponents = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _deltaComponents[i] = i switch
            {
                0 => layouts.Register(typeof(D0), new SchemaId(40_000)),
                1 => layouts.Register(typeof(D1), new SchemaId(40_001)),
                2 => layouts.Register(typeof(D2), new SchemaId(40_002)),
                3 => layouts.Register(typeof(D3), new SchemaId(40_003)),
                4 => layouts.Register(typeof(D4), new SchemaId(40_004)),
                5 => layouts.Register(typeof(D5), new SchemaId(40_005)),
                6 => layouts.Register(typeof(D6), new SchemaId(40_006)),
                _ => layouts.Register(typeof(D7), new SchemaId(40_007))
            };
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        _deltaEntities = new DeltaEntity[Amount];
        _deltaWorld.Create(_deltaComponents, _deltaEntities);
        for (var i = 0; i < _deltaEntities.Length; i++)
        {
            SetDeltaValues(_deltaEntities[i]);
        }

        var queryDescription = QuerySpec.WhereAll(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in queryDescription);
        _d0Binding = _deltaQuery.AccessWrite(_deltaComponents[0]);
        _d1Binding = ComponentCount >= 2 ? _deltaQuery.AccessWrite(_deltaComponents[1]) : default;
        _d2Binding = ComponentCount >= 4 ? _deltaQuery.AccessWrite(_deltaComponents[2]) : default;
        _d3Binding = ComponentCount >= 4 ? _deltaQuery.AccessWrite(_deltaComponents[3]) : default;
        _d4Binding = ComponentCount >= 8 ? _deltaQuery.AccessWrite(_deltaComponents[4]) : default;
        _d5Binding = ComponentCount >= 8 ? _deltaQuery.AccessWrite(_deltaComponents[5]) : default;
        _d6Binding = ComponentCount >= 8 ? _deltaQuery.AccessWrite(_deltaComponents[6]) : default;
        _d7Binding = ComponentCount >= 8 ? _deltaQuery.AccessWrite(_deltaComponents[7]) : default;
        _legacy = new LegacyByteDenseReference(ComponentCount, Amount);

        _archWorld = Arch.Core.World.Create();
        _archComponents = new ArchComponentType[ComponentCount];
        Array.Copy(s_archTypes, _archComponents, ComponentCount);
        _archQuery = new Arch.Core.QueryDescription { All = _archComponents };
        _archWorld.Reserve(_archComponents, Amount);
        for (var i = 0; i < Amount; i++)
        {
            var entity = _archWorld.Create(_archComponents);
            SetArchValues(entity);
        }

        _frifloWorld = new EntityStore();
        for (var i = 0; i < Amount; i++)
        {
            CreateFrifloEntity();
        }

        _frifloQ1 = _frifloWorld.Query<F0>();
        _frifloQ2 = _frifloWorld.Query<F0, F1>();
        _frifloQ4 = _frifloWorld.Query<F0, F1, F2, F3>();
        _frifloQ8 = _frifloWorld.Query<F0, F1, F2, F3, F4>();
    }

    [Benchmark(Baseline = true)]
    public void DeltaECS_Array_DistinctTypes()
    {
        switch (ComponentCount)
        {
            case 1:
                _deltaWorld.ForEach(in _deltaQuery, (ForEachAction<D0>)(static (ref D0 c0) => c0.X += c0.Y));
                break;
            case 2:
                _deltaWorld.ForEach(in _deltaQuery, (ForEachAction<D0, D1>)(static (ref D0 c0, ref D1 c1) =>
                {
                    c0.X += c0.Y;
                    c1.X += c1.Y;
                }));
                break;
            case 4:
                _deltaWorld.ForEach(in _deltaQuery, (ForEachAction<D0, D1, D2, D3>)(static (ref D0 c0, ref D1 c1, ref D2 c2, ref D3 c3) =>
                {
                    c0.X += c0.Y;
                    c1.X += c1.Y;
                    c2.X += c2.Y;
                    c3.X += c3.Y;
                }));
                break;
            case 8:
                _deltaWorld.ForEach(in _deltaQuery, (ForEachAction<D0, D1, D2, D3, D4, D5, D6, D7>)(static (ref D0 c0, ref D1 c1, ref D2 c2, ref D3 c3,
                    ref D4 c4, ref D5 c5, ref D6 c6, ref D7 c7) =>
                {
                    c0.X += c0.Y;
                    c1.X += c1.Y;
                    c2.X += c2.Y;
                    c3.X += c3.Y;
                    c4.X += c4.Y;
                    c5.X += c5.Y;
                    c6.X += c6.Y;
                    c7.X += c7.Y;
                }));
                break;
        }
    }

    [Benchmark]
    public void DeltaECS_LegacyByte_DistinctTypes() => _legacy.Iterate();

    [Benchmark]
    public void Arch_DistinctTypes()
    {
        switch (ComponentCount)
        {
            case 1: _archWorld.Query(_archQuery, static (ref A0 c0) => c0.X += c0.Y); break;
            case 2: _archWorld.Query(_archQuery, static (ref A0 c0, ref A1 c1) => { c0.X += c0.Y; c1.X += c1.Y; }); break;
            case 4: _archWorld.Query(_archQuery, static (ref A0 c0, ref A1 c1, ref A2 c2, ref A3 c3) => { c0.X += c0.Y; c1.X += c1.Y; c2.X += c2.Y; c3.X += c3.Y; }); break;
            case 8: _archWorld.Query(_archQuery, static (ref A0 c0, ref A1 c1, ref A2 c2, ref A3 c3, ref A4 c4, ref A5 c5, ref A6 c6, ref A7 c7) => { c0.X += c0.Y; c1.X += c1.Y; c2.X += c2.Y; c3.X += c3.Y; c4.X += c4.Y; c5.X += c5.Y; c6.X += c6.Y; c7.X += c7.Y; }); break;
        }
    }

    [Benchmark]
    public void Friflo_DistinctTypes()
    {
        switch (ComponentCount)
        {
            case 1: _frifloQ1.ForEachEntity(static (ref F0 c0, Friflo.Engine.ECS.Entity _) => c0.X += c0.Y); break;
            case 2: _frifloQ2.ForEachEntity(static (ref F0 c0, ref F1 c1, Friflo.Engine.ECS.Entity _) => { c0.X += c0.Y; c1.X += c1.Y; }); break;
            case 4: _frifloQ4.ForEachEntity(static (ref F0 c0, ref F1 c1, ref F2 c2, ref F3 c3, Friflo.Engine.ECS.Entity _) => { c0.X += c0.Y; c1.X += c1.Y; c2.X += c2.Y; c3.X += c3.Y; }); break;
            case 8: _frifloQ8.ForEachEntity(static (ref F0 c0, ref F1 c1, ref F2 c2, ref F3 c3, ref F4 c4, Friflo.Engine.ECS.Entity entity) => { c0.X += c0.Y; c1.X += c1.Y; c2.X += c2.Y; c3.X += c3.Y; c4.X += c4.Y; ref var c5 = ref entity.GetComponent<F5>(); ref var c6 = ref entity.GetComponent<F6>(); ref var c7 = ref entity.GetComponent<F7>(); c5.X += c5.Y; c6.X += c6.Y; c7.X += c7.Y; }); break;
        }
    }

    private void SetDeltaValues(DeltaEntity entity)
    {
        if (ComponentCount >= 1) _deltaWorld.Set(entity, _deltaComponents[0], new D0 { X = 1, Y = 2 });
        if (ComponentCount >= 2) _deltaWorld.Set(entity, _deltaComponents[1], new D1 { X = 1, Y = 2 });
        if (ComponentCount >= 4)
        {
            _deltaWorld.Set(entity, _deltaComponents[2], new D2 { X = 1, Y = 2 });
            _deltaWorld.Set(entity, _deltaComponents[3], new D3 { X = 1, Y = 2 });
        }
        if (ComponentCount >= 8)
        {
            _deltaWorld.Set(entity, _deltaComponents[4], new D4 { X = 1, Y = 2 });
            _deltaWorld.Set(entity, _deltaComponents[5], new D5 { X = 1, Y = 2 });
            _deltaWorld.Set(entity, _deltaComponents[6], new D6 { X = 1, Y = 2 });
            _deltaWorld.Set(entity, _deltaComponents[7], new D7 { X = 1, Y = 2 });
        }
    }

    private void SetArchValues(Arch.Core.Entity entity)
    {
        if (ComponentCount >= 1) _archWorld.Set(entity, new A0 { X = 1, Y = 2 });
        if (ComponentCount >= 2) _archWorld.Set(entity, new A1 { X = 1, Y = 2 });
        if (ComponentCount >= 4) { _archWorld.Set(entity, new A2 { X = 1, Y = 2 }); _archWorld.Set(entity, new A3 { X = 1, Y = 2 }); }
        if (ComponentCount >= 8) { _archWorld.Set(entity, new A4 { X = 1, Y = 2 }); _archWorld.Set(entity, new A5 { X = 1, Y = 2 }); _archWorld.Set(entity, new A6 { X = 1, Y = 2 }); _archWorld.Set(entity, new A7 { X = 1, Y = 2 }); }
    }

    private void CreateFrifloEntity()
    {
        switch (ComponentCount)
        {
            case 1: _frifloWorld.CreateEntity(new F0 { X = 1, Y = 2 }); break;
            case 2: _frifloWorld.CreateEntity(new F0 { X = 1, Y = 2 }, new F1 { X = 1, Y = 2 }); break;
            case 4: _frifloWorld.CreateEntity(new F0 { X = 1, Y = 2 }, new F1 { X = 1, Y = 2 }, new F2 { X = 1, Y = 2 }, new F3 { X = 1, Y = 2 }); break;
            case 8: _frifloWorld.CreateEntity(new F0 { X = 1, Y = 2 }, new F1 { X = 1, Y = 2 }, new F2 { X = 1, Y = 2 }, new F3 { X = 1, Y = 2 }, new F4 { X = 1, Y = 2 }, new F5 { X = 1, Y = 2 }, new F6 { X = 1, Y = 2 }, new F7 { X = 1, Y = 2 }); break;
        }
    }

    private struct D0 { public float X; public float Y; }
    private struct D1 { public float X; public float Y; }
    private struct D2 { public float X; public float Y; }
    private struct D3 { public float X; public float Y; }
    private struct D4 { public float X; public float Y; }
    private struct D5 { public float X; public float Y; }
    private struct D6 { public float X; public float Y; }
    private struct D7 { public float X; public float Y; }
    private struct A0 { public float X; public float Y; }
    private struct A1 { public float X; public float Y; }
    private struct A2 { public float X; public float Y; }
    private struct A3 { public float X; public float Y; }
    private struct A4 { public float X; public float Y; }
    private struct A5 { public float X; public float Y; }
    private struct A6 { public float X; public float Y; }
    private struct A7 { public float X; public float Y; }
    private struct F0 : IComponent { public float X; public float Y; }
    private struct F1 : IComponent { public float X; public float Y; }
    private struct F2 : IComponent { public float X; public float Y; }
    private struct F3 : IComponent { public float X; public float Y; }
    private struct F4 : IComponent { public float X; public float Y; }
    private struct F5 : IComponent { public float X; public float Y; }
    private struct F6 : IComponent { public float X; public float Y; }
    private struct F7 : IComponent { public float X; public float Y; }
}

internal sealed class LegacyByteDenseReference
{
    private const int ChunkCapacity = 1024;
    private readonly byte[][][] _chunks;
    private readonly int[] _sizes;

    public LegacyByteDenseReference(int rowCount, int amount, int chunkCapacity = 1024)
    {
        var chunkCount = (amount + chunkCapacity - 1) / chunkCapacity;
        _chunks = new byte[chunkCount][][];
        _sizes = new int[chunkCount];
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var size = Math.Min(chunkCapacity, amount - chunkIndex * chunkCapacity);
            _sizes[chunkIndex] = size;
            _chunks[chunkIndex] = new byte[rowCount][];
            for (var row = 0; row < rowCount; row++)
            {
                _chunks[chunkIndex][row] = new byte[size * Unsafe.SizeOf<LegacyValue>()];
                var values = MemoryMarshal.Cast<byte, LegacyValue>(_chunks[chunkIndex][row].AsSpan());
                for (var i = 0; i < values.Length; i++) values[i] = new LegacyValue { X = 1, Y = 2 };
            }
        }
    }

    public void Iterate()
    {
        for (var chunkIndex = 0; chunkIndex < _chunks.Length; chunkIndex++)
        {
            var rows = _chunks[chunkIndex];
            var size = _sizes[chunkIndex];
            switch (rows.Length)
            {
                case 1:
                    {
                        var row0 = MemoryMarshal.Cast<byte, LegacyValue>(rows[0].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        for (var i = size - 1; i >= 0; i--) row0[i].X += row0[i].Y;
                        break;
                    }
                case 2:
                    {
                        var row0 = MemoryMarshal.Cast<byte, LegacyValue>(rows[0].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row1 = MemoryMarshal.Cast<byte, LegacyValue>(rows[1].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        for (var i = size - 1; i >= 0; i--) { row0[i].X += row0[i].Y; row1[i].X += row1[i].Y; }
                        break;
                    }
                case 4:
                    {
                        var row0 = MemoryMarshal.Cast<byte, LegacyValue>(rows[0].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row1 = MemoryMarshal.Cast<byte, LegacyValue>(rows[1].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row2 = MemoryMarshal.Cast<byte, LegacyValue>(rows[2].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row3 = MemoryMarshal.Cast<byte, LegacyValue>(rows[3].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        for (var i = size - 1; i >= 0; i--) { row0[i].X += row0[i].Y; row1[i].X += row1[i].Y; row2[i].X += row2[i].Y; row3[i].X += row3[i].Y; }
                        break;
                    }
                case 8:
                    {
                        var row0 = MemoryMarshal.Cast<byte, LegacyValue>(rows[0].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row1 = MemoryMarshal.Cast<byte, LegacyValue>(rows[1].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row2 = MemoryMarshal.Cast<byte, LegacyValue>(rows[2].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row3 = MemoryMarshal.Cast<byte, LegacyValue>(rows[3].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row4 = MemoryMarshal.Cast<byte, LegacyValue>(rows[4].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row5 = MemoryMarshal.Cast<byte, LegacyValue>(rows[5].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row6 = MemoryMarshal.Cast<byte, LegacyValue>(rows[6].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        var row7 = MemoryMarshal.Cast<byte, LegacyValue>(rows[7].AsSpan(0, size * Unsafe.SizeOf<LegacyValue>()));
                        for (var i = size - 1; i >= 0; i--)
                        {
                            row0[i].X += row0[i].Y; row1[i].X += row1[i].Y; row2[i].X += row2[i].Y; row3[i].X += row3[i].Y;
                            row4[i].X += row4[i].Y; row5[i].X += row5[i].Y; row6[i].X += row6[i].Y; row7[i].X += row7[i].Y;
                        }
                        break;
                    }
            }
        }
    }

    private struct LegacyValue { public float X; public float Y; }
}
