using System;
using Delta.ECS;

namespace ApiChoiceProbe;

public interface IExpiringEffect { int Turns { get; set; } void Tick(); }
public record struct Burn(int Turns) : IExpiringEffect { public void Tick() => Turns--; }
public record struct Frost(int Turns) : IExpiringEffect { public void Tick() => Turns--; }
public record struct Shock(int Turns) : IExpiringEffect { public void Tick() => Turns--; }
public record struct Untracked(int Turns) : IExpiringEffect { public void Tick() => Turns--; }
public sealed class EffectScenario : IDisposable
{
    public readonly World World = new();
    public readonly ComponentId BurnId, FrostId, PoisonId, PoisonAltId, ShockId, UntrackedId, NoiseId;
    public readonly ComponentId[] EffectIds;
    public readonly Entity A;
    public EffectScenario()
    {
        var r = World.Components;
        BurnId = r.Register<Burn>(new SchemaId(1)); FrostId = r.Register<Frost>(new SchemaId(2));
        PoisonId = r.Register<Burn>(new SchemaId(3)); PoisonAltId = r.Register<Burn>(new SchemaId(4));
        ShockId = r.Register<Shock>(new SchemaId(5)); UntrackedId = r.Register<Untracked>(new SchemaId(6));
        NoiseId = r.Register<Noise>(new SchemaId(7));
        r.BindInterface<Burn, IExpiringEffect>(); r.BindInterface<Frost, IExpiringEffect>();
        r.BindInterface<Shock, IExpiringEffect>(); r.BindInterface<Untracked, IExpiringEffect>();
        EffectIds = new[] { BurnId, FrostId, PoisonId, PoisonAltId, ShockId };
        A = World.Create(new[] { BurnId, FrostId, PoisonId, PoisonAltId, ShockId, UntrackedId, NoiseId });
        World.GetRef<Burn>(A, BurnId) = new(1); World.GetRef<Frost>(A, FrostId) = new(2);
        World.GetRef<Burn>(A, PoisonId) = new(4); World.GetRef<Burn>(A, PoisonAltId) = new(4);
        World.GetRef<Shock>(A) = new(3); World.GetRef<Untracked>(A) = new(3);
        World.GetRef<Noise>(A) = new(1234);
    }
    public void Dispose() => World.Dispose();
}
