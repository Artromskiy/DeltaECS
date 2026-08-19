using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Delta.ECS;
using Leopotam.EcsLite;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class EcsLiteComparisonBenchmarks
{
    private const float Dt = 1.0f / 60.0f;

    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(0, 2, 6)]
    public int PayloadRows { get; set; }

    private World _deltaMovementWorld = null!;
    private QueryHandle _deltaMovementQuery;
    private ComponentId _deltaPosition;
    private ComponentId _deltaVelocity;
    private ComponentId[] _deltaMovementPayload = [];
    private Entity[] _deltaMovementEntities = [];

    private World _deltaFilterWorld = null!;
    private QueryHandle _deltaFilterQuery;
    private ComponentId[] _deltaFilterPayload = [];
    private Entity[] _deltaFilterEntities = [];

    private World _deltaCreateDestroyWorld = null!;
    private Entity[] _deltaCreateDestroyEntities = [];
    private ComponentId[] _deltaCreateDestroyComponents = [];

    private World _deltaTransitionWorld = null!;
    private ComponentId[] _deltaTransitionPayload = [];
    private ComponentId[] _deltaTransitionPayloadRows = [];
    private Entity[] _deltaTransitionEntities = [];

    private EcsWorld _leoMovementWorld = null!;
    private EcsPool<LeoPosition> _leoMovementPositions = null!;
    private EcsPool<LeoVelocity> _leoMovementVelocities = null!;
    private EcsFilter _leoMovementFilter = null!;
    private EcsWorld _leoFilterWorld = null!;
    private EcsPool<LeoFilterPosition> _leoFilterPositions = null!;
    private EcsPool<LeoFilterVelocity> _leoFilterVelocities = null!;
    private EcsFilter _leoFilterFilter = null!;

    private EcsWorld _leoCreateDestroyWorld = null!;
    private EcsPool<LeoCreatePosition> _leoCreatePositions = null!;
    private EcsPool<LeoCreateVelocity> _leoCreateVelocities = null!;
    private int[] _leoCreateDestroyEntities = [];

    private EcsWorld _leoTransitionWorld = null!;
    private EcsPool<LeoPayload0> _leoTransitionPayload0 = null!;
    private EcsPool<LeoPayload1> _leoTransitionPayload1 = null!;
    private EcsPool<LeoPayload2> _leoTransitionPayload2 = null!;
    private EcsPool<LeoPayload3> _leoTransitionPayload3 = null!;
    private EcsPool<LeoPayload4> _leoTransitionPayload4 = null!;
    private EcsPool<LeoPayload5> _leoTransitionPayload5 = null!;
    private int[] _leoTransitionEntities = [];

    private static readonly QueryAccess s_writeAccess = QueryAccess.Write;
    private static readonly QueryAccess s_readAccess = QueryAccess.Read;

    [GlobalSetup]
    public void Setup()
    {
        if (PayloadRows is not (0 or 2 or 6))
        {
            throw new InvalidOperationException($"Unsupported payload row count: {PayloadRows}.");
        }

        _deltaMovementEntities = new Entity[Amount];
        _deltaFilterEntities = new Entity[Amount];
        _deltaCreateDestroyEntities = new Entity[Amount];
        _deltaTransitionEntities = new Entity[Amount];
        _leoCreateDestroyEntities = new int[Amount];
        _leoTransitionEntities = new int[Amount];

        BuildDeltaMovementWorld();
        BuildDeltaFilterWorld();
        BuildDeltaCreateDestroyWorld();
        BuildDeltaTransitionWorld();

        BuildLeoMovementWorld();
        BuildLeoFilterWorld();
        BuildLeoCreateDestroyWorld();
        BuildLeoTransitionWorld();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DenseMovement")]
    public double DeltaECS_DenseMovement()
    {
        var state = new MovementState();
        using var chunks = _deltaMovementWorld.QueryChunks(in _deltaMovementQuery, s_writeAccess);
        while (chunks.MoveNext())
        {
            var lease = chunks.Current;
            var positions = lease.GetComponentRow<DeltaPosition>(0);
            var velocities = lease.GetComponentRow<DeltaVelocity>(1);
            var slotCount = lease.SlotCount;
            for (var i = slotCount - 1; i >= 0; i--)
            {
                positions[i].X += velocities[i].X * Dt;
                positions[i].Y += velocities[i].Y * Dt;
                state.Count++;
                state.Checksum += positions[i].X + positions[i].Y;
            }
        }

        return BenchmarkGuard.Check(state.Checksum, state.Count, Amount);
    }

    [Benchmark]
    [BenchmarkCategory("DenseMovement")]
    public double LeoEcsLite_DenseMovement()
    {
        var checksum = 0d;
        var count = 0;

        foreach (var entity in _leoMovementFilter)
        {
            ref var position = ref _leoMovementPositions.Get(entity);
            ref var velocity = ref _leoMovementVelocities.Get(entity);
            position.X += velocity.X * Dt;
            position.Y += velocity.Y * Dt;
            count++;
            checksum += position.X + position.Y;
        }

        return BenchmarkGuard.Check(checksum, count, Amount);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CachedQuery")]
    public int DeltaECS_CachedQueryIteration()
    {
        var state = new QueryState();
        using var chunks = _deltaFilterWorld.QueryChunks(in _deltaFilterQuery, s_readAccess);
        while (chunks.MoveNext())
        {
            var lease = chunks.Current;
            var positions = lease.GetComponentRow<DeltaFilterPosition>(0);
            var velocities = lease.GetComponentRow<DeltaFilterVelocity>(1);
            var slotCount = lease.SlotCount;
            for (var i = slotCount - 1; i >= 0; i--)
            {
                state.Count++;
                state.Checksum += positions[i].X + velocities[i].Y;
            }
        }

        return BenchmarkGuard.Check(state.Count, state.Checksum, Amount);
    }

    [Benchmark]
    [BenchmarkCategory("CachedQuery")]
    public int LeoEcsLite_CachedFilterIteration()
    {
        var checksum = 0d;
        var count = 0;

        foreach (var entity in _leoFilterFilter)
        {
            var position = _leoFilterPositions.Get(entity);
            var velocity = _leoFilterVelocities.Get(entity);
            count++;
            checksum += position.X + velocity.Y;
        }

        return BenchmarkGuard.Check(count, checksum, Amount);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateDestroy")]
    public int DeltaECS_BatchCreateAndDestroy()
    {
        var created = _deltaCreateDestroyWorld.CreateBatch(_deltaCreateDestroyComponents, _deltaCreateDestroyEntities);
        if (created != Amount)
        {
            throw new InvalidOperationException($"Expected to create {Amount}, got {created}.");
        }

        var destroyed = _deltaCreateDestroyWorld.DestroyBatch(_deltaCreateDestroyEntities);
        if (destroyed != Amount)
        {
            throw new InvalidOperationException($"Expected to destroy {Amount}, got {destroyed}.");
        }

        return BenchmarkGuard.Check(destroyed, Amount);
    }

    [Benchmark]
    [BenchmarkCategory("CreateDestroy")]
    public int LeoEcsLite_BatchCreateAndDestroy()
    {
        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoCreateDestroyWorld.NewEntity();
            _leoCreateDestroyEntities[i] = entity;

            _leoCreatePositions.Add(entity);
            _leoCreateVelocities.Add(entity);
        }

        for (var i = 0; i < _leoCreateDestroyEntities.Length; i++)
        {
            _leoCreateDestroyWorld.DelEntity(_leoCreateDestroyEntities[i]);
        }

        return BenchmarkGuard.Check(_leoCreateDestroyWorld.GetEntitiesCount(), 0);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Structural")]
    public int DeltaECS_StructuralAddAndRemove()
    {
        _deltaTransitionWorld.AddComponents(_deltaTransitionPayloadRows, _deltaTransitionEntities);

        _deltaTransitionWorld.RemoveComponents(_deltaTransitionPayloadRows, _deltaTransitionEntities);

        return BenchmarkGuard.Check(_deltaTransitionWorld.AliveEntityCount, Amount);
    }

    [Benchmark]
    [BenchmarkCategory("Structural")]
    public int LeoEcsLite_StructuralAddAndRemove()
    {
        AddLeoTransitionPayloads();
        RemoveLeoTransitionPayloads();
        return BenchmarkGuard.Check(_leoTransitionWorld.GetEntitiesCount(), Amount);
    }

    private void AddLeoTransitionPayloads()
    {
        switch (PayloadRows)
        {
            case 0:
                return;
            case 2:
                for (var i = 0; i < _leoTransitionEntities.Length; i++)
                {
                    var entity = _leoTransitionEntities[i];
                    _leoTransitionPayload0.Add(entity);
                    _leoTransitionPayload1.Add(entity);
                }
                return;
            case 6:
                for (var i = 0; i < _leoTransitionEntities.Length; i++)
                {
                    var entity = _leoTransitionEntities[i];
                    _leoTransitionPayload0.Add(entity);
                    _leoTransitionPayload1.Add(entity);
                    _leoTransitionPayload2.Add(entity);
                    _leoTransitionPayload3.Add(entity);
                    _leoTransitionPayload4.Add(entity);
                    _leoTransitionPayload5.Add(entity);
                }
                return;
            default:
                throw new InvalidOperationException($"Unsupported payload row count: {PayloadRows}.");
        }
    }

    private void RemoveLeoTransitionPayloads()
    {
        switch (PayloadRows)
        {
            case 0:
                return;
            case 2:
                for (var i = 0; i < _leoTransitionEntities.Length; i++)
                {
                    var entity = _leoTransitionEntities[i];
                    _leoTransitionPayload0.Del(entity);
                    _leoTransitionPayload1.Del(entity);
                }
                return;
            case 6:
                for (var i = 0; i < _leoTransitionEntities.Length; i++)
                {
                    var entity = _leoTransitionEntities[i];
                    _leoTransitionPayload0.Del(entity);
                    _leoTransitionPayload1.Del(entity);
                    _leoTransitionPayload2.Del(entity);
                    _leoTransitionPayload3.Del(entity);
                    _leoTransitionPayload4.Del(entity);
                    _leoTransitionPayload5.Del(entity);
                }
                return;
            default:
                throw new InvalidOperationException($"Unsupported payload row count: {PayloadRows}.");
        }
    }

    private void BuildDeltaMovementWorld()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register<DeltaPosition>(new SchemaId(70_000));
        _deltaVelocity = layouts.Register<DeltaVelocity>(new SchemaId(70_001));
        _deltaMovementPayload = BuildPayloadComponentIds(layouts, 70_002);
        var components = MergeComponents(_deltaPosition, _deltaVelocity, _deltaMovementPayload, PayloadRows);

        _deltaMovementWorld = new World(layouts, initialEntityCapacity: Amount);
        _deltaMovementWorld.CreateBatch(components, _deltaMovementEntities);

        for (var entityIndex = 0; entityIndex < _deltaMovementEntities.Length; entityIndex++)
        {
            var entity = _deltaMovementEntities[entityIndex];
            _deltaMovementWorld.SetComponent(entity, _deltaPosition, new DeltaPosition { X = 1f, Y = 2f });
            _deltaMovementWorld.SetComponent(entity, _deltaVelocity, new DeltaVelocity { X = 3f, Y = 4f });

            for (var payloadIndex = 0; payloadIndex < PayloadRows; payloadIndex++)
            {
                _deltaMovementWorld.SetComponent(entity, components[2 + payloadIndex], new DeltaPayload { Value = entityIndex });
            }
        }

        var queryDescription = QueryDescription.ForComponents(components);
        _deltaMovementQuery = _deltaMovementWorld.CreateQuery(in queryDescription);
    }

    private void BuildDeltaFilterWorld()
    {
        var layouts = new ComponentLayoutRegistry();
        var position = layouts.Register<DeltaFilterPosition>(new SchemaId(71_000));
        var velocity = layouts.Register<DeltaFilterVelocity>(new SchemaId(71_001));
        _deltaFilterPayload = BuildPayloadComponentIds(layouts, 71_002);
        var components = MergeComponents(position, velocity, _deltaFilterPayload, PayloadRows);

        _deltaFilterWorld = new World(layouts, initialEntityCapacity: Amount);
        var queryDescription = QueryDescription.ForComponents(components);
        _deltaFilterQuery = _deltaFilterWorld.CreateQuery(in queryDescription);
        _deltaFilterWorld.CreateBatch(components, _deltaFilterEntities);

        for (var entityIndex = 0; entityIndex < _deltaFilterEntities.Length; entityIndex++)
        {
            var entity = _deltaFilterEntities[entityIndex];
            _deltaFilterWorld.SetComponent(entity, position, new DeltaFilterPosition { X = 1f, Y = 2f });
            _deltaFilterWorld.SetComponent(entity, velocity, new DeltaFilterVelocity { X = 3f, Y = 4f });

            for (var payloadIndex = 0; payloadIndex < PayloadRows; payloadIndex++)
            {
                _deltaFilterWorld.SetComponent(entity, components[2 + payloadIndex], new DeltaPayload { Value = entityIndex });
            }
        }
    }

    private void BuildDeltaCreateDestroyWorld()
    {
        var layouts = new ComponentLayoutRegistry();
        var position = layouts.Register<DeltaCreatePosition>(new SchemaId(72_000));
        var velocity = layouts.Register<DeltaCreateVelocity>(new SchemaId(72_001));

        _deltaCreateDestroyComponents = new[] { position, velocity };
        _deltaCreateDestroyWorld = new World(layouts, initialEntityCapacity: Amount);
    }

    private void BuildDeltaTransitionWorld()
    {
        var layouts = new ComponentLayoutRegistry();
        var position = layouts.Register<DeltaTransitionPosition>(new SchemaId(73_000));
        var velocity = layouts.Register<DeltaTransitionVelocity>(new SchemaId(73_001));
        _deltaTransitionPayload = BuildPayloadComponentIds(layouts, 73_002);
        _deltaTransitionPayloadRows = Slice(_deltaTransitionPayload, PayloadRows);
        _deltaTransitionWorld = new World(layouts, initialEntityCapacity: Amount);

        var baseComponents = new[] { position, velocity };
        _deltaTransitionWorld.CreateBatch(baseComponents, _deltaTransitionEntities);
        for (var i = 0; i < _deltaTransitionEntities.Length; i++)
        {
            var entity = _deltaTransitionEntities[i];
            _deltaTransitionWorld.SetComponent(entity, position, new DeltaTransitionPosition { X = 1f, Y = 2f });
            _deltaTransitionWorld.SetComponent(entity, velocity, new DeltaTransitionVelocity { X = 3f, Y = 4f });
        }
    }

    private void BuildLeoMovementWorld()
    {
        _leoMovementWorld = new EcsWorld();
        _leoMovementPositions = _leoMovementWorld.GetPool<LeoPosition>();
        _leoMovementVelocities = _leoMovementWorld.GetPool<LeoVelocity>();
        _leoMovementFilter = BuildLeoMovementFilter(_leoMovementWorld, PayloadRows);

        var payload0 = _leoMovementWorld.GetPool<LeoPayload0>();
        var payload1 = _leoMovementWorld.GetPool<LeoPayload1>();
        var payload2 = _leoMovementWorld.GetPool<LeoPayload2>();
        var payload3 = _leoMovementWorld.GetPool<LeoPayload3>();
        var payload4 = _leoMovementWorld.GetPool<LeoPayload4>();
        var payload5 = _leoMovementWorld.GetPool<LeoPayload5>();

        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoMovementWorld.NewEntity();

            ref var position = ref _leoMovementPositions.Add(entity);
            position = new LeoPosition { X = 1f, Y = 2f };

            ref var velocity = ref _leoMovementVelocities.Add(entity);
            velocity = new LeoVelocity { X = 3f, Y = 4f };

            for (var payloadIndex = 0; payloadIndex < PayloadRows; payloadIndex++)
            {
                AddLeoPayload(payload0, payload1, payload2, payload3, payload4, payload5, payloadIndex, entity, seed: i);
            }
        }
    }

    private void BuildLeoFilterWorld()
    {
        _leoFilterWorld = new EcsWorld();
        _leoFilterPositions = _leoFilterWorld.GetPool<LeoFilterPosition>();
        _leoFilterVelocities = _leoFilterWorld.GetPool<LeoFilterVelocity>();
        _leoFilterFilter = BuildLeoFilterFilter(_leoFilterWorld, PayloadRows);

        var payload0 = _leoFilterWorld.GetPool<LeoPayload0>();
        var payload1 = _leoFilterWorld.GetPool<LeoPayload1>();
        var payload2 = _leoFilterWorld.GetPool<LeoPayload2>();
        var payload3 = _leoFilterWorld.GetPool<LeoPayload3>();
        var payload4 = _leoFilterWorld.GetPool<LeoPayload4>();
        var payload5 = _leoFilterWorld.GetPool<LeoPayload5>();

        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoFilterWorld.NewEntity();
            ref var position = ref _leoFilterPositions.Add(entity);
            position = new LeoFilterPosition { X = 1f, Y = 2f };

            ref var velocity = ref _leoFilterVelocities.Add(entity);
            velocity = new LeoFilterVelocity { X = 3f, Y = 4f };

            for (var payloadIndex = 0; payloadIndex < PayloadRows; payloadIndex++)
            {
                AddLeoPayload(payload0, payload1, payload2, payload3, payload4, payload5, payloadIndex, entity, seed: i);
            }
        }
    }

    private void BuildLeoCreateDestroyWorld()
    {
        _leoCreateDestroyWorld = new EcsWorld();
        _leoCreatePositions = _leoCreateDestroyWorld.GetPool<LeoCreatePosition>();
        _leoCreateVelocities = _leoCreateDestroyWorld.GetPool<LeoCreateVelocity>();
    }

    private void BuildLeoTransitionWorld()
    {
        _leoTransitionWorld = new EcsWorld();
        var positionPool = _leoTransitionWorld.GetPool<LeoTransitionPosition>();
        var velocityPool = _leoTransitionWorld.GetPool<LeoTransitionVelocity>();
        _leoTransitionPayload0 = _leoTransitionWorld.GetPool<LeoPayload0>();
        _leoTransitionPayload1 = _leoTransitionWorld.GetPool<LeoPayload1>();
        _leoTransitionPayload2 = _leoTransitionWorld.GetPool<LeoPayload2>();
        _leoTransitionPayload3 = _leoTransitionWorld.GetPool<LeoPayload3>();
        _leoTransitionPayload4 = _leoTransitionWorld.GetPool<LeoPayload4>();
        _leoTransitionPayload5 = _leoTransitionWorld.GetPool<LeoPayload5>();

        for (var i = 0; i < Amount; i++)
        {
            var entity = _leoTransitionWorld.NewEntity();
            _leoTransitionEntities[i] = entity;

            ref var position = ref positionPool.Add(entity);
            position = new LeoTransitionPosition { X = 1f, Y = 2f };

            ref var velocity = ref velocityPool.Add(entity);
            velocity = new LeoTransitionVelocity { X = 3f, Y = 4f };
        }
    }

    private EcsFilter BuildLeoMovementFilter(EcsWorld world, int payloadRows)
    {
        var filter = world.Filter<LeoPosition>().Inc<LeoVelocity>();

        if (payloadRows > 0)
        {
            filter = filter.Inc<LeoPayload0>();
        }

        if (payloadRows > 1)
        {
            filter = filter.Inc<LeoPayload1>();
        }

        if (payloadRows > 2)
        {
            filter = filter.Inc<LeoPayload2>();
        }

        if (payloadRows > 3)
        {
            filter = filter.Inc<LeoPayload3>();
        }

        if (payloadRows > 4)
        {
            filter = filter.Inc<LeoPayload4>();
        }

        if (payloadRows > 5)
        {
            filter = filter.Inc<LeoPayload5>();
        }

        return filter.End(Amount);
    }

    private EcsFilter BuildLeoFilterFilter(EcsWorld world, int payloadRows)
    {
        var filter = world.Filter<LeoFilterPosition>().Inc<LeoFilterVelocity>();

        if (payloadRows > 0)
        {
            filter = filter.Inc<LeoPayload0>();
        }

        if (payloadRows > 1)
        {
            filter = filter.Inc<LeoPayload1>();
        }

        if (payloadRows > 2)
        {
            filter = filter.Inc<LeoPayload2>();
        }

        if (payloadRows > 3)
        {
            filter = filter.Inc<LeoPayload3>();
        }

        if (payloadRows > 4)
        {
            filter = filter.Inc<LeoPayload4>();
        }

        if (payloadRows > 5)
        {
            filter = filter.Inc<LeoPayload5>();
        }

        return filter.End(Amount);
    }

    private static void AddLeoPayload(
        EcsPool<LeoPayload0> payload0,
        EcsPool<LeoPayload1> payload1,
        EcsPool<LeoPayload2> payload2,
        EcsPool<LeoPayload3> payload3,
        EcsPool<LeoPayload4> payload4,
        EcsPool<LeoPayload5> payload5,
        int payloadIndex,
        int entity,
        int seed)
    {
        switch (payloadIndex)
        {
            case 0:
                payload0.Add(entity) = new LeoPayload0 { Value = seed };
                break;
            case 1:
                payload1.Add(entity) = new LeoPayload1 { Value = seed };
                break;
            case 2:
                payload2.Add(entity) = new LeoPayload2 { Value = seed };
                break;
            case 3:
                payload3.Add(entity) = new LeoPayload3 { Value = seed };
                break;
            case 4:
                payload4.Add(entity) = new LeoPayload4 { Value = seed };
                break;
            case 5:
                payload5.Add(entity) = new LeoPayload5 { Value = seed };
                break;
            default:
                throw new InvalidOperationException("Unexpected payload row index.");
        }
    }

    private static void RemoveLeoPayload(
        EcsPool<LeoPayload0> payload0,
        EcsPool<LeoPayload1> payload1,
        EcsPool<LeoPayload2> payload2,
        EcsPool<LeoPayload3> payload3,
        EcsPool<LeoPayload4> payload4,
        EcsPool<LeoPayload5> payload5,
        int payloadIndex,
        int entity)
    {
        switch (payloadIndex)
        {
            case 0:
                payload0.Del(entity);
                break;
            case 1:
                payload1.Del(entity);
                break;
            case 2:
                payload2.Del(entity);
                break;
            case 3:
                payload3.Del(entity);
                break;
            case 4:
                payload4.Del(entity);
                break;
            case 5:
                payload5.Del(entity);
                break;
            default:
                throw new InvalidOperationException("Unexpected payload row index.");
        }
    }

    private static ComponentId[] BuildPayloadComponentIds(ComponentLayoutRegistry layouts, ulong baseSchemaId)
    {
        return new[]
        {
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId)),
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId + 1)),
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId + 2)),
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId + 3)),
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId + 4)),
            layouts.Register<DeltaPayload>(new SchemaId(baseSchemaId + 5))
        };
    }

    private static ComponentId[] MergeComponents(ComponentId first, ComponentId second, ComponentId[] payload, int payloadRows)
    {
        var result = new ComponentId[2 + payloadRows];
        result[0] = first;
        result[1] = second;

        if (payloadRows > 0)
        {
            for (var i = 0; i < payloadRows; i++)
            {
                result[2 + i] = payload[i];
            }
        }

        return result;
    }

    private static ComponentId[] Slice(ComponentId[] payload, int payloadRows)
    {
        if (payloadRows <= 0)
        {
            return Array.Empty<ComponentId>();
        }

        var result = new ComponentId[payloadRows];
        Array.Copy(payload, 0, result, 0, payloadRows);
        return result;
    }

    private struct MovementState
    {
        public int Count;
        public double Checksum;
    }

    private struct QueryState
    {
        public int Count;
        public double Checksum;
    }

    private static class BenchmarkGuard
    {
        public static double Check(double checksum, int count, int expectedCount)
        {
            if (count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} iterations, got {count}.");
            }

            if (double.IsNaN(checksum) || double.IsInfinity(checksum))
            {
                throw new InvalidOperationException("Benchmark checksum is not finite.");
            }

            return checksum;
        }

        public static int Check(int count, double checksum, int expectedCount)
        {
            Check(checksum, count, expectedCount);
            return count;
        }

        public static int Check(int count, int expectedCount)
        {
            if (count != expectedCount)
            {
                throw new InvalidOperationException($"Expected {expectedCount} entities, got {count}.");
            }

            return count;
        }
    }

    private struct DeltaPosition
    {
        public float X;
        public float Y;
    }

    private struct DeltaVelocity
    {
        public float X;
        public float Y;
    }

    private struct DeltaPayload
    {
        public int Value;
    }

    private struct DeltaFilterPosition
    {
        public float X;
        public float Y;
    }

    private struct DeltaFilterVelocity
    {
        public float X;
        public float Y;
    }

#pragma warning disable CS0649 // Structural benchmark components are intentionally default-initialized.
    private struct DeltaCreatePosition
    {
        public float X;
        public float Y;
    }

    private struct DeltaCreateVelocity
    {
        public float X;
        public float Y;
    }

    private struct DeltaTransitionPosition
    {
        public float X;
        public float Y;
    }

    private struct DeltaTransitionVelocity
    {
        public float X;
        public float Y;
    }

    private struct LeoPosition
    {
        public float X;
        public float Y;
    }

    private struct LeoVelocity
    {
        public float X;
        public float Y;
    }

    private struct LeoPayload0 { public int Value; }
    private struct LeoPayload1 { public int Value; }
    private struct LeoPayload2 { public int Value; }
    private struct LeoPayload3 { public int Value; }
    private struct LeoPayload4 { public int Value; }
    private struct LeoPayload5 { public int Value; }

    private struct LeoFilterPosition
    {
        public float X;
        public float Y;
    }

    private struct LeoFilterVelocity
    {
        public float X;
        public float Y;
    }

    private struct LeoCreatePosition
    {
        public float X;
        public float Y;
    }

    private struct LeoCreateVelocity
    {
        public float X;
        public float Y;
    }

    private struct LeoTransitionPosition
    {
        public float X;
        public float Y;
    }

    private struct LeoTransitionVelocity
    {
        public float X;
        public float Y;
    }
#pragma warning restore CS0649
}
