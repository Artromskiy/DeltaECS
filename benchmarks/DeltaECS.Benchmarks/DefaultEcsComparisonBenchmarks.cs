using System;
using BenchmarkDotNet.Attributes;
using DefaultEcs.Command;
using DefaultEcs;
using DVG.ECS;

using DVGEntity = DVG.ECS.Entity;

namespace DVG.ECS.Benchmarks;

/// <summary>
/// Fairness-focused comparison of dense movement and structural workloads between DVG.ECS and DefaultEcs.
/// Setups are done once in <see cref="GlobalSetup"/>; only hot loop work is measured.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class DefaultEcsComparisonBenchmarks
{
    private const float Dt = 1.0f / 60.0f;

    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(0, 2, 6)]
    public int PayloadRows { get; set; }

    private World _dvgMovementWorld = null!;
    private ComponentId _dvgPosition;
    private ComponentId _dvgVelocity;
    private ComponentId[] _dvgPayloads = Array.Empty<ComponentId>();
    private ComponentId[] _dvgMovementComponents = Array.Empty<ComponentId>();
    private QueryHandle _dvgMovementQuery;
    private DVGEntity[] _dvgMovementEntities = Array.Empty<DVGEntity>();
    private ComponentId[] _dvgTransitionComponents = Array.Empty<ComponentId>();

    private DefaultEcs.World _defaultMovementWorld = null!;
    private DefaultEcs.Entity[] _defaultMovementEntities = Array.Empty<DefaultEcs.Entity>();
    private DefaultEcs.World _defaultBatchWorld = null!;
    private DefaultEcs.Entity[] _defaultBatchEntities = Array.Empty<DefaultEcs.Entity>();
    private DefaultEcs.World _defaultTransitionWorld = null!;
    private DefaultEcs.Entity[] _defaultTransitionEntities = Array.Empty<DefaultEcs.Entity>();
    private EntityCommandRecorder _defaultTransitionRecorder = null!;

    private World _dvgBatchWorld = null!;
    private ComponentId[] _dvgBatchComponents = Array.Empty<ComponentId>();
    private DVGEntity[] _dvgBatchEntities = Array.Empty<DVGEntity>();

    private World _dvgTransitionWorld = null!;
    private DVGEntity[] _dvgTransitionEntities = Array.Empty<DVGEntity>();

    [GlobalSetup]
    public void Setup()
    {
        SetupMovementDvg();
        SetupMovementDefault();
        SetupBatchDvg();
        SetupBatchDefault();
        SetupTransitionDvg();
        SetupTransitionDefault();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // DefaultEcs owns its component stores and must be disposed after each
        // parameter combination. DVG.ECS has no world-level IDisposable API.
        _defaultMovementWorld?.Dispose();
        _defaultBatchWorld?.Dispose();
        _defaultTransitionWorld?.Dispose();
        _defaultTransitionRecorder?.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DenseMovement")]
    public double DeltaECS_Movement_PositionVelocity()
    {
        var state = new MovementState { Count = 0, ExpectedCount = Amount, Dt = Dt };
        _dvgMovementWorld.Query(in _dvgMovementQuery, QueryAccess.Write, ref state, static (ref MovementState s, ref DenseChunkLeaseView lease) =>
        {
            var positions = lease.GetComponentRow<MovementPosition>(0);
            var velocities = lease.GetComponentRow<MovementVelocity>(1);
            var slotCount = lease.SlotCount;
            for (var i = slotCount - 1; i >= 0; i--)
            {
                positions[i].X += velocities[i].X * s.Dt;
                positions[i].Y += velocities[i].Y * s.Dt;
                s.Count++;
                s.Checksum += positions[i].X + positions[i].Y;
            }
        });

        return BenchmarkGuard.Checksum(state.Count, state.ExpectedCount, state.Checksum);
    }

    [Benchmark]
    [BenchmarkCategory("DenseMovement")]
    public double DefaultEcs_Movement_PositionVelocity()
    {
        var state = new MovementState { Count = 0, ExpectedCount = Amount, Dt = Dt };
        for (var i = 0; i < _defaultMovementEntities.Length; i++)
        {
            ref var entity = ref _defaultMovementEntities[i];
            ref var position = ref entity.Get<MovementPosition>();
            ref var velocity = ref entity.Get<MovementVelocity>();
            position.X += velocity.X * state.Dt;
            position.Y += velocity.Y * state.Dt;
            state.Count++;
            state.Checksum += position.X + position.Y;
        }

        return BenchmarkGuard.Checksum(state.Count, state.ExpectedCount, state.Checksum);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CreateDestroy")]
    public int DeltaECS_Batch_CreateDestroy()
    {
        var created = _dvgBatchWorld.CreateBatch(_dvgBatchComponents, _dvgBatchEntities);
        var destroyed = _dvgBatchWorld.DestroyBatch(_dvgBatchEntities);
        if (created != Amount || destroyed != Amount || _dvgBatchWorld.AliveEntityCount != 0)
        {
            throw new InvalidOperationException($"DVG batch lifecycle mismatch: created={created}, destroyed={destroyed}, alive={_dvgBatchWorld.AliveEntityCount}.");
        }

        return created + destroyed;
    }

    [Benchmark]
    [BenchmarkCategory("CreateDestroy")]
    public int DefaultEcs_Batch_CreateDestroy()
    {
        var created = 0;
        for (var i = 0; i < _defaultBatchEntities.Length; i++)
        {
            _defaultBatchEntities[i] = _defaultBatchWorld.CreateEntity();
            // DefaultEcs creates an empty entity; adding the two components is
            // the corresponding operation to DVG's two-component CreateBatch.
            _defaultBatchEntities[i].Set<BatchValue>();
            _defaultBatchEntities[i].Set<BatchVelocity>();
            created++;
        }

        var destroyed = 0;
        for (var i = 0; i < _defaultBatchEntities.Length; i++)
        {
            _defaultBatchEntities[i].Dispose();
            if (_defaultBatchEntities[i].IsAlive)
            {
                throw new InvalidOperationException("DefaultEcs entity remained alive after Dispose.");
            }

            destroyed++;
        }

        if (created != Amount || destroyed != Amount)
        {
            throw new InvalidOperationException($"DefaultEcs batch lifecycle mismatch: created={created}, destroyed={destroyed}.");
        }

        return created + destroyed;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Structural")]
    public int DeltaECS_Batch_AddRemoveTransition()
    {
        _dvgTransitionWorld.AddComponents(_dvgTransitionComponents, _dvgTransitionEntities);

        if (!_dvgTransitionWorld.TryGetComponent(_dvgTransitionEntities[0], _dvgTransitionComponents[0], out TransitionPayload _))
        {
            throw new InvalidOperationException("DVG transition add did not produce the payload component.");
        }

        _dvgTransitionWorld.RemoveComponents(_dvgTransitionComponents, _dvgTransitionEntities);

        if (_dvgTransitionWorld.TryGetComponent(_dvgTransitionEntities[0], _dvgTransitionComponents[0], out TransitionPayload _))
        {
            throw new InvalidOperationException("DVG transition remove left the payload component present.");
        }

        return Amount * 2;
    }

    [Benchmark]
    [BenchmarkCategory("Structural")]
    public int DefaultEcs_Batch_AddRemoveTransition()
    {
        for (var i = 0; i < _defaultTransitionEntities.Length; i++)
        {
            var record = _defaultTransitionRecorder.Record(_defaultTransitionEntities[i]);
            record.Set<TransitionPayload>();
        }

        _defaultTransitionRecorder.Execute();

        if (!_defaultTransitionEntities[0].Has<TransitionPayload>())
        {
            throw new InvalidOperationException("DefaultEcs transition add did not produce the payload component.");
        }

        for (var i = 0; i < _defaultTransitionEntities.Length; i++)
        {
            var record = _defaultTransitionRecorder.Record(_defaultTransitionEntities[i]);
            record.Remove<TransitionPayload>();
        }

        _defaultTransitionRecorder.Execute();

        if (_defaultTransitionEntities[0].Has<TransitionPayload>())
        {
            throw new InvalidOperationException("DefaultEcs transition remove left the payload component present.");
        }

        return Amount * 2;
    }

    private void SetupMovementDvg()
    {
        var layouts = new ComponentLayoutRegistry();
        _dvgPosition = layouts.Register<MovementPosition>(new SchemaId(130_000));
        _dvgVelocity = layouts.Register<MovementVelocity>(new SchemaId(130_001));
        _dvgPayloads = new ComponentId[PayloadRows];
        for (var i = 0; i < PayloadRows; i++)
        {
            _dvgPayloads[i] = RegisterPayload(layouts, i);
        }

        _dvgMovementComponents = new ComponentId[2 + PayloadRows];
        _dvgMovementComponents[0] = _dvgPosition;
        _dvgMovementComponents[1] = _dvgVelocity;
        for (var i = 0; i < PayloadRows; i++)
        {
            _dvgMovementComponents[2 + i] = _dvgPayloads[i];
        }

        _dvgMovementWorld = new World(layouts, initialEntityCapacity: Amount);
        _dvgMovementEntities = new DVGEntity[Amount];
        _dvgMovementWorld.CreateBatch(_dvgMovementComponents, _dvgMovementEntities);

        for (var i = 0; i < _dvgMovementEntities.Length; i++)
        {
            _dvgMovementWorld.SetComponent(_dvgMovementEntities[i], _dvgPosition, new MovementPosition { X = 1f, Y = 2f });
            _dvgMovementWorld.SetComponent(_dvgMovementEntities[i], _dvgVelocity, new MovementVelocity { X = 3f, Y = 4f });
            for (var payloadIndex = 0; payloadIndex < PayloadRows; payloadIndex++)
            {
                _dvgMovementWorld.SetComponent(_dvgMovementEntities[i], _dvgPayloads[payloadIndex], new MovementPayload());
            }
        }

        var queryDescription = QueryDescription.ForComponents(_dvgMovementComponents);
        _dvgMovementQuery = _dvgMovementWorld.CreateQuery(in queryDescription);
    }

    private void SetupMovementDefault()
    {
        _defaultMovementWorld = new DefaultEcs.World(Amount);
        _defaultMovementEntities = new DefaultEcs.Entity[Amount];

        for (var i = 0; i < Amount; i++)
        {
            var entity = _defaultMovementWorld.CreateEntity();
            entity.Set(new MovementPosition { X = 1f, Y = 2f });
            entity.Set(new MovementVelocity { X = 3f, Y = 4f });

            switch (PayloadRows)
            {
                case 6:
                    entity.Set<MovementPayload0>();
                    entity.Set<MovementPayload1>();
                    entity.Set<MovementPayload2>();
                    entity.Set<MovementPayload3>();
                    entity.Set<MovementPayload4>();
                    entity.Set<MovementPayload5>();
                    break;
                case 2:
                    entity.Set<MovementPayload0>();
                    entity.Set<MovementPayload1>();
                    break;
                case 0:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(PayloadRows), PayloadRows, "Only payload rows 0, 2, and 6 are supported.");
            }

            _defaultMovementEntities[i] = entity;
        }
    }

    private void SetupBatchDvg()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register<BatchValue>(new SchemaId(130_101));
        var second = layouts.Register<BatchValue>(new SchemaId(130_102));
        _dvgBatchWorld = new World(layouts, initialEntityCapacity: Amount);
        _dvgBatchComponents = new[] { first, second };
        _dvgBatchEntities = new DVGEntity[Amount];
    }

    private void SetupBatchDefault()
    {
        _defaultBatchWorld = new DefaultEcs.World(Amount);
        _defaultBatchEntities = new DefaultEcs.Entity[Amount];
    }

    private void SetupTransitionDvg()
    {
        var layouts = new ComponentLayoutRegistry();
        var baseComponent = layouts.Register<TransitionBase>(new SchemaId(130_201));
        var transitionPayload = layouts.Register<TransitionPayload>(new SchemaId(130_202));
        _dvgTransitionWorld = new World(layouts, initialEntityCapacity: Amount);
        _dvgTransitionEntities = new DVGEntity[Amount];
        _dvgTransitionWorld.CreateBatch(new[] { baseComponent }, _dvgTransitionEntities);
        _dvgTransitionComponents = new[] { transitionPayload };
        for (var i = 0; i < Amount; i++)
        {
            _dvgTransitionWorld.SetComponent(_dvgTransitionEntities[i], baseComponent, new TransitionBase { A = 1 });
        }
    }

    private void SetupTransitionDefault()
    {
        _defaultTransitionWorld = new DefaultEcs.World(Amount);
        // Match DVG's queue + playback lifecycle. The generous fixed command
        // budget keeps recorder growth/allocation out of the measured loop.
        _defaultTransitionRecorder = new EntityCommandRecorder(Amount * 64);
        _defaultTransitionEntities = new DefaultEcs.Entity[Amount];
        for (var i = 0; i < Amount; i++)
        {
            _defaultTransitionEntities[i] = _defaultTransitionWorld.CreateEntity();
            _defaultTransitionEntities[i].Set(new TransitionBase { A = 1 });
        }
    }

    private static ComponentId RegisterPayload(ComponentLayoutRegistry layouts, int index)
    {
        return layouts.Register<MovementPayload>(new SchemaId((ulong)(130_300 + index)));
    }

    private struct MovementState
    {
        public int Count;
        public int ExpectedCount;
        public float Dt;
        public double Checksum;
    }

    private struct MovementPosition
    {
        public float X;
        public float Y;
    }

    private struct MovementVelocity
    {
        public float X;
        public float Y;
    }

#pragma warning disable CS0649 // Benchmark payload fields are intentionally default-initialized.
    private struct MovementPayload
    {
        public int Value;
    }

    private struct MovementPayload1
    {
        public int Value;
    }

    private struct MovementPayload0
    {
        public int Value;
    }

    private struct MovementPayload2
    {
        public int Value;
    }

    private struct MovementPayload3
    {
        public int Value;
    }

    private struct MovementPayload4
    {
        public int Value;
    }

    private struct MovementPayload5
    {
        public int Value;
    }

    private struct BatchValue
    {
        public long A;
        public long B;
    }

    private struct BatchVelocity
    {
        public long A;
        public long B;
    }

    private struct TransitionPayload
    {
        public int Value;
    }

    private struct TransitionBase
    {
        public int A;
    }
#pragma warning restore CS0649
}

public static class BenchmarkGuard
{
    public static double Checksum(int touched, int expected, double checksum)
    {
        if (touched != expected)
        {
            throw new InvalidOperationException($"Benchmark touched {touched} entities, expected {expected}.");
        }

        if (double.IsNaN(checksum) || double.IsInfinity(checksum))
        {
            throw new InvalidOperationException("Benchmark checksum is invalid.");
        }

        return checksum;
    }
}
