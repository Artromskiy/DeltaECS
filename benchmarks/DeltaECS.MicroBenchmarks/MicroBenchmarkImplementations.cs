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
    public static int IterateMovement2(
        MicroWorld fixture,
        in QueryHandle query,
        CursorWriteBinding<Position> position,
        CursorReadBinding<Velocity> velocity)
    {
        var checksum = 0;
        using var iterator = fixture.World.Iterate(in query);
        while (iterator.MoveNextArchetype())
        {
            while (iterator.MoveNextChunk())
            {
                var cursor = iterator.Current;
                var positions = cursor.Resolve(position);
                var velocities = cursor.Resolve(velocity);
                while (cursor.MoveNext())
                {
                    ref var p = ref positions[cursor];
                    ref readonly var v = ref velocities[cursor];
                    p.X += v.X;
                    p.Y += v.Y;
                    checksum += p.X + p.Y;
                }
            }
        }

        return checksum;
    }

    public static int IterateMovement4(
        MicroWorld fixture,
        in QueryHandle query,
        CursorWriteBinding<Movement4A> aBinding,
        CursorWriteBinding<Movement4B> bBinding,
        CursorWriteBinding<Movement4C> cBinding,
        CursorReadBinding<Movement4D> dBinding)
    {
        var checksum = 0;
        using var iterator = fixture.World.Iterate(in query);
        while (iterator.MoveNextArchetype())
        {
            while (iterator.MoveNextChunk())
            {
                var cursor = iterator.Current;
                var a = cursor.Resolve(aBinding);
                var b = cursor.Resolve(bBinding);
                var c = cursor.Resolve(cBinding);
                var d = cursor.Resolve(dBinding);
                while (cursor.MoveNext())
                {
                    ref var rowA = ref a[cursor];
                    ref var rowB = ref b[cursor];
                    ref var rowC = ref c[cursor];
                    ref readonly var rowD = ref d[cursor];
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

public class QueryIteratorIterationMicroBenchmarkImplementation
{
    [Params(100, 1_000, 10_000, 100_000)]
    public int Amount { get; set; }

    private MicroWorld _fixture = null!;
    private Entity[] _movement2Entities = null!;
    private Entity[] _movement4Entities = null!;
    private QueryHandle _movement2Query;
    private QueryHandle _movement4Query;
    private CursorWriteBinding<Position> _movement2Position;
    private CursorReadBinding<Velocity> _movement2Velocity;
    private CursorWriteBinding<Movement4A> _movement4A;
    private CursorWriteBinding<Movement4B> _movement4B;
    private CursorWriteBinding<Movement4C> _movement4C;
    private CursorReadBinding<Movement4D> _movement4D;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _movement2Entities = _fixture.CreateMoving(Amount);
        _movement4Entities = _fixture.CreateMovement4(Amount);

        var movement2 = QueryDescription.ForComponents(_fixture.Position, _fixture.Velocity);
        _movement2Query = _fixture.World.CreateQuery(in movement2);
        _movement2Position = _movement2Query.CursorBind<Position>(_fixture.Position, RowAccess.Write);
        _movement2Velocity = _movement2Query.CursorBind<Velocity>(_fixture.Velocity, RowAccess.Read);

        var movement4 = QueryDescription.ForComponents(
            _fixture.Movement4A,
            _fixture.Movement4B,
            _fixture.Movement4C,
            _fixture.Movement4D);
        _movement4Query = _fixture.World.CreateQuery(in movement4);
        _movement4A = _movement4Query.CursorBind<Movement4A>(_fixture.Movement4A, RowAccess.Write);
        _movement4B = _movement4Query.CursorBind<Movement4B>(_fixture.Movement4B, RowAccess.Write);
        _movement4C = _movement4Query.CursorBind<Movement4C>(_fixture.Movement4C, RowAccess.Write);
        _movement4D = _movement4Query.CursorBind<Movement4D>(_fixture.Movement4D, RowAccess.Read);
    }

    [IterationSetup(Target = nameof(Movement2Components))]
    public void ResetMovement2() => _fixture.ResetMoving(_movement2Entities);

    [IterationSetup(Target = nameof(Movement4Components))]
    public void ResetMovement4() => _fixture.ResetMovement4(_movement4Entities);

    [Benchmark]
    [InvocationCount(1)]
    public int Movement2Components() =>
        MicroBenchmarkKernels.IterateMovement2(_fixture, in _movement2Query, _movement2Position, _movement2Velocity);

    [Benchmark]
    [InvocationCount(1)]
    public int Movement4Components() =>
        MicroBenchmarkKernels.IterateMovement4(
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
        var movement2Description = QueryDescription.ForComponents(fixture.Position, fixture.Velocity);
        var movement2Query = fixture.World.CreateQuery(in movement2Description);
        var movement2Position = movement2Query.CursorBind<Position>(fixture.Position, RowAccess.Write);
        var movement2Velocity = movement2Query.CursorBind<Velocity>(fixture.Velocity, RowAccess.Read);

        var movement2Sum = MicroBenchmarkKernels.IterateMovement2(
            fixture,
            in movement2Query,
            movement2Position,
            movement2Velocity);
        if (movement2Sum != movement2Entities.Length * (movement2Entities.Length + 3))
            throw new InvalidOperationException("QueryIterator Movement2 checksum mismatch.");

        fixture.ResetMoving(movement2Entities);
        var movement4Entities = fixture.CreateMovement4(8);
        var movement4Description = QueryDescription.ForComponents(
            fixture.Movement4A,
            fixture.Movement4B,
            fixture.Movement4C,
            fixture.Movement4D);
        var movement4Query = fixture.World.CreateQuery(in movement4Description);
        var movement4A = movement4Query.CursorBind<Movement4A>(fixture.Movement4A, RowAccess.Write);
        var movement4B = movement4Query.CursorBind<Movement4B>(fixture.Movement4B, RowAccess.Write);
        var movement4C = movement4Query.CursorBind<Movement4C>(fixture.Movement4C, RowAccess.Write);
        var movement4D = movement4Query.CursorBind<Movement4D>(fixture.Movement4D, RowAccess.Read);

        var movement4Sum = MicroBenchmarkKernels.IterateMovement4(
            fixture,
            in movement4Query,
            movement4A,
            movement4B,
            movement4C,
            movement4D);
        if (movement4Sum != movement4Entities.Length * 20)
            throw new InvalidOperationException("QueryIterator Movement4 checksum mismatch.");

        var structural = fixture.World.Create([fixture.Position, fixture.Velocity]);
        fixture.World.AddComponents([fixture.Auxiliary], structural);
        fixture.World.RemoveComponents([fixture.Auxiliary], structural);
        if (!fixture.World.Destroy(structural))
            throw new InvalidOperationException("Destroy invariant failed.");

        var created = fixture.World.Create([fixture.Position]);
        if (!created.IsAlive)
            throw new InvalidOperationException("Create invariant failed.");

        Console.WriteLine("Micro contract smoke passed: QueryIterator Movement2/Movement4 and Add/Remove/Create/Destroy.");
    }
}
