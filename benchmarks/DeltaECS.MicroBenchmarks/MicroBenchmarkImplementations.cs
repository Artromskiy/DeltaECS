using BenchmarkDotNet.Attributes;
using DeltaECS;
using System.Runtime.CompilerServices;

namespace DeltaECS.MicroBenchmarks;

internal static class MicroIds
{
    public static (ComponentId Position, ComponentId Velocity, ComponentId Auxiliary, ComponentId Reference, ComponentId Movement4A, ComponentId Movement4B, ComponentId Movement4C, ComponentId Movement4D) Register(ComponentLayoutRegistry layouts)
        => (
            layouts.Register(typeof(Position), new SchemaId(30_001)),
            layouts.Register(typeof(Velocity), new SchemaId(30_002)),
            layouts.Register(typeof(Auxiliary), new SchemaId(30_003)),
            layouts.Register(typeof(ReferenceValue), new SchemaId(30_004)),
            layouts.Register(typeof(Movement4A), new SchemaId(30_005)),
            layouts.Register(typeof(Movement4B), new SchemaId(30_006)),
            layouts.Register(typeof(Movement4C), new SchemaId(30_007)),
            layouts.Register(typeof(Movement4D), new SchemaId(30_008)));
}

public struct Position { public int X; public int Y; }
public struct Velocity { public int X; public int Y; }
public struct Auxiliary { public int Value; }
public sealed class ReferenceValue { public int Value; }
public struct Movement4A { public int Value; }
public struct Movement4B { public int Value; }
public struct Movement4C { public int Value; }
public struct Movement4D { public int Value; }

internal sealed class MicroWorld
{
    public readonly ComponentLayoutRegistry Layouts = new();
    public readonly ComponentId Position;
    public readonly ComponentId Velocity;
    public readonly ComponentId Auxiliary;
    public readonly ComponentId Reference;
    public readonly ComponentId Movement4A;
    public readonly ComponentId Movement4B;
    public readonly ComponentId Movement4C;
    public readonly ComponentId Movement4D;

    public MicroWorld(int chunkCapacity = 64, int initialEntityCapacity = 100_000)
    {
        (Position, Velocity, Auxiliary, Reference, Movement4A, Movement4B, Movement4C, Movement4D) = MicroIds.Register(Layouts);
        World = new World(Layouts, initialEntityCapacity: initialEntityCapacity, chunkCapacity: chunkCapacity);
    }

    public World World { get; }

    public ComponentId[] Moving => [Position, Velocity];

    public ComponentId[] Movement4 => [Movement4A, Movement4B, Movement4C, Movement4D];

    public Entity[] CreateMoving(int amount)
    {
        var entities = new Entity[amount];
        World.Create(Moving, entities);
        ResetMoving(entities);
        return entities;
    }

    public void ResetMoving(Entity[] entities)
    {
        for (var i = 0; i < entities.Length; i++)
        {
            World.Set(entities[i], Position, new Position { X = i, Y = i + 1 });
            World.Set(entities[i], Velocity, new Velocity { X = 1, Y = 2 });
        }
    }

    public Entity[] CreateMovement4(int amount)
    {
        var entities = new Entity[amount];
        World.Create(Movement4, entities);
        ResetMovement4(entities);
        return entities;
    }

    public void ResetMovement4(Entity[] entities)
    {
        for (var i = 0; i < entities.Length; i++)
        {
            World.Set(entities[i], Movement4A, new Movement4A { Value = 1 });
            World.Set(entities[i], Movement4B, new Movement4B { Value = 2 });
            World.Set(entities[i], Movement4C, new Movement4C { Value = 3 });
            World.Set(entities[i], Movement4D, new Movement4D { Value = 4 });
        }
    }
}
internal static class MicroBenchmarkKernels
{
    public static int IterateMovement2Dense(
        MicroWorld fixture,
        in Query query,
        WriteAccess position,
        ReadAccess velocity)
    {
        _ = position;
        _ = velocity;
        var checksum = 0;
        fixture.World.ForEach(
            in query,
            ref checksum,
            static (ref int sum, ref Position p, in Velocity v) =>
            {
                p.X += v.X;
                p.Y += v.Y;
                sum += p.X + p.Y;
            });
        return checksum;
    }

    public static int IterateMovement4Dense(
        MicroWorld fixture,
        in Query query,
        WriteAccess aBinding,
        WriteAccess bBinding,
        WriteAccess cBinding,
        ReadAccess dBinding)
    {
        _ = aBinding;
        _ = bBinding;
        _ = cBinding;
        _ = dBinding;
        var checksum = 0;
        fixture.World.ForEach(
            in query,
            ref checksum,
            static (ref int sum, ref Movement4A a, ref Movement4B b, ref Movement4C c, in Movement4D d) =>
            {
                a.Value += d.Value;
                b.Value += d.Value;
                c.Value = (a.Value + b.Value) / 2;
                sum += a.Value + b.Value + c.Value + d.Value;
            });
        return checksum;
    }

    public static int IterateMovement4ForwardFor(
        MicroWorld fixture,
        in Query query,
        WriteAccess aBinding,
        WriteAccess bBinding,
        WriteAccess cBinding,
        ReadAccess dBinding)
    {
        var checksum = 0;
        using var scope = fixture.World.BeginScope(in query);
        var preparedA = aBinding;
        var preparedB = bBinding;
        var preparedC = cBinding;
        var preparedD = dBinding;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var chunk = chunks.Current;
                var slots = chunk.Slots;
                var a = slots.GetRow(preparedA);
                var b = slots.GetRow(preparedB);
                var c = slots.GetRow(preparedC);
                var d = slots.GetRow(preparedD);
                var slotCount = chunk.SlotCount;
                for (var slot = 0; slot < slotCount; slot++)
                {
                    ref var rowA = ref a.Ref<Movement4A>(slot);
                    ref var rowB = ref b.Ref<Movement4B>(slot);
                    ref var rowC = ref c.Ref<Movement4C>(slot);
                    ref readonly var rowD = ref d.Ref<Movement4D>(slot);
                    rowA.Value += rowD.Value;
                    rowB.Value += rowD.Value;
                    rowC.Value = (rowA.Value + rowB.Value) / 2;
                    checksum += rowA.Value + rowB.Value + rowC.Value + rowD.Value;
                }
            }
        }

        return checksum;
    }

    public static int IterateMovement4ReverseFor(
        MicroWorld fixture,
        in Query query,
        WriteAccess aBinding,
        WriteAccess bBinding,
        WriteAccess cBinding,
        ReadAccess dBinding)
    {
        var checksum = 0;
        using var scope = fixture.World.BeginScope(in query);
        var preparedA = aBinding;
        var preparedB = bBinding;
        var preparedC = cBinding;
        var preparedD = dBinding;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var chunk = chunks.Current;
                var slots = chunk.Slots;
                var a = slots.GetRow(preparedA);
                var b = slots.GetRow(preparedB);
                var c = slots.GetRow(preparedC);
                var d = slots.GetRow(preparedD);
                for (var slot = chunk.SlotCount - 1; slot >= 0; slot--)
                {
                    ref var rowA = ref a.Ref<Movement4A>(slot);
                    ref var rowB = ref b.Ref<Movement4B>(slot);
                    ref var rowC = ref c.Ref<Movement4C>(slot);
                    ref readonly var rowD = ref d.Ref<Movement4D>(slot);
                    rowA.Value += rowD.Value;
                    rowB.Value += rowD.Value;
                    rowC.Value = (rowA.Value + rowB.Value) / 2;
                    checksum += rowA.Value + rowB.Value + rowC.Value + rowD.Value;
                }
            }
        }

        return checksum;
    }
}

public class DenseIterationMicroBenchmarkImplementation
{
    [Params(100, 1_000, 10_000, 100_000, 1_000_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Entity[] _movement2Entities = null!;
    private Entity[] _movement4Entities = null!;
    private Query _movement2Query;
    private Query _movement4Query;
    private WriteAccess _movement2Position;
    private ReadAccess _movement2Velocity;
    private WriteAccess _movement4A;
    private WriteAccess _movement4B;
    private WriteAccess _movement4C;
    private ReadAccess _movement4D;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _movement2Entities = _fixture.CreateMoving(Amount);
        _movement4Entities = _fixture.CreateMovement4(Amount);

        var movement2 = QuerySpec.WhereAll(_fixture.Position, _fixture.Velocity);
        _movement2Query = _fixture.World.CreateQuery(in movement2);
        _movement2Position = _movement2Query.AccessWrite(_fixture.Position);
        _movement2Velocity = _movement2Query.AccessRead(_fixture.Velocity);

        var movement4 = QuerySpec.WhereAll(
            _fixture.Movement4A,
            _fixture.Movement4B,
            _fixture.Movement4C,
            _fixture.Movement4D);
        _movement4Query = _fixture.World.CreateQuery(in movement4);
        _movement4A = _movement4Query.AccessWrite(_fixture.Movement4A);
        _movement4B = _movement4Query.AccessWrite(_fixture.Movement4B);
        _movement4C = _movement4Query.AccessWrite(_fixture.Movement4C);
        _movement4D = _movement4Query.AccessRead(_fixture.Movement4D);
    }

    [IterationSetup(Target = nameof(Movement2Components))]
    public void ResetMovement2() => _fixture.ResetMoving(_movement2Entities);

    [IterationSetup(Target = nameof(Movement4Components))]
    public void ResetMovement4() => _fixture.ResetMovement4(_movement4Entities);

    [Benchmark]
    public int Movement2Components() =>
        MicroBenchmarkKernels.IterateMovement2Dense(
            _fixture,
            in _movement2Query,
            _movement2Position,
            _movement2Velocity);

    [Benchmark]
    public int Movement4Components() =>
        MicroBenchmarkKernels.IterateMovement4Dense(
            _fixture,
            in _movement4Query,
            _movement4A,
            _movement4B,
            _movement4C,
            _movement4D);
}

public class Movement4OrderMicroBenchmarkImplementation
{
    [Params(100_000, 1_000_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;
    private Query _query;
    private WriteAccess _a;
    private WriteAccess _b;
    private WriteAccess _c;
    private ReadAccess _d;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld(initialEntityCapacity: Amount);
        _entities = _fixture.CreateMovement4(Amount);

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

    [IterationSetup]
    public void Reset() => _fixture.ResetMovement4(_entities);

    [Benchmark(Baseline = true)]
    public int ForwardFor() => MicroBenchmarkKernels.IterateMovement4ForwardFor(
        _fixture,
        in _query,
        _a,
        _b,
        _c,
        _d);

    [Benchmark]
    public int ReverseFor() => MicroBenchmarkKernels.IterateMovement4ReverseFor(
        _fixture,
        in _query,
        _a,
        _b,
        _c,
        _d);
}

internal record struct GeneratedMovement4Functor : IForEach
{
    public int Checksum { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke(
        ref Movement4A a,
        ref Movement4B b,
        ref Movement4C c,
        in Movement4D d)
    {
        a.Value += d.Value;
        b.Value += a.Value;
        c.Value += b.Value;
        Checksum += a.Value + b.Value + c.Value + d.Value;
    }
}

public class GeneratedFunctorMovement4MicroBenchmarkImplementation
{
    [Params(1_000_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;
    private Query _query;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld(initialEntityCapacity: Amount);
        _entities = _fixture.CreateMovement4(Amount);
        var description = QuerySpec.WhereAll(
            _fixture.Movement4A,
            _fixture.Movement4B,
            _fixture.Movement4C,
            _fixture.Movement4D);
        _query = _fixture.World.CreateQuery(in description);
    }

    [IterationSetup]
    public void Reset() => _fixture.ResetMovement4(_entities);

    [Benchmark]
    public int Movement4GeneratedFunctor()
    {
        var functor = new GeneratedMovement4Functor();
        _fixture.World.ForEach(in _query, ref functor);
        return functor.Checksum;
    }
}

internal static class MicroContractSmoke
{
    public static void Run()
    {
        var fixture = new MicroWorld(chunkCapacity: 4);
        var movement2Entities = fixture.CreateMoving(8);
        var movement2Description = QuerySpec.WhereAll(fixture.Position, fixture.Velocity);
        var movement2Query = fixture.World.CreateQuery(in movement2Description);
        var movement2Position = movement2Query.AccessWrite(fixture.Position);
        var movement2Velocity = movement2Query.AccessRead(fixture.Velocity);

        var movement2Sum = MicroBenchmarkKernels.IterateMovement2Dense(
            fixture,
            in movement2Query,
            movement2Position,
            movement2Velocity);
        if (movement2Sum != movement2Entities.Length * (movement2Entities.Length + 3))
            throw new InvalidOperationException("Dense Movement2 checksum mismatch.");

        fixture.ResetMoving(movement2Entities);
        var movement4Entities = fixture.CreateMovement4(8);
        var movement4Description = QuerySpec.WhereAll(
            fixture.Movement4A,
            fixture.Movement4B,
            fixture.Movement4C,
            fixture.Movement4D);
        var movement4Query = fixture.World.CreateQuery(in movement4Description);
        var movement4A = movement4Query.AccessWrite(fixture.Movement4A);
        var movement4B = movement4Query.AccessWrite(fixture.Movement4B);
        var movement4C = movement4Query.AccessWrite(fixture.Movement4C);
        var movement4D = movement4Query.AccessRead(fixture.Movement4D);

        var movement4Sum = MicroBenchmarkKernels.IterateMovement4Dense(
            fixture,
            in movement4Query,
            movement4A,
            movement4B,
            movement4C,
            movement4D);
        if (movement4Sum != movement4Entities.Length * 20)
            throw new InvalidOperationException("Dense Movement4 checksum mismatch.");

        fixture.ResetMovement4(movement4Entities);
        var movement4ForwardSum = MicroBenchmarkKernels.IterateMovement4ForwardFor(
            fixture,
            in movement4Query,
            movement4A,
            movement4B,
            movement4C,
            movement4D);
        if (movement4ForwardSum != movement4Entities.Length * 20)
            throw new InvalidOperationException("Forward Movement4 checksum mismatch.");

        fixture.ResetMovement4(movement4Entities);
        var movement4ReverseSum = MicroBenchmarkKernels.IterateMovement4ReverseFor(
            fixture,
            in movement4Query,
            movement4A,
            movement4B,
            movement4C,
            movement4D);
        if (movement4ReverseSum != movement4Entities.Length * 20)
            throw new InvalidOperationException("Reverse Movement4 checksum mismatch.");

        var apiComparison = new Movement4ApiComparisonMicroBenchmarkImplementation { Amount = 8 };
        apiComparison.Setup();
        var expectedApiComparison = apiComparison.ThreeWhile();
        if (apiComparison.TwoWhile() != expectedApiComparison)
            throw new InvalidOperationException("Two-while Movement4 checksum mismatch.");
        if (apiComparison.Functor() != expectedApiComparison)
            throw new InvalidOperationException("Functor Movement4 checksum mismatch.");
        if (apiComparison.Delegate() != expectedApiComparison)
            throw new InvalidOperationException("Delegate Movement4 checksum mismatch.");
        if (apiComparison.DelegateContext() != expectedApiComparison)
            throw new InvalidOperationException("Delegate-context Movement4 checksum mismatch.");
        if (apiComparison.FunctorContext() != expectedApiComparison)
            throw new InvalidOperationException("Functor-context Movement4 checksum mismatch.");

        Console.WriteLine("Micro contract smoke passed: dense Movement2/Movement4 and API-shape comparisons.");
    }
}
