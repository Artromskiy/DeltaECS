using System;
using System.Collections.Generic;
using System.Numerics;

using Delta.ECS;

namespace ApiChoiceProbe;

public partial record struct Position(Vector2 Value);
public partial record struct Velocity(Vector2 Value);
public partial record struct Health(int Value);
public partial record struct StableId(int Value);
public partial record struct Noise(int Value);
public partial record struct Enemy;
public partial record struct Paused;
public partial record struct Marked;
public interface IResettable { void Reset(); }
public partial record struct Armor(int Value) : IResettable { public void Reset() => Value = 0; }
public partial record struct Energy(int Value) : IResettable { public void Reset() => Value = 0; }
public readonly record struct ChangeNotice(Entity Entity, ChangeKind Kind, int? Value);
public readonly record struct SnapshotRecord(Entity Entity, ComponentId Id, object? Value);
public sealed class SnapshotSink
{
    public readonly List<SnapshotRecord> Records = new();
    public void Put<T>(Entity entity, ComponentId id, in T value) => Records.Add(new(entity, id, value));
}

public sealed class Scenario : IDisposable
{
    public readonly World World = new();
    public readonly ComponentId PositionId, VelocityId, HealthId, StableIdId, NoiseId, EnemyId, PausedId, MarkedId;
    public readonly ComponentId ArmorId, EnergyId, AmbientId, DamageId, ConfiguredHealthId;
    public readonly Entity Player;
    public readonly Entity[] Units = new Entity[8];
    public readonly ComponentId[] ResettableIds;
    public readonly ComponentId[] HistoryIds;

    public Scenario()
    {
        ComponentRegistry r = World.Components;
#if GENERATED_REGISTRATIONS
        PositionId = r.Register(GeneratedComponentCatalog.GetRegistration<Position>());
        VelocityId = r.Register(GeneratedComponentCatalog.GetRegistration<Velocity>());
        HealthId = r.Register(GeneratedComponentCatalog.GetRegistration<Health>());
        StableIdId = r.Register(GeneratedComponentCatalog.GetRegistration<StableId>());
        NoiseId = r.Register(GeneratedComponentCatalog.GetRegistration<Noise>());
        EnemyId = r.Register(GeneratedComponentCatalog.GetRegistration<Enemy>());
        PausedId = r.Register(GeneratedComponentCatalog.GetRegistration<Paused>());
        MarkedId = r.Register(GeneratedComponentCatalog.GetRegistration<Marked>());
        ArmorId = r.Register(GeneratedComponentCatalog.GetRegistration<Armor>());
        EnergyId = r.Register(GeneratedComponentCatalog.GetRegistration<Energy>());
#else
        PositionId = r.Register<Position>(new SchemaId(1));
        VelocityId = r.Register<Velocity>(new SchemaId(2));
        HealthId = r.Register<Health>(new SchemaId(3));
        StableIdId = r.Register<StableId>(new SchemaId(4));
        NoiseId = r.Register<Noise>(new SchemaId(5));
        EnemyId = r.Register<Enemy>(new SchemaId(6));
        PausedId = r.Register<Paused>(new SchemaId(7));
        MarkedId = r.Register<Marked>(new SchemaId(8));
        ArmorId = r.Register<Armor>(new SchemaId(9));
        EnergyId = r.Register<Energy>(new SchemaId(10));
#endif
        AmbientId = r.Register<float>(new SchemaId(100));
        DamageId = r.Register<float>(new SchemaId(101));
        ConfiguredHealthId = r.Register<float>(new SchemaId(102));
        r.BindInterface<Armor, IResettable>();
        r.BindInterface<Energy, IResettable>();
        ResettableIds = new[] { ArmorId, EnergyId };
        HistoryIds = new[] { PositionId, HealthId, ArmorId, DamageId };
        Player = World.Create(PositionId);
        World.GetRef<Position>(Player) = new(Vector2.Zero);
        Vector2[] points = { new(2, 0), new(2, 0), new(4, 0), new(1, 0), new(1, 0), new(11, 0), new(3, 0), new(0, 5) };
        int[] health = { 6, 4, 20, 0, 9, 9, 8, 7 };
        int[] stableIds = { 20, 10, 30, 40, 50, 60, 70, 80 };
        for (int i = 0; i < Units.Length; i++)
        {
            Entity e = SpawnUnit(points[i], health[i], stableIds[i], i != 6, i == 4);
            Units[i] = e;
            World.GetRef<Velocity>(e) = new(new Vector2(i + 1, -i));
            if (i is 0 or 2 or 6) World.Add(e, MarkedId);
            AddConfigured(e, i + 2, i + 10);
        }
        World.Add(Units[0], new Armor(7));
        World.Add(Units[0], new Energy(3));
        World.Add(Units[1], new Armor(8));
        World.Add(Units[2], new Energy(6));
    }

    public Entity SpawnUnit(Vector2 point, int health, int stableId, bool enemy = true, bool paused = false)
    {
        Entity e = World.Create(new ComponentId[] { PositionId, VelocityId, HealthId, StableIdId, NoiseId });
        World.GetRef<Position>(e) = new(point);
        World.GetRef<Velocity>(e) = new(new Vector2(1, 0));
        World.GetRef<Health>(e) = new(health);
        World.GetRef<StableId>(e) = new(stableId);
        World.GetRef<Noise>(e) = new(1234);
        if (enemy) World.Add(e, EnemyId);
        if (paused) World.Add(e, PausedId);
        return e;
    }

    public Entity SpawnHealth(int value)
    {
        Entity e = World.Create(HealthId);
        World.GetRef<Health>(e) = new(value);
        return e;
    }

    public void AddConfigured(Entity e, float damage, float health)
    {
        World.Add(e, new ComponentId[] { AmbientId, DamageId, ConfiguredHealthId });
        World.GetRef<float>(e, AmbientId) = 999;
        World.GetRef<float>(e, DamageId) = damage;
        World.GetRef<float>(e, ConfiguredHealthId) = health;
    }
    public void Dispose() => World.Dispose();
}

public static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    public static void Set(IEnumerable<Entity> actual, IEnumerable<Entity> expected, string label)
        => That(new HashSet<Entity>(actual).SetEquals(expected), label);
    public static void Notices(IReadOnlyList<ChangeNotice> actual, params ChangeNotice[] expected)
    {
        That(actual.Count == expected.Length, $"Notice count: expected {expected.Length}, got {actual.Count}");
        That(new HashSet<ChangeNotice>(actual).SetEquals(expected), "Notice identities/kinds/values differ");
    }
}
