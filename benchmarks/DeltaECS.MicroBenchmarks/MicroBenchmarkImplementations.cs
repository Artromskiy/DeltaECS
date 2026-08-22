using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal static class MicroIds
{
    public static (ComponentId Position, ComponentId Velocity, ComponentId Auxiliary, ComponentId Reference, ComponentId Movement4A, ComponentId Movement4B, ComponentId Movement4C, ComponentId Movement4D) Register(ComponentLayoutRegistry layouts)
        => (
            layouts.Register<Position>(new SchemaId(30_001)),
            layouts.Register<Velocity>(new SchemaId(30_002)),
            layouts.Register<Auxiliary>(new SchemaId(30_003)),
            layouts.Register<ReferenceValue>(new SchemaId(30_004)),
            layouts.Register<Movement4A>(new SchemaId(30_005)),
            layouts.Register<Movement4B>(new SchemaId(30_006)),
            layouts.Register<Movement4C>(new SchemaId(30_007)),
            layouts.Register<Movement4D>(new SchemaId(30_008)));
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
        World.CreateBatch(Moving, entities);
        ResetMoving(entities);
        return entities;
    }

    public void ResetMoving(Entity[] entities)
    {
        for (var i = 0; i < entities.Length; i++)
        {
            World.SetComponent(entities[i], Position, new Position { X = i, Y = i + 1 });
            World.SetComponent(entities[i], Velocity, new Velocity { X = 1, Y = 2 });
        }
    }

    public Entity[] CreateMovement4(int amount)
    {
        var entities = new Entity[amount];
        World.CreateBatch(Movement4, entities);
        ResetMovement4(entities);
        return entities;
    }

    public void ResetMovement4(Entity[] entities)
    {
        for (var i = 0; i < entities.Length; i++)
        {
            World.SetComponent(entities[i], Movement4A, new Movement4A { Value = 1 });
            World.SetComponent(entities[i], Movement4B, new Movement4B { Value = 2 });
            World.SetComponent(entities[i], Movement4C, new Movement4C { Value = 3 });
            World.SetComponent(entities[i], Movement4D, new Movement4D { Value = 4 });
        }
    }
}
internal static class MicroBenchmarkKernels
{
    public static int IterateMovement2Dense(
        MicroWorld fixture,
        in Query query,
        WriteRequest<Position> position,
        ReadRequest<Velocity> velocity)
    {
        var checksum = 0;
        using var scope = fixture.World.OpenQuery(in query);
        var preparedPosition = scope.Bind(position);
        var preparedVelocity = scope.Bind(velocity);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var positions = slots.Get(preparedPosition);
                var velocities = slots.Get(preparedVelocity);
                while (slots.MoveNext())
                {
                    ref var p = ref positions[slots];
                    ref readonly var v = ref velocities[slots];
                    p.X += v.X;
                    p.Y += v.Y;
                    checksum += p.X + p.Y;
                }
            }
        }

        return checksum;
    }

    public static int IterateMovement4Dense(
        MicroWorld fixture,
        in Query query,
        WriteRequest<Movement4A> aBinding,
        WriteRequest<Movement4B> bBinding,
        WriteRequest<Movement4C> cBinding,
        ReadRequest<Movement4D> dBinding)
    {
        var checksum = 0;
        using var scope = fixture.World.OpenQuery(in query);
        var preparedA = scope.Bind(aBinding);
        var preparedB = scope.Bind(bBinding);
        var preparedC = scope.Bind(cBinding);
        var preparedD = scope.Bind(dBinding);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var a = slots.Get(preparedA);
                var b = slots.Get(preparedB);
                var c = slots.Get(preparedC);
                var d = slots.Get(preparedD);
                while (slots.MoveNext())
                {
                    ref var rowA = ref a[slots];
                    ref var rowB = ref b[slots];
                    ref var rowC = ref c[slots];
                    ref readonly var rowD = ref d[slots];
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
    [Params(100, 1_000, 10_000, 100_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Entity[] _movement2Entities = null!;
    private Entity[] _movement4Entities = null!;
    private Query _movement2Query;
    private Query _movement4Query;
    private WriteRequest<Position> _movement2Position;
    private ReadRequest<Velocity> _movement2Velocity;
    private WriteRequest<Movement4A> _movement4A;
    private WriteRequest<Movement4B> _movement4B;
    private WriteRequest<Movement4C> _movement4C;
    private ReadRequest<Movement4D> _movement4D;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _movement2Entities = _fixture.CreateMoving(Amount);
        _movement4Entities = _fixture.CreateMovement4(Amount);

        var movement2 = QuerySpec.ForComponents(_fixture.Position, _fixture.Velocity);
        _movement2Query = _fixture.World.CreateQuery(in movement2);
        _movement2Position = _movement2Query.Access<Position>(_fixture.Position, AccessMode.Write);
        _movement2Velocity = _movement2Query.Access<Velocity>(_fixture.Velocity, AccessMode.Read);

        var movement4 = QuerySpec.ForComponents(
            _fixture.Movement4A,
            _fixture.Movement4B,
            _fixture.Movement4C,
            _fixture.Movement4D);
        _movement4Query = _fixture.World.CreateQuery(in movement4);
        _movement4A = _movement4Query.Access<Movement4A>(_fixture.Movement4A, AccessMode.Write);
        _movement4B = _movement4Query.Access<Movement4B>(_fixture.Movement4B, AccessMode.Write);
        _movement4C = _movement4Query.Access<Movement4C>(_fixture.Movement4C, AccessMode.Write);
        _movement4D = _movement4Query.Access<Movement4D>(_fixture.Movement4D, AccessMode.Read);
    }

    [IterationSetup(Target = nameof(Movement2Components))]
    public void ResetMovement2() => _fixture.ResetMoving(_movement2Entities);

    [IterationSetup(Target = nameof(Movement4Components))]
    public void ResetMovement4() => _fixture.ResetMovement4(_movement4Entities);

    [Benchmark]
    [InvocationCount(1)]
    public int Movement2Components() =>
        MicroBenchmarkKernels.IterateMovement2Dense(
            _fixture,
            in _movement2Query,
            _movement2Position,
            _movement2Velocity);

    [Benchmark]
    [InvocationCount(1)]
    public int Movement4Components() =>
        MicroBenchmarkKernels.IterateMovement4Dense(
            _fixture,
            in _movement4Query,
            _movement4A,
            _movement4B,
            _movement4C,
            _movement4D);
}

public class AddMicroBenchmarkImplementation
{
    [Params(1, 4)]
    public int ChangeWidth { get; set; }

    private MicroWorld _fixture = null!;
    private Entity _entity;
    private ComponentId[] _change = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _entity = _fixture.World.Create([_fixture.Position, _fixture.Velocity]);
        _change = ChangeWidth == 1
            ? [_fixture.Auxiliary]
            : [_fixture.Auxiliary, _fixture.Reference];
    }

    [Benchmark]
    [InvocationCount(1)]
    public int Add()
    {
        _fixture.World.AddComponents(_change, _entity);
        return _entity.Index;
    }
}

public class RemoveMicroBenchmarkImplementation
{
    [Params(1, 4)]
    public int ChangeWidth { get; set; }

    private MicroWorld _fixture = null!;
    private Entity _entity;
    private ComponentId[] _change = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _change = ChangeWidth == 1
            ? [_fixture.Auxiliary]
            : [_fixture.Auxiliary, _fixture.Reference];

        var components = new ComponentId[2 + _change.Length];
        components[0] = _fixture.Position;
        components[1] = _fixture.Velocity;
        _change.CopyTo(components, 2);
        _entity = _fixture.World.Create(components);
    }

    [Benchmark]
    [InvocationCount(1)]
    public int Remove()
    {
        _fixture.World.RemoveComponents(_change, _entity);
        return _entity.Index;
    }
}

public class CreateMicroBenchmarkImplementation
{
    private MicroWorld _fixture = null!;
    private ComponentId[] _components = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _components = [_fixture.Position, _fixture.Velocity];
    }

    [Benchmark]
    [InvocationCount(1)]
    public int Create()
    {
        var entity = _fixture.World.Create(_components);
        return entity.IsAlive ? entity.Index : -1;
    }
}

public class DestroyMicroBenchmarkImplementation
{
    private MicroWorld _fixture = null!;
    private Entity _entity;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _entity = _fixture.World.Create([_fixture.Position, _fixture.Velocity]);
    }

    [Benchmark]
    [InvocationCount(1)]
    public int Destroy() => _fixture.World.Destroy(_entity) ? 1 : 0;
}

internal static class MicroContractSmoke
{
    public static void Run()
    {
        var fixture = new MicroWorld(chunkCapacity: 4);
        var movement2Entities = fixture.CreateMoving(8);
        var movement2Description = QuerySpec.ForComponents(fixture.Position, fixture.Velocity);
        var movement2Query = fixture.World.CreateQuery(in movement2Description);
        var movement2Position = movement2Query.Access<Position>(fixture.Position, AccessMode.Write);
        var movement2Velocity = movement2Query.Access<Velocity>(fixture.Velocity, AccessMode.Read);

        var movement2Sum = MicroBenchmarkKernels.IterateMovement2Dense(
            fixture,
            in movement2Query,
            movement2Position,
            movement2Velocity);
        if (movement2Sum != movement2Entities.Length * (movement2Entities.Length + 3))
            throw new InvalidOperationException("Dense Movement2 checksum mismatch.");

        fixture.ResetMoving(movement2Entities);
        var movement4Entities = fixture.CreateMovement4(8);
        var movement4Description = QuerySpec.ForComponents(
            fixture.Movement4A,
            fixture.Movement4B,
            fixture.Movement4C,
            fixture.Movement4D);
        var movement4Query = fixture.World.CreateQuery(in movement4Description);
        var movement4A = movement4Query.Access<Movement4A>(fixture.Movement4A, AccessMode.Write);
        var movement4B = movement4Query.Access<Movement4B>(fixture.Movement4B, AccessMode.Write);
        var movement4C = movement4Query.Access<Movement4C>(fixture.Movement4C, AccessMode.Write);
        var movement4D = movement4Query.Access<Movement4D>(fixture.Movement4D, AccessMode.Read);

        var movement4Sum = MicroBenchmarkKernels.IterateMovement4Dense(
            fixture,
            in movement4Query,
            movement4A,
            movement4B,
            movement4C,
            movement4D);
        if (movement4Sum != movement4Entities.Length * 20)
            throw new InvalidOperationException("Dense Movement4 checksum mismatch.");

        var structural = fixture.World.Create([fixture.Position, fixture.Velocity]);
        fixture.World.AddComponents([fixture.Auxiliary], structural);
        fixture.World.RemoveComponents([fixture.Auxiliary], structural);
        if (!fixture.World.Destroy(structural))
            throw new InvalidOperationException("Destroy invariant failed.");

        var created = fixture.World.Create([fixture.Position]);
        if (!created.IsAlive)
            throw new InvalidOperationException("Create invariant failed.");

        Console.WriteLine("Micro contract smoke passed: dense Movement2/Movement4 plus Add/Remove/Create/Destroy.");
    }
}
