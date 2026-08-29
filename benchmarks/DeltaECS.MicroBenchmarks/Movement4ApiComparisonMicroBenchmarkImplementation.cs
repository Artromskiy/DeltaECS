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

internal static class Movement4ApiComparisonKernels
{
    public static int ThreeWhile(
        MicroWorld fixture,
        in Query query,
        WriteAccess aAccess,
        WriteAccess bAccess,
        WriteAccess cAccess,
        ReadAccess dAccess)
    {
        var checksum = 0;
        using var scope = fixture.World.BeginScope(in query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var a = slots.GetRow(aAccess);
                var b = slots.GetRow(bAccess);
                var c = slots.GetRow(cAccess);
                var d = slots.GetRow(dAccess);
                while (slots.MoveNext())
                {
                    ref var rowA = ref a.Ref<Movement4A>(slots);
                    ref var rowB = ref b.Ref<Movement4B>(slots);
                    ref var rowC = ref c.Ref<Movement4C>(slots);
                    ref readonly var rowD = ref d.Ref<Movement4D>(slots);
                    Apply(ref rowA, ref rowB, ref rowC, in rowD, ref checksum);
                }
            }
        }

        return checksum;
    }

    public static int TwoWhile(
        MicroWorld fixture,
        in Query query,
        WriteAccess aAccess,
        WriteAccess bAccess,
        WriteAccess cAccess,
        ReadAccess dAccess)
    {
        var checksum = 0;
        using var scope = fixture.World.BeginScope(in query);
        var chunks = scope.Chunks;
        while (chunks.MoveNext())
        {
            var slots = chunks.Current.Slots;
            var a = slots.GetRow(aAccess);
            var b = slots.GetRow(bAccess);
            var c = slots.GetRow(cAccess);
            var d = slots.GetRow(dAccess);
            while (slots.MoveNext())
            {
                ref var rowA = ref a.Ref<Movement4A>(slots);
                ref var rowB = ref b.Ref<Movement4B>(slots);
                ref var rowC = ref c.Ref<Movement4C>(slots);
                ref readonly var rowD = ref d.Ref<Movement4D>(slots);
                Apply(ref rowA, ref rowB, ref rowC, in rowD, ref checksum);
            }
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Apply(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        ref readonly Movement4D d,
        ref int checksum)
    {
        a.Value = d.Value + 1;
        b.Value = d.Value + 2;
        c.Value = (a.Value + b.Value) / 2;
        checksum += a.Value + b.Value + c.Value + d.Value;
    }
}

public class Movement4ApiComparisonMicroBenchmarkImplementation
{
    internal static int s_delegateChecksum;

    [Params(100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Query _query;
    private WriteAccess _a;
    private WriteAccess _b;
    private WriteAccess _c;
    private ReadAccess _d;

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
        _a = _query.AccessWrite(_fixture.Movement4A);
        _b = _query.AccessWrite(_fixture.Movement4B);
        _c = _query.AccessWrite(_fixture.Movement4C);
        _d = _query.AccessRead(_fixture.Movement4D);
    }

    [Benchmark(Baseline = true)]
    public int ThreeWhile() => Movement4ApiComparisonKernels.ThreeWhile(
        _fixture,
        in _query,
        _a,
        _b,
        _c,
        _d);

    [Benchmark]
    public int TwoWhile() => Movement4ApiComparisonKernels.TwoWhile(
        _fixture,
        in _query,
        _a,
        _b,
        _c,
        _d);

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
