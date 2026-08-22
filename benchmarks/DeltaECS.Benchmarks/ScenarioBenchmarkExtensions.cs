using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Arch.Core;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Friflo.Engine.ECS;
using ArchComponentType = Arch.Core.Utils.ComponentType;
using FrifloEntity = Friflo.Engine.ECS.Entity;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("SmallDense")]
public class SmallDenseScenarioBenchmarks
{
    [Params(100, 1_000)]
    public int Amount { get; set; }

    [Params(1, 2, 4, 8)]
    public int ComponentCount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteRequest<SmallDenseValue>[] _deltaBindings = Array.Empty<WriteRequest<SmallDenseValue>>();
    private LegacyDenseReference _legacy = null!;

    private Arch.Core.World _archWorld = null!;
    private ArchComponentType[] _archComponents = Array.Empty<ArchComponentType>();
    private Arch.Core.QueryDescription _archQuery;

    private EntityStore _frifloWorld = null!;
    private ArchetypeQuery<F0> _frifloQ1 = null!;
    private ArchetypeQuery<F0, F1> _frifloQ2 = null!;
    private ArchetypeQuery<F0, F1, F2, F3> _frifloQ4 = null!;
    private ArchetypeQuery<F0, F1, F2, F3, F4> _frifloQ5 = null!;

    private static readonly QueryAction<SmallDenseState> s_cachedIteration = IterateSmallDense;

    private struct SmallDenseState
    {
        public int ComponentCount;
        public double Checksum;
        public WriteRequest<SmallDenseValue>[] Bindings;
    }

    private static readonly ArchComponentType[] s_allArchComponents =
    {
        typeof(ArchD0),
        typeof(ArchD1),
        typeof(ArchD2),
        typeof(ArchD3),
        typeof(ArchD4),
        typeof(ArchD5),
        typeof(ArchD6),
        typeof(ArchD7)
    };

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaComponents = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _deltaComponents[i] = layouts.Register<SmallDenseValue>(new SchemaId((ulong)(90_000 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var description = Delta.ECS.QuerySpec.ForComponents(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in description);
        _deltaBindings = new WriteRequest<SmallDenseValue>[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
            _deltaBindings[i] = _deltaQuery.Access<SmallDenseValue>(_deltaComponents[i], AccessMode.Write);

        var entities = new Entity[Amount];
        _deltaWorld.CreateBatch(_deltaComponents, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < _deltaComponents.Length; componentIndex++)
            {
                _deltaWorld.SetComponent(entities[i], _deltaComponents[componentIndex], new SmallDenseValue { X = 1f, Y = 2f });
            }
        }

        _legacy = new LegacyDenseReference(ComponentCount, Amount);

        _archWorld = Arch.Core.World.Create();
        _archComponents = new ArchComponentType[ComponentCount];
        Array.Copy(s_allArchComponents, _archComponents, ComponentCount);
        _archQuery = new Arch.Core.QueryDescription { All = _archComponents };
        _archWorld.Reserve(_archComponents, Amount);
        for (var i = 0; i < Amount; i++)
        {
            var entity = _archWorld.Create(_archComponents);
            for (var componentIndex = 0; componentIndex < ComponentCount; componentIndex++)
            {
                switch (componentIndex)
                {
                    case 0: _archWorld.Set(entity, new ArchD0 { X = 1f, Y = 2f }); break;
                    case 1: _archWorld.Set(entity, new ArchD1 { X = 1f, Y = 2f }); break;
                    case 2: _archWorld.Set(entity, new ArchD2 { X = 1f, Y = 2f }); break;
                    case 3: _archWorld.Set(entity, new ArchD3 { X = 1f, Y = 2f }); break;
                    case 4: _archWorld.Set(entity, new ArchD4 { X = 1f, Y = 2f }); break;
                    case 5: _archWorld.Set(entity, new ArchD5 { X = 1f, Y = 2f }); break;
                    case 6: _archWorld.Set(entity, new ArchD6 { X = 1f, Y = 2f }); break;
                    case 7: _archWorld.Set(entity, new ArchD7 { X = 1f, Y = 2f }); break;
                }
            }
        }

        _frifloWorld = new EntityStore();
        for (var i = 0; i < Amount; i++)
        {
            var entity = _frifloWorld.CreateEntity();
            switch (ComponentCount)
            {
                case 1:
                    entity.AddComponent(new F0 { X = 1f, Y = 2f });
                    break;
                case 2:
                    entity.AddComponent(new F0 { X = 1f, Y = 2f });
                    entity.AddComponent(new F1 { X = 1f, Y = 2f });
                    break;
                case 4:
                    entity.AddComponent(new F0 { X = 1f, Y = 2f });
                    entity.AddComponent(new F1 { X = 1f, Y = 2f });
                    entity.AddComponent(new F2 { X = 1f, Y = 2f });
                    entity.AddComponent(new F3 { X = 1f, Y = 2f });
                    break;
                case 8:
                    entity.AddComponent(new F0 { X = 1f, Y = 2f });
                    entity.AddComponent(new F1 { X = 1f, Y = 2f });
                    entity.AddComponent(new F2 { X = 1f, Y = 2f });
                    entity.AddComponent(new F3 { X = 1f, Y = 2f });
                    entity.AddComponent(new F4 { X = 1f, Y = 2f });
                    entity.AddComponent(new F5 { X = 1f, Y = 2f });
                    entity.AddComponent(new F6 { X = 1f, Y = 2f });
                    entity.AddComponent(new F7 { X = 1f, Y = 2f });
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        _frifloQ1 = _frifloWorld.Query<F0>();
        _frifloQ2 = _frifloWorld.Query<F0, F1>();
        _frifloQ4 = _frifloWorld.Query<F0, F1, F2, F3>();
        _frifloQ5 = _frifloWorld.Query<F0, F1, F2, F3, F4>();
    }

    [Benchmark]
    public double DeltaECS_QueryPlan()
    {
        var state = new SmallDenseState { ComponentCount = ComponentCount, Bindings = _deltaBindings };
        _deltaWorld.Query(in _deltaQuery, ref state, s_cachedIteration);
        return state.Checksum;
    }

    [Benchmark]
    public double DeltaECS_Legacy()
    {
        return _legacy.Iterate();
    }

    [Benchmark]
    public double Arch_Movement4Components()
    {
        var checksum = 0d;
        switch (ComponentCount)
        {
            case 1:
                _archWorld.Query(_archQuery, (ref ArchD0 c0) => checksum += UpdateAndChecksum(ref c0));
                break;
            case 2:
                _archWorld.Query(_archQuery, (ref ArchD0 c0, ref ArchD1 c1) =>
                {
                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                });
                break;
            case 4:
                _archWorld.Query(_archQuery, (ref ArchD0 c0, ref ArchD1 c1, ref ArchD2 c2, ref ArchD3 c3) =>
                {
                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                    checksum += UpdateAndChecksum(ref c2);
                    checksum += UpdateAndChecksum(ref c3);
                });
                break;
            case 8:
                _archWorld.Query(_archQuery, (ref ArchD0 c0, ref ArchD1 c1, ref ArchD2 c2, ref ArchD3 c3, ref ArchD4 c4, ref ArchD5 c5, ref ArchD6 c6, ref ArchD7 c7) =>
                {
                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                    checksum += UpdateAndChecksum(ref c2);
                    checksum += UpdateAndChecksum(ref c3);
                    checksum += UpdateAndChecksum(ref c4);
                    checksum += UpdateAndChecksum(ref c5);
                    checksum += UpdateAndChecksum(ref c6);
                    checksum += UpdateAndChecksum(ref c7);
                });
                break;
            default:
                throw new InvalidOperationException();
        }

        return checksum;
    }

    [Benchmark]
    public double Friflo_Movement4Components()
    {
        var checksum = 0d;
        switch (ComponentCount)
        {
            case 1:
                _frifloQ1.ForEachEntity((ref F0 c0, FrifloEntity _) => checksum += UpdateAndChecksum(ref c0));
                break;
            case 2:
                _frifloQ2.ForEachEntity((ref F0 c0, ref F1 c1, FrifloEntity _) =>
                {
                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                });
                break;
            case 4:
                _frifloQ4.ForEachEntity((ref F0 c0, ref F1 c1, ref F2 c2, ref F3 c3, FrifloEntity _) =>
                {
                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                    checksum += UpdateAndChecksum(ref c2);
                    checksum += UpdateAndChecksum(ref c3);
                });
                break;
            case 8:
                _frifloQ5.ForEachEntity((ref F0 c0, ref F1 c1, ref F2 c2, ref F3 c3, ref F4 c4, FrifloEntity entity) =>
                {
                    ref var c5 = ref entity.GetComponent<F5>();
                    ref var c6 = ref entity.GetComponent<F6>();
                    ref var c7 = ref entity.GetComponent<F7>();

                    checksum += UpdateAndChecksum(ref c0);
                    checksum += UpdateAndChecksum(ref c1);
                    checksum += UpdateAndChecksum(ref c2);
                    checksum += UpdateAndChecksum(ref c3);
                    checksum += UpdateAndChecksum(ref c4);
                    checksum += UpdateAndChecksum(ref c5);
                    checksum += UpdateAndChecksum(ref c6);
                    checksum += UpdateAndChecksum(ref c7);
                });
                break;
            default:
                throw new InvalidOperationException();
        }

        return checksum;
    }

    private static void IterateSmallDense(ref SmallDenseState state, ref QueryChunkCursor lease)
    {
        switch (state.ComponentCount)
        {
            case 1:
            {
                var c0 = lease.Get(state.Bindings[0]);
                while (lease.MoveNext())
                {
                    ref var value = ref c0[lease];
                    value.X += value.Y;
                    state.Checksum += value.X + value.Y;
                }

                break;
            }
            case 2:
            {
                var c0 = lease.Get(state.Bindings[0]);
                var c1 = lease.Get(state.Bindings[1]);
                while (lease.MoveNext())
                {
                    ref var v0 = ref c0[lease];
                    ref var v1 = ref c1[lease];
                    v0.X += v0.Y;
                    v1.X += v1.Y;
                    state.Checksum += v0.X + v1.X + v0.Y + v1.Y;
                }

                break;
            }
            case 4:
            {
                var c0 = lease.Get(state.Bindings[0]);
                var c1 = lease.Get(state.Bindings[1]);
                var c2 = lease.Get(state.Bindings[2]);
                var c3 = lease.Get(state.Bindings[3]);
                while (lease.MoveNext())
                {
                    ref var v0 = ref c0[lease];
                    ref var v1 = ref c1[lease];
                    ref var v2 = ref c2[lease];
                    ref var v3 = ref c3[lease];
                    v0.X += v0.Y;
                    v1.X += v1.Y;
                    v2.X += v2.Y;
                    v3.X += v3.Y;
                    state.Checksum += v0.X + v1.X + v2.X + v3.X + v0.Y + v1.Y + v2.Y + v3.Y;
                }

                break;
            }
            case 8:
            {
                var c0 = lease.Get(state.Bindings[0]);
                var c1 = lease.Get(state.Bindings[1]);
                var c2 = lease.Get(state.Bindings[2]);
                var c3 = lease.Get(state.Bindings[3]);
                var c4 = lease.Get(state.Bindings[4]);
                var c5 = lease.Get(state.Bindings[5]);
                var c6 = lease.Get(state.Bindings[6]);
                var c7 = lease.Get(state.Bindings[7]);
                while (lease.MoveNext())
                {
                    ref var v0 = ref c0[lease];
                    ref var v1 = ref c1[lease];
                    ref var v2 = ref c2[lease];
                    ref var v3 = ref c3[lease];
                    ref var v4 = ref c4[lease];
                    ref var v5 = ref c5[lease];
                    ref var v6 = ref c6[lease];
                    ref var v7 = ref c7[lease];
                    v0.X += v0.Y;
                    v1.X += v1.Y;
                    v2.X += v2.Y;
                    v3.X += v3.Y;
                    v4.X += v4.Y;
                    v5.X += v5.Y;
                    v6.X += v6.Y;
                    v7.X += v7.Y;
                    state.Checksum += v0.X + v1.X + v2.X + v3.X + v4.X + v5.X + v6.X + v7.X;
                    state.Checksum += v0.Y + v1.Y + v2.Y + v3.Y + v4.Y + v5.Y + v6.Y + v7.Y;
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double UpdateAndChecksum<T>(ref T value) where T : unmanaged
    {
        ref var x = ref Unsafe.As<T, float>(ref value);
        ref var y = ref Unsafe.Add(ref x, 1);
        x += y;
        return x + y;
    }

    private struct SmallDenseValue
    {
        public float X;
        public float Y;
    }

    private struct ArchD0 { public float X; public float Y; }
    private struct ArchD1 { public float X; public float Y; }
    private struct ArchD2 { public float X; public float Y; }
    private struct ArchD3 { public float X; public float Y; }
    private struct ArchD4 { public float X; public float Y; }
    private struct ArchD5 { public float X; public float Y; }
    private struct ArchD6 { public float X; public float Y; }
    private struct ArchD7 { public float X; public float Y; }

    private struct F0 : IComponent { public float X; public float Y; }
    private struct F1 : IComponent { public float X; public float Y; }
    private struct F2 : IComponent { public float X; public float Y; }
    private struct F3 : IComponent { public float X; public float Y; }
    private struct F4 : IComponent { public float X; public float Y; }
    private struct F5 : IComponent { public float X; public float Y; }
    private struct F6 : IComponent { public float X; public float Y; }
    private struct F7 : IComponent { public float X; public float Y; }
}

[MemoryDiagnoser]
[SimpleJob]
[BenchmarkCategory("WideArchetypeNarrow")]
public class WideArchetypeNarrowAccessBenchmarks
{
    [Params(8, 16, 32, 64)]
    public int ArchetypeWidth { get; set; }

    [Params(100, 1_000, 10_000, 100_000)]
    public int Amount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteRequest<WideDenseValue> _deltaPositionBinding;
    private ReadRequest<WideDenseValue> _deltaVelocityBinding;
    private LegacyWideReference _legacy = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (ArchetypeWidth < 2)
        {
            throw new InvalidOperationException("Archetype width must include position and velocity rows.");
        }

        var layouts = new ComponentLayoutRegistry();
        _deltaComponents = new ComponentId[ArchetypeWidth];
        for (var i = 0; i < ArchetypeWidth; i++)
        {
            _deltaComponents[i] = layouts.Register<WideDenseValue>(new SchemaId((ulong)(91_000 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var query = Delta.ECS.QuerySpec.ForComponents(_deltaComponents[0], _deltaComponents[1]);
        _deltaQuery = _deltaWorld.CreateQuery(in query);
        _deltaPositionBinding = _deltaQuery.Access<WideDenseValue>(_deltaComponents[0], AccessMode.Write);
        _deltaVelocityBinding = _deltaQuery.Access<WideDenseValue>(_deltaComponents[1], AccessMode.Read);

        var entities = new Entity[Amount];
        _deltaWorld.CreateBatch(_deltaComponents, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < ArchetypeWidth; componentIndex++)
            {
                _deltaWorld.SetComponent(entities[i], _deltaComponents[componentIndex], new WideDenseValue { X = 1f, Y = 2f + componentIndex });
            }
        }

        _legacy = new LegacyWideReference(ArchetypeWidth, Amount);
    }

    [Benchmark(Baseline = true)]
    public double DeltaECS_NarrowAccess()
    {
        var state = new SmallWideState { Position = _deltaPositionBinding, Velocity = _deltaVelocityBinding };
        _deltaWorld.Query(in _deltaQuery, ref state, static (ref SmallWideState s, ref QueryChunkCursor cursor) =>
        {
            var positions = cursor.Get(s.Position);
            var velocities = cursor.Get(s.Velocity);
            while (cursor.MoveNext())
            {
                ref var pos = ref positions[cursor];
                var vel = velocities[cursor];
                pos.X += vel.X;
                pos.Y += vel.Y;
                s.Checksum += pos.X + pos.Y;
            }
        });

        return state.Checksum;
    }

    [Benchmark]
    public double LegacyWide_NarrowAccess()
    {
        return _legacy.IteratePositionVelocity();
    }

    private struct SmallWideState
    {
        public double Checksum;
        public WriteRequest<WideDenseValue> Position;
        public ReadRequest<WideDenseValue> Velocity;
    }

    private struct WideDenseValue
    {
        public float X;
        public float Y;
    }

}

[MemoryDiagnoser]
[SimpleJob]
[BenchmarkCategory("WideArchetypeNarrow")]
public class WideArchetypeNarrowAccessComparisonBenchmarks
{
    [Params(8)]
    public int ArchetypeWidth { get; set; }

    [Params(100, 1_000, 10_000, 100_000)]
    public int Amount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteRequest<WideDenseValue> _deltaPositionBinding;
    private ReadRequest<WideDenseValue> _deltaVelocityBinding;
    private LegacyWideReference _legacy = null!;

    private Arch.Core.World _archWorld = null!;
    private ArchComponentType[] _archComponents = Array.Empty<ArchComponentType>();
    private Arch.Core.QueryDescription _archQuery;

    private EntityStore _frifloWorld = null!;
    private ArchetypeQuery<W0, W1> _frifloQ2 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaComponents = new ComponentId[ArchetypeWidth];
        for (var i = 0; i < ArchetypeWidth; i++)
        {
            _deltaComponents[i] = layouts.Register<WideDenseValue>(new SchemaId((ulong)(92_000 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var query = Delta.ECS.QuerySpec.ForComponents(_deltaComponents[0], _deltaComponents[1]);
        _deltaQuery = _deltaWorld.CreateQuery(in query);
        _deltaPositionBinding = _deltaQuery.Access<WideDenseValue>(_deltaComponents[0], AccessMode.Write);
        _deltaVelocityBinding = _deltaQuery.Access<WideDenseValue>(_deltaComponents[1], AccessMode.Read);

        var entities = new Entity[Amount];
        _deltaWorld.CreateBatch(_deltaComponents, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            for (var componentIndex = 0; componentIndex < ArchetypeWidth; componentIndex++)
            {
                _deltaWorld.SetComponent(entities[i], _deltaComponents[componentIndex], new WideDenseValue { X = 1f, Y = 2f + componentIndex });
            }
        }

        _legacy = new LegacyWideReference(ArchetypeWidth, Amount);

        _archWorld = Arch.Core.World.Create();
        _archComponents = new ArchComponentType[]
        {
            typeof(ArchA0),
            typeof(ArchA1),
            typeof(ArchA2),
            typeof(ArchA3),
            typeof(ArchA4),
            typeof(ArchA5),
            typeof(ArchA6),
            typeof(ArchA7)
        };
        _archQuery = new Arch.Core.QueryDescription { All = _archComponents };
        _archWorld.Reserve(_archComponents, Amount);
        for (var i = 0; i < Amount; i++)
        {
            var entity = _archWorld.Create(_archComponents);
            _archWorld.Set(entity, new ArchA0 { X = 1f, Y = 2f });
            _archWorld.Set(entity, new ArchA1 { X = 1f, Y = 3f });
            _archWorld.Set(entity, new ArchA2 { X = 1f, Y = 4f });
            _archWorld.Set(entity, new ArchA3 { X = 1f, Y = 5f });
            _archWorld.Set(entity, new ArchA4 { X = 1f, Y = 6f });
            _archWorld.Set(entity, new ArchA5 { X = 1f, Y = 7f });
            _archWorld.Set(entity, new ArchA6 { X = 1f, Y = 8f });
            _archWorld.Set(entity, new ArchA7 { X = 1f, Y = 9f });
        }

        _frifloWorld = new EntityStore();
        for (var i = 0; i < Amount; i++)
        {
            var entity = _frifloWorld.CreateEntity(new W0 { X = 1f, Y = 2f }, new W1 { X = 1f, Y = 3f }, new W2 { X = 1f, Y = 4f }, new W3 { X = 1f, Y = 5f }, new W4 { X = 1f, Y = 6f }, new W5 { X = 1f, Y = 7f }, new W6 { X = 1f, Y = 8f }, new W7 { X = 1f, Y = 9f });
        }

        _frifloQ2 = _frifloWorld.Query<W0, W1>();
    }

    [Benchmark(Baseline = true)]
    public double DeltaECS_ComparisonNarrow()
    {
        var state = new WideComparisonState { Position = _deltaPositionBinding, Velocity = _deltaVelocityBinding };
        _deltaWorld.Query(in _deltaQuery, ref state, static (ref WideComparisonState current, ref QueryChunkCursor cursor) =>
        {
            var positions = cursor.Get(current.Position);
            var velocities = cursor.Get(current.Velocity);
            while (cursor.MoveNext())
            {
                ref var pos = ref positions[cursor];
                var vel = velocities[cursor];
                pos.X += vel.X;
                pos.Y += vel.Y;
                current.Checksum += pos.X + pos.Y;
            }
        });

        return state.Checksum;
    }

    [Benchmark]
    public double Arch_ComparisonNarrow()
    {
        var checksum = 0d;
        _archWorld.Query(_archQuery, (ref ArchA0 c0, ref ArchA1 c1) =>
        {
            c0.X += c1.X;
            c0.Y += c1.Y;
            checksum += c0.X + c0.Y;
        });

        return checksum;
    }

    [Benchmark]
    public double Friflo_ComparisonNarrow()
    {
        var checksum = 0d;
        _frifloQ2.ForEachEntity((ref W0 c0, ref W1 c1, FrifloEntity _) =>
        {
            c0.X += c1.X;
            c0.Y += c1.Y;
            checksum += c0.X + c0.Y;
        });

        return checksum;
    }

    [Benchmark]
    public double Legacy_ComparisonNarrow()
    {
        return _legacy.IteratePositionVelocity();
    }

    private struct WideDenseValue
    {
        public float X;
        public float Y;
    }

    private struct WideComparisonState
    {
        public WriteRequest<WideDenseValue> Position;
        public ReadRequest<WideDenseValue> Velocity;
        public double Checksum;
    }

    private struct ArchA0 { public float X; public float Y; }
    private struct ArchA1 { public float X; public float Y; }
    private struct ArchA2 { public float X; public float Y; }
    private struct ArchA3 { public float X; public float Y; }
    private struct ArchA4 { public float X; public float Y; }
    private struct ArchA5 { public float X; public float Y; }
    private struct ArchA6 { public float X; public float Y; }
    private struct ArchA7 { public float X; public float Y; }

    private struct W0 : IComponent { public float X; public float Y; }
    private struct W1 : IComponent { public float X; public float Y; }
    private struct W2 : IComponent { public float X; public float Y; }
    private struct W3 : IComponent { public float X; public float Y; }
    private struct W4 : IComponent { public float X; public float Y; }
    private struct W5 : IComponent { public float X; public float Y; }
    private struct W6 : IComponent { public float X; public float Y; }
    private struct W7 : IComponent { public float X; public float Y; }
}

[MemoryDiagnoser]
[ShortRunJob]
[BenchmarkCategory("SparseHeterogeneousQuery")]
public class SparseHeterogeneousQueryBenchmarks
{
    private const int NoiseComponentCount = 10;
    private const int MatchStride = 4;
    private const int ColdMarkerCount = 128;

    [Params(100, 1_000)]
    public int Amount { get; set; }

    private World _deltaWorld = null!;
    private ComponentId _deltaPosition;
    private ComponentId _deltaVelocity;
    private ComponentId[] _deltaNoise = Array.Empty<ComponentId>();
    private ComponentId[] _deltaColdMarkers = Array.Empty<ComponentId>();
    private Query _deltaWarmQuery;
    private WriteRequest<SparseValue> _deltaPositionBinding;
    private ReadRequest<SparseValue> _deltaVelocityBinding;
    private int _deltaColdMarkerIndex;

    private Arch.Core.World _archWorld = null!;
    private ArchComponentType[] _archAllComponents = Array.Empty<ArchComponentType>();
    private Arch.Core.QueryDescription _archWarmQuery;

    private EntityStore _frifloWorld = null!;
    private ArchetypeQuery<SparseF0, SparseF1> _frifloWarmQuery = null!;

    private LegacySparseReference _legacy = null!;

    private struct SparseState
    {
        public int Count;
        public double Checksum;
        public WriteRequest<SparseValue> Position;
        public ReadRequest<SparseValue> Velocity;
    }

    [GlobalSetup]
    public void Setup()
    {
        SetupDelta();
        SetupArch();
        SetupFriflo();
        _legacy = new LegacySparseReference(Amount);

        // Prime only the explicitly warm handle. Cold methods use an
        // equivalent query with an absent marker component, which forces a
        // fresh matching plan while selecting the same entities.
        var warmState = new SparseState();
        _deltaWorld.Query(in _deltaWarmQuery, ref warmState, CountDeltaMatches);
        if (warmState.Count != ExpectedMatches)
        {
            throw new InvalidOperationException($"Delta warm query matched {warmState.Count}, expected {ExpectedMatches}.");
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CachedWarm")]
    public double DeltaECS_CachedWarmQuery()
    {
        var state = new SparseState { Position = _deltaPositionBinding, Velocity = _deltaVelocityBinding };
        _deltaWorld.Query(in _deltaWarmQuery, ref state, IterateDeltaMatches);
        return CheckResult(state, "DeltaECS cached warm query");
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Cold")]
    public double DeltaECS_ColdQuery()
    {
        var marker = _deltaColdMarkers[_deltaColdMarkerIndex++ % _deltaColdMarkers.Length];
        var description = new QuerySpec(
            new[] { _deltaPosition, _deltaVelocity },
            Array.Empty<ComponentId>(),
            new[] { marker },
            Array.Empty<TagId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        var coldQuery = _deltaWorld.CreateQuery(in description);
        var positionBinding = coldQuery.Access<SparseValue>(_deltaPosition, AccessMode.Write);
        var velocityBinding = coldQuery.Access<SparseValue>(_deltaVelocity, AccessMode.Read);
        var state = new SparseState { Position = positionBinding, Velocity = velocityBinding };
        _deltaWorld.Query(in coldQuery, ref state, IterateDeltaMatches);
        return CheckResult(state, "DeltaECS cold query");
    }

    [Benchmark]
    [BenchmarkCategory("CachedWarm")]
    public double Legacy_CachedWarmQuery()
    {
        return _legacy.IterateWarm();
    }

    [Benchmark]
    [BenchmarkCategory("Cold")]
    public double Legacy_ColdQuery()
    {
        return _legacy.IterateCold();
    }

    [Benchmark]
    [BenchmarkCategory("CachedWarm")]
    public double Arch_CachedWarmQuery()
    {
        var state = new SparseState();
        _archWorld.Query(_archWarmQuery, (ref SparseA0 position, ref SparseA1 velocity) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
            state.Count++;
            state.Checksum += position.X + position.Y;
        });
        return CheckResult(state, "Arch cached warm query");
    }

    [Benchmark]
    [BenchmarkCategory("Cold")]
    public double Arch_ColdQuery()
    {
        var description = new Arch.Core.QueryDescription
        {
            All = new ArchComponentType[] { typeof(SparseA0), typeof(SparseA1) }
        };
        var state = new SparseState();
        _archWorld.Query(description, (ref SparseA0 position, ref SparseA1 velocity) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
            state.Count++;
            state.Checksum += position.X + position.Y;
        });
        return CheckResult(state, "Arch cold query");
    }

    [Benchmark]
    [BenchmarkCategory("CachedWarm")]
    public double Friflo_CachedWarmQuery()
    {
        var state = new SparseState();
        _frifloWarmQuery.ForEachEntity((ref SparseF0 position, ref SparseF1 velocity, FrifloEntity _) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
            state.Count++;
            state.Checksum += position.X + position.Y;
        });
        return CheckResult(state, "Friflo cached warm query");
    }

    [Benchmark]
    [BenchmarkCategory("Cold")]
    public double Friflo_ColdQuery()
    {
        var coldQuery = _frifloWorld.Query<SparseF0, SparseF1>();
        var state = new SparseState();
        coldQuery.ForEachEntity((ref SparseF0 position, ref SparseF1 velocity, FrifloEntity _) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
            state.Count++;
            state.Checksum += position.X + position.Y;
        });
        return CheckResult(state, "Friflo cold query");
    }

    private int ExpectedMatches => (Amount + MatchStride - 1) / MatchStride;

    private void SetupDelta()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register<SparseValue>(new SchemaId(93_000));
        _deltaVelocity = layouts.Register<SparseValue>(new SchemaId(93_001));
        _deltaNoise = new ComponentId[NoiseComponentCount];
        for (var i = 0; i < _deltaNoise.Length; i++)
        {
            _deltaNoise[i] = layouts.Register<SparseValue>(new SchemaId((ulong)(93_010 + i)));
        }

        _deltaColdMarkers = new ComponentId[ColdMarkerCount];
        for (var i = 0; i < _deltaColdMarkers.Length; i++)
        {
            _deltaColdMarkers[i] = layouts.Register<SparseValue>(new SchemaId((ulong)(94_000 + i)));
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        for (var entityIndex = 0; entityIndex < Amount; entityIndex++)
        {
            var signature = BuildDeltaSignature(entityIndex);
            var entity = _deltaWorld.Create(signature);
            for (var componentIndex = 0; componentIndex < signature.Length; componentIndex++)
            {
                var component = signature[componentIndex];
                var value = component == _deltaVelocity
                    ? new SparseValue { X = 3f, Y = 4f }
                    : new SparseValue { X = 1f, Y = 2f };
                _deltaWorld.SetComponent(entity, component, value);
            }
        }

        var warmDescription = QuerySpec.ForComponents(_deltaPosition, _deltaVelocity);
        _deltaWarmQuery = _deltaWorld.CreateQuery(in warmDescription);
        _deltaPositionBinding = _deltaWarmQuery.Access<SparseValue>(_deltaPosition, AccessMode.Write);
        _deltaVelocityBinding = _deltaWarmQuery.Access<SparseValue>(_deltaVelocity, AccessMode.Read);
    }

    private void SetupArch()
    {
        _archWorld = Arch.Core.World.Create();
        _archAllComponents = new ArchComponentType[]
        {
            typeof(SparseA0), typeof(SparseA1), typeof(SparseA2), typeof(SparseA3),
            typeof(SparseA4), typeof(SparseA5), typeof(SparseA6), typeof(SparseA7),
            typeof(SparseA8), typeof(SparseA9), typeof(SparseA10), typeof(SparseA11)
        };

        for (var entityIndex = 0; entityIndex < Amount; entityIndex++)
        {
            var signature = BuildArchSignature(entityIndex);
            var entity = _archWorld.Create(signature);
            if (IsMatch(entityIndex))
            {
                _archWorld.Set(entity, new SparseA0 { X = 1f, Y = 2f });
                _archWorld.Set(entity, new SparseA1 { X = 3f, Y = 4f });
            }
        }

        _archWarmQuery = new Arch.Core.QueryDescription
        {
            All = new ArchComponentType[] { typeof(SparseA0), typeof(SparseA1) }
        };
    }

    private void SetupFriflo()
    {
        _frifloWorld = new EntityStore();
        for (var entityIndex = 0; entityIndex < Amount; entityIndex++)
        {
            var entity = _frifloWorld.CreateEntity();
            if (IsMatch(entityIndex))
            {
                entity.AddComponent(new SparseF0 { X = 1f, Y = 2f });
                entity.AddComponent(new SparseF1 { X = 3f, Y = 4f });
            }

            AddFrifloNoise(entity, NoiseMask(entityIndex));
        }

        _frifloWarmQuery = _frifloWorld.Query<SparseF0, SparseF1>();
    }

    private ComponentId[] BuildDeltaSignature(int entityIndex)
    {
        var noiseMask = NoiseMask(entityIndex);
        var signature = new List<ComponentId>(NoiseComponentCount + 2);
        if (IsMatch(entityIndex))
        {
            signature.Add(_deltaPosition);
            signature.Add(_deltaVelocity);
        }

        for (var noiseIndex = 0; noiseIndex < NoiseComponentCount; noiseIndex++)
        {
            if ((noiseMask & (1 << noiseIndex)) != 0)
            {
                signature.Add(_deltaNoise[noiseIndex]);
            }
        }

        return signature.ToArray();
    }

    private ArchComponentType[] BuildArchSignature(int entityIndex)
    {
        var noiseMask = NoiseMask(entityIndex);
        var signature = new List<ArchComponentType>(NoiseComponentCount + 2);
        if (IsMatch(entityIndex))
        {
            signature.Add(typeof(SparseA0));
            signature.Add(typeof(SparseA1));
        }

        for (var noiseIndex = 0; noiseIndex < NoiseComponentCount; noiseIndex++)
        {
            if ((noiseMask & (1 << noiseIndex)) != 0)
            {
                signature.Add(_archAllComponents[noiseIndex + 2]);
            }
        }

        return signature.ToArray();
    }

    private static void AddFrifloNoise(FrifloEntity entity, int noiseMask)
    {
        if ((noiseMask & (1 << 0)) != 0) entity.AddComponent(new SparseF2());
        if ((noiseMask & (1 << 1)) != 0) entity.AddComponent(new SparseF3());
        if ((noiseMask & (1 << 2)) != 0) entity.AddComponent(new SparseF4());
        if ((noiseMask & (1 << 3)) != 0) entity.AddComponent(new SparseF5());
        if ((noiseMask & (1 << 4)) != 0) entity.AddComponent(new SparseF6());
        if ((noiseMask & (1 << 5)) != 0) entity.AddComponent(new SparseF7());
        if ((noiseMask & (1 << 6)) != 0) entity.AddComponent(new SparseF8());
        if ((noiseMask & (1 << 7)) != 0) entity.AddComponent(new SparseF9());
        if ((noiseMask & (1 << 8)) != 0) entity.AddComponent(new SparseF10());
        if ((noiseMask & (1 << 9)) != 0) entity.AddComponent(new SparseF11());
    }

    private static int NoiseMask(int entityIndex)
    {
        var hash = (uint)entityIndex * 2_654_435_761u;
        hash ^= hash >> 16;
        var mask = (int)(hash & ((1u << NoiseComponentCount) - 1));
        return mask == 0 ? 1 << (entityIndex % NoiseComponentCount) : mask;
    }

    private static bool IsMatch(int entityIndex) => entityIndex % MatchStride == 0;

    private static void CountDeltaMatches(ref SparseState state, ref QueryChunkCursor cursor)
    {
        while (cursor.MoveNext())
        {
            if (cursor.IsActiveSlot(cursor.CurrentIndex)) state.Count++;
        }
    }

    private static void IterateDeltaMatches(ref SparseState state, ref QueryChunkCursor cursor)
    {
        var positions = cursor.Get(state.Position);
        var velocities = cursor.Get(state.Velocity);
        while (cursor.MoveNext())
        {
            if (!cursor.IsActiveSlot(cursor.CurrentIndex)) continue;
            ref var position = ref positions[cursor];
            var velocity = velocities[cursor];
            position.X += velocity.X;
            position.Y += velocity.Y;
            state.Count++;
            state.Checksum += position.X + position.Y;
        }
    }

    private double CheckResult(SparseState state, string queryName)
    {
        if (state.Count != ExpectedMatches)
        {
            throw new InvalidOperationException($"{queryName} matched {state.Count}, expected {ExpectedMatches}.");
        }

        return state.Checksum;
    }

    private struct SparseValue
    {
        public float X;
        public float Y;
    }

    private struct SparseA0 { public float X; public float Y; }
    private struct SparseA1 { public float X; public float Y; }
    private struct SparseA2 { }
    private struct SparseA3 { }
    private struct SparseA4 { }
    private struct SparseA5 { }
    private struct SparseA6 { }
    private struct SparseA7 { }
    private struct SparseA8 { }
    private struct SparseA9 { }
    private struct SparseA10 { }
    private struct SparseA11 { }

    private struct SparseF0 : IComponent { public float X; public float Y; }
    private struct SparseF1 : IComponent { public float X; public float Y; }
    private struct SparseF2 : IComponent { }
    private struct SparseF3 : IComponent { }
    private struct SparseF4 : IComponent { }
    private struct SparseF5 : IComponent { }
    private struct SparseF6 : IComponent { }
    private struct SparseF7 : IComponent { }
    private struct SparseF8 : IComponent { }
    private struct SparseF9 : IComponent { }
    private struct SparseF10 : IComponent { }
    private struct SparseF11 : IComponent { }
}

internal sealed class LegacySparseReference
{
    private readonly LegacySparseValue[] _positions;
    private readonly LegacySparseValue[] _velocities;
    private readonly bool[] _hasTarget;
    private readonly int[] _matchingIndices;

    public LegacySparseReference(int amount)
    {
        _positions = new LegacySparseValue[amount];
        _velocities = new LegacySparseValue[amount];
        _hasTarget = new bool[amount];
        _matchingIndices = new int[(amount + 3) / 4];
        var matchingCount = 0;
        for (var entityIndex = 0; entityIndex < amount; entityIndex++)
        {
            _positions[entityIndex] = new LegacySparseValue { X = 1f, Y = 2f };
            _velocities[entityIndex] = new LegacySparseValue { X = 3f, Y = 4f };
            if (entityIndex % 4 == 0)
            {
                _hasTarget[entityIndex] = true;
                _matchingIndices[matchingCount++] = entityIndex;
            }
        }
    }

    public double IterateWarm()
    {
        var checksum = 0d;
        for (var matchIndex = 0; matchIndex < _matchingIndices.Length; matchIndex++)
        {
            var entityIndex = _matchingIndices[matchIndex];
            checksum += Update(entityIndex);
        }

        return checksum;
    }

    public double IterateCold()
    {
        var checksum = 0d;
        for (var entityIndex = 0; entityIndex < _hasTarget.Length; entityIndex++)
        {
            if (_hasTarget[entityIndex])
            {
                checksum += Update(entityIndex);
            }
        }

        return checksum;
    }

    private double Update(int entityIndex)
    {
        var position = _positions[entityIndex];
        var velocity = _velocities[entityIndex];
        position.X += velocity.X;
        position.Y += velocity.Y;
        _positions[entityIndex] = position;
        return position.X + position.Y;
    }

    private struct LegacySparseValue
    {
        public float X;
        public float Y;
    }
}

internal sealed class LegacyDenseReference
{
    private readonly byte[][] _rows;

    public LegacyDenseReference(int rowCount, int amount)
    {
        var valueSize = Unsafe.SizeOf<LegacyDenseValue>();
        _rows = new byte[rowCount][];
        for (var row = 0; row < rowCount; row++)
        {
            var bytes = new byte[amount * valueSize];
            var values = MemoryMarshal.Cast<byte, LegacyDenseValue>(bytes.AsSpan());
            for (var i = values.Length - 1; i >= 0; i--)
            {
                values[i] = new LegacyDenseValue { X = 1f, Y = 2f };
            }

            _rows[row] = bytes;
        }
    }

    public double Iterate()
    {
        for (var rowIndex = 0; rowIndex < _rows.Length; rowIndex++)
        {
            var values = MemoryMarshal.Cast<byte, LegacyDenseValue>(_rows[rowIndex].AsSpan());
            for (var i = values.Length - 1; i >= 0; i--)
            {
                var value = values[i];
                value.X += value.Y;
                values[i] = value;
            }
        }

        double checksum = 0;
        for (var rowIndex = 0; rowIndex < _rows.Length; rowIndex++)
        {
            var values = MemoryMarshal.Cast<byte, LegacyDenseValue>(_rows[rowIndex].AsSpan());
            for (var i = values.Length - 1; i >= 0; i--)
            {
                checksum += values[i].X;
            }
        }

        return checksum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LegacyDenseValue
    {
        public float X;
        public float Y;
    }
}

internal sealed class LegacyWideReference
{
    private readonly byte[][] _rows;
    private readonly int _amount;

    public LegacyWideReference(int archetypeWidth, int amount)
    {
        var valueSize = Unsafe.SizeOf<LegacyWideValue>();
        _amount = amount;
        _rows = new byte[archetypeWidth][];
        for (var row = 0; row < archetypeWidth; row++)
        {
            var bytes = new byte[amount * valueSize];
            var values = MemoryMarshal.Cast<byte, LegacyWideValue>(bytes.AsSpan());
            for (var i = values.Length - 1; i >= 0; i--)
            {
                values[i] = new LegacyWideValue { X = 1f, Y = 2f + row };
            }

            _rows[row] = bytes;
        }
    }

    public double IteratePositionVelocity()
    {
        if (_rows.Length < 2)
        {
            return 0;
        }

        var positions = MemoryMarshal.Cast<byte, LegacyWideValue>(_rows[0].AsSpan());
        var velocities = MemoryMarshal.Cast<byte, LegacyWideValue>(_rows[1].AsSpan());
        var checksum = 0d;

        for (var i = _amount - 1; i >= 0; i--)
        {
            var pos = positions[i];
            var vel = velocities[i];
            pos.X += vel.X;
            pos.Y += vel.Y;
            positions[i] = pos;
            checksum += pos.X + pos.Y;
        }

        return checksum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LegacyWideValue
    {
        public float X;
        public float Y;
    }
}
