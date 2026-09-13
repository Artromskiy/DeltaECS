using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

public struct Movement4ApiContext
{
    public int Checksum;
}

internal struct Movement4NoContextFunctor : IForEach
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

internal struct Movement4ContextFunctor : IForEachContext<Movement4ApiContext>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        ref Movement4ApiContext context,
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        context.Checksum += a.Value + b.Value + c.Value + d.Value;
    }
}

public class Movement4ApiComparisonMicroBenchmarkImplementation
{
    internal static int s_delegateChecksum;

    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private MicroWorld _fixture = null!;
    private Query _query;
    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld(initialEntityCapacity: Amount);
        _ = _fixture.CreateMovement4(Amount);
        var description = QuerySpec.WhereAll(
            _fixture.Movement4A,
            _fixture.Movement4B,
            _fixture.Movement4C,
            _fixture.Movement4D);
        _query = _fixture.World.CreateQuery(in description);
    }

    [Benchmark]
    public int Functor()
    {
        var functor = new Movement4NoContextFunctor();
        _fixture.World.ForEach(in _query, ref functor);
        return functor.Checksum;
    }

    [Benchmark]
    public int Delegate()
    {
        s_delegateChecksum = 0;
        ForEachAction_WWWR<Movement4A, Movement4B, Movement4C, Movement4D> action = ApplyDelegate;
        _fixture.World.ForEach(in _query, action);
        return s_delegateChecksum;
    }

    [Benchmark]
    public int Intercepted()
    {
        s_delegateChecksum = 0;
        _fixture.World.ForEach(in _query, ApplyDelegate);
        return s_delegateChecksum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ApplyDelegate(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        s_delegateChecksum += a.Value + b.Value + c.Value + d.Value;
    }

    [Benchmark]
    public int DelegateContext()
    {
        var context = new Movement4ApiContext();
        _fixture.World.ForEach(
            in _query,
            ref context,
            static (ref Movement4ApiContext state, ref Movement4A a, ref Movement4B b, ref Movement4C c, ref readonly Movement4D d) =>
            {
                a.Value = d.Value + 1;
                b.Value = d.Value + 2;
                c.Value = (a.Value + b.Value) / 2;
                state.Checksum += a.Value + b.Value + c.Value + d.Value;
            });
        return context.Checksum;
    }

    [Benchmark]
    public int FunctorContext()
    {
        var context = new Movement4ApiContext();
        var functor = new Movement4ContextFunctor();
        _fixture.World.ForEach(in _query, ref context, ref functor);
        return context.Checksum;
    }
}
