using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Friflo.Engine.ECS;
using FrifloEntity = Friflo.Engine.ECS.Entity;

namespace Delta.ECS.Benchmarks;

/// <summary>
/// Position += Velocity * dt with the same two hot components and optional
/// payload rows in each backend. Payload rows are deliberately present in the
/// archetype but are not touched by the system, matching a system that reads
/// only the components it declares.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class AlgorithmicMovementBenchmarks
{
    private const float Dt = 1.0f / 60.0f;

    [Params(10_000, 100_000)]
    public int Amount { get; set; }

    [Params(0, 2, 6)]
    public int PayloadRows { get; set; }

    private World _deltaWorld = null!;
    private ComponentId _deltaPosition;
    private ComponentId _deltaVelocity;
    private ComponentId[] _deltaComponents = Array.Empty<ComponentId>();
    private Query _deltaQuery;
    private WriteAccess _deltaPositionBinding;
    private ReadAccess _deltaVelocityBinding;
    private LegacyMovementReference _legacy = null!;

    private Arch.Core.World _archWorld = null!;
    private Arch.Core.QueryDescription _archQuery;
    private Arch.Core.Utils.ComponentType[] _archComponents = Array.Empty<Arch.Core.Utils.ComponentType>();

    private EntityStore _frifloWorld = null!;
    private ArchetypeQuery<FrifloPosition, FrifloVelocity> _frifloQuery = null!;

    [GlobalSetup]
    public void Setup()
    {
        SetupDelta();
        SetupArch();
        SetupFriflo();
        _legacy = new LegacyMovementReference(Amount, PayloadRows);
    }

    [Benchmark(Baseline = true)]
    public double DeltaECS_Movement()
    {
        double checksum = 0;
        var count = 0;
        using var scope = _deltaWorld.OpenQuery(in _deltaQuery);
        var positionAccess = scope.Bind(_deltaPositionBinding);
        var velocityAccess = scope.Bind(_deltaVelocityBinding);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var positions = slots.GetRow(positionAccess);
                var velocities = slots.GetRow(velocityAccess);
                while (slots.MoveNext())
                {
                    ref var position = ref positions.Ref<DeltaPosition>(slots);
                    ref readonly var velocity = ref velocities.Ref<DeltaVelocity>(slots);
                    position.X += velocity.X * Dt;
                    position.Y += velocity.Y * Dt;
                    count++;
                    checksum += position.X + position.Y;
                }
            }
        }

        return MovementGuard.Checksum(checksum, count, Amount);
    }

    [Benchmark]
    public double Arch_Movement()
    {
        s_archChecksum = 0;
        s_archCount = 0;
        switch (PayloadRows)
        {
            case 0:
                _archWorld.Query(_archQuery, static (ref ArchPosition position, ref ArchVelocity velocity) => UpdateArch(ref position, ref velocity));
                break;
            case 2:
                _archWorld.Query(_archQuery, static (ref ArchPosition position, ref ArchVelocity velocity) => UpdateArch(ref position, ref velocity));
                break;
            case 6:
                _archWorld.Query(_archQuery, static (ref ArchPosition position, ref ArchVelocity velocity) => UpdateArch(ref position, ref velocity));
                break;
            default:
                throw new InvalidOperationException($"Unsupported payload row count: {PayloadRows}");
        }

        return MovementGuard.Checksum(s_archChecksum, s_archCount, Amount);
    }

    [Benchmark]
    public double Friflo_Movement()
    {
        s_frifloChecksum = 0;
        s_frifloCount = 0;
        _frifloQuery.ForEachEntity(static (ref FrifloPosition position, ref FrifloVelocity velocity, FrifloEntity _)
            => UpdateFriflo(ref position, ref velocity));
        return MovementGuard.Checksum(s_frifloChecksum, s_frifloCount, Amount);
    }

    [Benchmark]
    public double Legacy_ByteMovement()
    {
        return MovementGuard.Checksum(_legacy.Iterate(Dt), Amount, Amount);
    }

    private void SetupDelta()
    {
        var layouts = new ComponentLayoutRegistry();
        _deltaPosition = layouts.Register(typeof(DeltaPosition), new SchemaId(60_000));
        _deltaVelocity = layouts.Register(typeof(DeltaVelocity), new SchemaId(60_001));
        _deltaComponents = new ComponentId[2 + PayloadRows];
        _deltaComponents[0] = _deltaPosition;
        _deltaComponents[1] = _deltaVelocity;
        for (var i = 0; i < PayloadRows; i++)
        {
            _deltaComponents[2 + i] = RegisterDeltaPayload(layouts, i);
        }

        _deltaWorld = new World(layouts, initialEntityCapacity: Amount);
        var entities = new Entity[Amount];
        _deltaWorld.Create(_deltaComponents, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            _deltaWorld.Set(entities[i], _deltaPosition, new DeltaPosition { X = 1, Y = 2 });
            _deltaWorld.Set(entities[i], _deltaVelocity, new DeltaVelocity { X = 3, Y = 4 });
            SetDeltaPayload(entities[i], i);
        }

        var spec = QuerySpec.ForComponents(_deltaComponents);
        _deltaQuery = _deltaWorld.CreateQuery(in spec);
        _deltaPositionBinding = _deltaQuery.AccessWrite(_deltaPosition);
        _deltaVelocityBinding = _deltaQuery.AccessRead(_deltaVelocity);
    }

    private void SetupArch()
    {
        _archWorld = Arch.Core.World.Create();
        _archComponents = new Arch.Core.Utils.ComponentType[2 + PayloadRows];
        _archComponents[0] = typeof(ArchPosition);
        _archComponents[1] = typeof(ArchVelocity);
        for (var i = 0; i < PayloadRows; i++)
        {
            _archComponents[2 + i] = i switch
            {
                0 => typeof(ArchPayload0),
                1 => typeof(ArchPayload1),
                2 => typeof(ArchPayload2),
                3 => typeof(ArchPayload3),
                4 => typeof(ArchPayload4),
                _ => typeof(ArchPayload5)
            };
        }

        _archQuery = new Arch.Core.QueryDescription { All = _archComponents };
        _archWorld.Reserve(_archComponents, Amount);
        for (var i = 0; i < Amount; i++)
        {
            var entity = _archWorld.Create(_archComponents);
            _archWorld.Set(entity, new ArchPosition { X = 1, Y = 2 });
            _archWorld.Set(entity, new ArchVelocity { X = 3, Y = 4 });
            SetArchPayload(entity, i);
        }
    }

    private void SetupFriflo()
    {
        _frifloWorld = new EntityStore();
        for (var i = 0; i < Amount; i++)
        {
            var entity = _frifloWorld.CreateEntity(new FrifloPosition { X = 1, Y = 2 }, new FrifloVelocity { X = 3, Y = 4 });
            SetFrifloPayload(entity, i);
        }

        _frifloQuery = _frifloWorld.Query<FrifloPosition, FrifloVelocity>();
    }

    private void SetArchPayload(Arch.Core.Entity entity, int seed)
    {
        for (var i = 0; i < PayloadRows; i++)
        {
            switch (i)
            {
                case 0: _archWorld.Set(entity, new ArchPayload0 { Value = seed }); break;
                case 1: _archWorld.Set(entity, new ArchPayload1 { Value = seed }); break;
                case 2: _archWorld.Set(entity, new ArchPayload2 { Value = seed }); break;
                case 3: _archWorld.Set(entity, new ArchPayload3 { Value = seed }); break;
                case 4: _archWorld.Set(entity, new ArchPayload4 { Value = seed }); break;
                case 5: _archWorld.Set(entity, new ArchPayload5 { Value = seed }); break;
            }
        }
    }

    private static ComponentId RegisterDeltaPayload(ComponentLayoutRegistry layouts, int index) => index switch
    {
        0 => layouts.Register(typeof(DeltaPayload), new SchemaId(60_010)),
        1 => layouts.Register(typeof(DeltaPayload), new SchemaId(60_011)),
        2 => layouts.Register(typeof(DeltaPayload), new SchemaId(60_012)),
        3 => layouts.Register(typeof(DeltaPayload), new SchemaId(60_013)),
        4 => layouts.Register(typeof(DeltaPayload), new SchemaId(60_014)),
        _ => layouts.Register(typeof(DeltaPayload), new SchemaId(60_015))
    };

    private void SetDeltaPayload(Entity entity, int seed)
    {
        for (var i = 0; i < PayloadRows; i++)
        {
            _deltaWorld.Set(entity, _deltaComponents[2 + i], new DeltaPayload { Value = seed });
        }
    }

    private void SetFrifloPayload(FrifloEntity entity, int seed)
    {
        for (var i = 0; i < PayloadRows; i++)
        {
            switch (i)
            {
                case 0: entity.AddComponent(new FrifloPayload0 { Value = seed }); break;
                case 1: entity.AddComponent(new FrifloPayload1 { Value = seed }); break;
                case 2: entity.AddComponent(new FrifloPayload2 { Value = seed }); break;
                case 3: entity.AddComponent(new FrifloPayload3 { Value = seed }); break;
                case 4: entity.AddComponent(new FrifloPayload4 { Value = seed }); break;
                case 5: entity.AddComponent(new FrifloPayload5 { Value = seed }); break;
            }
        }
    }

    private static void UpdateArch(ref ArchPosition position, ref ArchVelocity velocity)
    {
        position.X += velocity.X * Dt;
        position.Y += velocity.Y * Dt;
        s_archCount++;
        s_archChecksum += position.X + position.Y;
    }

    private static void UpdateFriflo(ref FrifloPosition position, ref FrifloVelocity velocity)
    {
        position.X += velocity.X * Dt;
        position.Y += velocity.Y * Dt;
        s_frifloCount++;
        s_frifloChecksum += position.X + position.Y;
    }

    private static double s_archChecksum;
    private static double s_frifloChecksum;
    private static int s_archCount;
    private static int s_frifloCount;

    private struct DeltaPosition { public float X; public float Y; }
    private struct DeltaVelocity { public float X; public float Y; }
    private struct DeltaPayload { public int Value; }

    private struct ArchPosition { public float X; public float Y; }
    private struct ArchVelocity { public float X; public float Y; }
    private struct ArchPayload0 { public int Value; }
    private struct ArchPayload1 { public int Value; }
    private struct ArchPayload2 { public int Value; }
    private struct ArchPayload3 { public int Value; }
    private struct ArchPayload4 { public int Value; }
    private struct ArchPayload5 { public int Value; }

    private struct FrifloPosition : IComponent { public float X; public float Y; }
    private struct FrifloVelocity : IComponent { public float X; public float Y; }
    private struct FrifloPayload0 : IComponent { public int Value; }
    private struct FrifloPayload1 : IComponent { public int Value; }
    private struct FrifloPayload2 : IComponent { public int Value; }
    private struct FrifloPayload3 : IComponent { public int Value; }
    private struct FrifloPayload4 : IComponent { public int Value; }
    private struct FrifloPayload5 : IComponent { public int Value; }
}

internal static class MovementGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Checksum(double checksum, int actualCount, int expectedCount)
    {
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException($"Movement benchmark touched {actualCount} entities, expected {expectedCount}.");
        }

        if (double.IsNaN(checksum) || double.IsInfinity(checksum))
        {
            throw new InvalidOperationException("Movement benchmark checksum is not finite.");
        }

        return checksum;
    }
}

internal sealed class LegacyMovementReference
{
    private readonly byte[] _positions;
    private readonly byte[] _velocities;
    private readonly byte[][] _payloadRows;
    private readonly int _amount;

    public LegacyMovementReference(int amount, int payloadRows)
    {
        _amount = amount;
        _positions = new byte[amount * Unsafe.SizeOf<LegacyMovementValue>()];
        _velocities = new byte[amount * Unsafe.SizeOf<LegacyMovementValue>()];
        _payloadRows = new byte[payloadRows][];
        for (var row = 0; row < payloadRows; row++)
        {
            _payloadRows[row] = new byte[amount * sizeof(int)];
        }

        var positions = MemoryMarshal.Cast<byte, LegacyMovementValue>(_positions.AsSpan());
        var velocities = MemoryMarshal.Cast<byte, LegacyMovementValue>(_velocities.AsSpan());
        for (var i = 0; i < amount; i++)
        {
            positions[i] = new LegacyMovementValue { X = 1, Y = 2 };
            velocities[i] = new LegacyMovementValue { X = 3, Y = 4 };
        }
    }

    public double Iterate(float dt)
    {
        var positions = MemoryMarshal.Cast<byte, LegacyMovementValue>(_positions.AsSpan());
        var velocities = MemoryMarshal.Cast<byte, LegacyMovementValue>(_velocities.AsSpan());
        double checksum = 0;
        for (var i = 0; i < _amount; i++)
        {
            positions[i].X += velocities[i].X * dt;
            positions[i].Y += velocities[i].Y * dt;
            checksum += positions[i].X + positions[i].Y;
        }

        GC.KeepAlive(_payloadRows);
        return checksum;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LegacyMovementValue
    {
        public float X;
        public float Y;
    }
}
