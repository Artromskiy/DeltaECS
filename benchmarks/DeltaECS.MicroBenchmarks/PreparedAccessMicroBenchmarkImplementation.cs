using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Measures repeated invocations of entity-aware and explicit-ID operations.</summary>
public class PreparedAccessMicroBenchmarkImplementation
{
    private MicroWorld _fixture = null!;
    private Query _query;
    private IOperation<PrimaryFunctor> _primaryOperation = null!;
    private IOperation<EntityAwarePrimaryFunctor> _entityAwarePrimaryOperation = null!;
    private IOperation<ExplicitIdFunctor> _explicitIdOperation = null!;
    private IOperation<ExplicitEntityIdFunctor> _explicitEntityIdOperation = null!;

    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld(initialEntityCapacity: Amount);
        _ = _fixture.CreateMovement4(Amount);
        var description = QuerySpec.WhereAll(_fixture.Movement4);
        _query = _fixture.World.CreateQuery(in description);
        var primaryFunctor = new PrimaryFunctor();
        var entityAwareFunctor = new EntityAwarePrimaryFunctor();
        var explicitIdFunctor = new ExplicitIdFunctor();
        var explicitEntityIdFunctor = new ExplicitEntityIdFunctor();
        _primaryOperation = _fixture.World.ForEach(in _query, ref primaryFunctor);
        _entityAwarePrimaryOperation = _fixture.World.ForEachEntity(in _query, ref entityAwareFunctor);
        _explicitIdOperation = _fixture.World.ForEach(in _query, _fixture.Movement4, ref explicitIdFunctor);
        _explicitEntityIdOperation = _fixture.World.ForEachEntity(in _query, _fixture.Movement4, ref explicitEntityIdFunctor);
    }

    [GlobalCleanup]
    public void Cleanup() => _fixture.World.Dispose();

    [Benchmark]
    public int PrimaryIds()
    {
        var functor = new PrimaryFunctor();
        _primaryOperation.Invoke(ref functor);
        return functor.Checksum;
    }

    [Benchmark]
    public int EntityAwarePrimary()
    {
        var functor = new EntityAwarePrimaryFunctor();
        _entityAwarePrimaryOperation.Invoke(ref functor);
        return functor.Checksum;
    }

    [Benchmark]
    public int ExplicitIds()
    {
        var functor = new ExplicitIdFunctor();
        _explicitIdOperation.Invoke(ref functor);
        return functor.Checksum;
    }

    [Benchmark]
    public int EntityAwareExplicitIds()
    {
        var functor = new ExplicitEntityIdFunctor();
        _explicitEntityIdOperation.Invoke(ref functor);
        return functor.Checksum;
    }
}

internal struct PrimaryFunctor : IForEach
{
    public int Checksum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        Checksum += a.Value + b.Value + c.Value + d.Value;
    }
}

internal struct EntityAwarePrimaryFunctor : IForEachEntity
{
    public int Checksum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        EntityRef entity,
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        Checksum += a.Value + b.Value + c.Value + d.Value + entity.Index;
    }
}

internal struct ExplicitIdFunctor : IForEach
{
    public int Checksum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        Checksum += a.Value + b.Value + c.Value + d.Value;
    }
}

internal struct ExplicitEntityIdFunctor : IForEachEntity
{
    public int Checksum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        EntityRef entity,
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        Checksum += a.Value + b.Value + c.Value + d.Value + entity.Index;
    }
}
