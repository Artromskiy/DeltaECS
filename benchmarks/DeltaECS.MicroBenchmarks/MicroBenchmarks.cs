using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal static class MicroIds
{
    public static ComponentLayoutRegistry Layouts()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Position>(new SchemaId(30_001));
        layouts.Register<Velocity>(new SchemaId(30_002));
        layouts.Register<Auxiliary>(new SchemaId(30_003));
        layouts.Register<ReferenceValue>(new SchemaId(30_004));
        layouts.Register<Movement4A>(new SchemaId(30_005));
        layouts.Register<Movement4B>(new SchemaId(30_006));
        layouts.Register<Movement4C>(new SchemaId(30_007));
        layouts.Register<Movement4D>(new SchemaId(30_008));
        return layouts;
    }

    public static (ComponentId Position, ComponentId Velocity, ComponentId Auxiliary, ComponentId Reference, ComponentId Movement4A, ComponentId Movement4B, ComponentId Movement4C, ComponentId Movement4D) Register(ComponentLayoutRegistry layouts)
        => (layouts.Register<Position>(new SchemaId(30_001)), layouts.Register<Velocity>(new SchemaId(30_002)),
            layouts.Register<Auxiliary>(new SchemaId(30_003)), layouts.Register<ReferenceValue>(new SchemaId(30_004)),
            layouts.Register<Movement4A>(new SchemaId(30_005)), layouts.Register<Movement4B>(new SchemaId(30_006)),
            layouts.Register<Movement4C>(new SchemaId(30_007)), layouts.Register<Movement4D>(new SchemaId(30_008)));
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
    public ComponentId[] Wide => [Position, Velocity, Auxiliary, Reference];
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

[MemoryDiagnoser]
public class EntityRecordResolveMicroBenchmarks
{
    [Params(1, 100, 10_000)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _entities = _fixture.CreateMoving(Amount);
    }

    [Benchmark]
    public int ResolveAndValidateGeneration()
    {
        var checksum = 0;
        for (var i = _entities.Length - 1; i >= 0; i--)
            checksum += _fixture.World.IsAlive(_entities[i]) ? _entities[i].Generation : -1;
        return checksum;
    }
}

[MemoryDiagnoser]
public class CreateKnownArchetypeMicroBenchmarks
{
    [Params(1, 100, 1_000)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private ArchetypeHandle _archetype;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _archetype = _fixture.World.GetArchetype(_fixture.Position, _fixture.Velocity);
    }

    [IterationSetup]
    public void Reset() => Setup();

    [Benchmark]
    public int CreateKnownArchetype()
    {
        var entities = new Entity[Amount];
        var count = _fixture.World.CreateBatch(_archetype, entities);
        return count + entities[^1].Index;
    }
}

[MemoryDiagnoser]
public class CachedBindingIterationMicroBenchmarks
{
    [Params(100, 1_000, 10_000, 100_000)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private Entity[] _movement2Entities = null!;
    private Entity[] _movement4Entities = null!;
    private QueryHandle _movement2Query;
    private QueryHandle _movement4Query;
    private CursorWriteBinding<Position> _movement2WritePosition;
    private CursorReadBinding<Velocity> _movement2ReadVelocity;
    private CursorWriteBinding<Movement4A> _movement4WriteA;
    private CursorWriteBinding<Movement4B> _movement4WriteB;
    private CursorWriteBinding<Movement4C> _movement4WriteC;
    private CursorReadBinding<Movement4D> _movement4ReadD;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _movement2Entities = _fixture.CreateMoving(Amount);
        _movement4Entities = _fixture.CreateMovement4(Amount);

        var movement2Description = QueryDescription.ForComponents(_fixture.Position, _fixture.Velocity);
        _movement2Query = _fixture.World.CreateQuery(in movement2Description);
        _movement2WritePosition = _movement2Query.CursorBind<Position>(_fixture.Position, RowAccess.Write);
        _movement2ReadVelocity = _movement2Query.CursorBind<Velocity>(_fixture.Velocity, RowAccess.Read);

        var movement4Description = QueryDescription.ForComponents(_fixture.Movement4A, _fixture.Movement4B, _fixture.Movement4C, _fixture.Movement4D);
        _movement4Query = _fixture.World.CreateQuery(in movement4Description);
        _movement4WriteA = _movement4Query.CursorBind<Movement4A>(_fixture.Movement4A, RowAccess.Write);
        _movement4WriteB = _movement4Query.CursorBind<Movement4B>(_fixture.Movement4B, RowAccess.Write);
        _movement4WriteC = _movement4Query.CursorBind<Movement4C>(_fixture.Movement4C, RowAccess.Write);
        _movement4ReadD = _movement4Query.CursorBind<Movement4D>(_fixture.Movement4D, RowAccess.Read);
    }

    [IterationSetup(Target = nameof(Movement2ComponentsForward))]
    public void ResetMovement2Forward() => _fixture.ResetMoving(_movement2Entities);

    [IterationSetup(Target = nameof(Movement2ComponentsReverse))]
    public void ResetMovement2Reverse() => _fixture.ResetMoving(_movement2Entities);

    [IterationSetup(Target = nameof(Movement4ComponentsForward))]
    public void ResetMovement4Forward() => _fixture.ResetMovement4(_movement4Entities);

    [IterationSetup(Target = nameof(Movement4ComponentsReverse))]
    public void ResetMovement4Reverse() => _fixture.ResetMovement4(_movement4Entities);

    [Benchmark]
    public int Movement2ComponentsForward()
    {
        var state = new Movement2State(_movement2WritePosition, _movement2ReadVelocity);
        _fixture.World.QueryCursor(in _movement2Query, ref state, static (ref Movement2State s, ref DenseChunkCursor chunk) =>
        {
            var positions = chunk.Resolve(s.Position);
            var velocities = chunk.Resolve(s.Velocity);
            var checksum = 0;
            while (chunk.MoveNext())
            {
                positions[chunk].X += velocities[chunk].X;
                positions[chunk].Y += velocities[chunk].Y;
                checksum += positions[chunk].X + positions[chunk].Y;
            }
            s.Checksum += checksum;
        });
        return state.Checksum;
    }

    [Benchmark]
    public int Movement2ComponentsReverse()
    {
        var state = new Movement2State(_movement2WritePosition, _movement2ReadVelocity);
        _fixture.World.QueryCursor(in _movement2Query, ref state, static (ref Movement2State s, ref DenseChunkCursor chunk) =>
        {
            var positions = chunk.Resolve(s.Position);
            var velocities = chunk.Resolve(s.Velocity);
            var checksum = 0;
            while (chunk.MoveNext())
            {
                positions[chunk].X += velocities[chunk].X;
                positions[chunk].Y += velocities[chunk].Y;
                checksum += positions[chunk].X + positions[chunk].Y;
            }
            s.Checksum += checksum;
        });
        return state.Checksum;
    }

    [Benchmark]
    public int Movement4ComponentsForward()
    {
        var state = new Movement4State(_movement4WriteA, _movement4WriteB, _movement4WriteC, _movement4ReadD);
        _fixture.World.QueryCursor(in _movement4Query, ref state, static (ref Movement4State s, ref DenseChunkCursor chunk) =>
        {
            // For each slot: a += d; b += d; c = (a + b) / 2; d stays read-only.
            var a = chunk.Resolve(s.A); var b = chunk.Resolve(s.B);
            var c = chunk.Resolve(s.C); var d = chunk.Resolve(s.D);
            var checksum = 0;
            while (chunk.MoveNext())
            {
                a[chunk].Value += d[chunk].Value;
                b[chunk].Value += d[chunk].Value;
                c[chunk].Value = (a[chunk].Value + b[chunk].Value) / 2;
                checksum += a[chunk].Value + b[chunk].Value + c[chunk].Value + d[chunk].Value;
            }
            s.Checksum += checksum;
        });
        return state.Checksum;
    }

    [Benchmark]
    public int Movement4ComponentsReverse()
    {
        var state = new Movement4State(_movement4WriteA, _movement4WriteB, _movement4WriteC, _movement4ReadD);
        _fixture.World.QueryCursor(in _movement4Query, ref state, static (ref Movement4State s, ref DenseChunkCursor chunk) =>
        {
            // For each slot: a += d; b += d; c = (a + b) / 2; d stays read-only.
            var a = chunk.Resolve(s.A); var b = chunk.Resolve(s.B);
            var c = chunk.Resolve(s.C); var d = chunk.Resolve(s.D);
            var checksum = 0;
            while (chunk.MoveNext())
            {
                a[chunk].Value += d[chunk].Value;
                b[chunk].Value += d[chunk].Value;
                c[chunk].Value = (a[chunk].Value + b[chunk].Value) / 2;
                checksum += a[chunk].Value + b[chunk].Value + c[chunk].Value + d[chunk].Value;
            }
            s.Checksum += checksum;
        });
        return state.Checksum;
    }

    private struct Movement2State(CursorWriteBinding<Position> position, CursorReadBinding<Velocity> velocity)
    {
        public CursorWriteBinding<Position> Position = position;
        public CursorReadBinding<Velocity> Velocity = velocity;
        public int Checksum;
    }

    private struct Movement4State(
        CursorWriteBinding<Movement4A> a,
        CursorWriteBinding<Movement4B> b,
        CursorWriteBinding<Movement4C> c,
        CursorReadBinding<Movement4D> d)
    {
        public CursorWriteBinding<Movement4A> A = a;
        public CursorWriteBinding<Movement4B> B = b;
        public CursorWriteBinding<Movement4C> C = c;
        public CursorReadBinding<Movement4D> D = d;
        public int Checksum;
    }
}

[MemoryDiagnoser]
public class AtomicStructuralMicroBenchmarks
{
    [Params(1, 4)] public int ChangeWidth { get; set; }
    private MicroWorld _fixture = null!;
    private Entity _entity;
    private ComponentId[] _change = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _entity = _fixture.World.Create([_fixture.Position, _fixture.Velocity]);
        _change = ChangeWidth == 1 ? [_fixture.Auxiliary] : [_fixture.Auxiliary, _fixture.Reference];
    }

    [Benchmark] public int AddRemove() { _fixture.World.AddComponents(_change, _entity); _fixture.World.RemoveComponents(_change, _entity); return _fixture.World.IsAlive(_entity) ? 1 : 0; }
    [Benchmark] public int CreateDestroy() { var entity = _fixture.World.Create([_fixture.Position]); return _fixture.World.Destroy(entity) ? 1 : 0; }
}

[MemoryDiagnoser]
public class ListBatchMicroBenchmarks
{
    [Params(100, 1_000, 10_000)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;
    private ComponentId[] _change = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _entities = _fixture.CreateMoving(Amount);
        _change = [_fixture.Auxiliary];
    }

    [Benchmark] public int CreateBatch() { var output = new Entity[Amount]; return _fixture.World.CreateBatch(_fixture.Moving, output); }
    [Benchmark] public int DestroyBatch() => _fixture.World.DestroyBatch(_entities);
    [Benchmark] public int AddBatch() => _fixture.World.AddComponents(_change, _entities);
    [Benchmark] public int RemoveBatch() => _fixture.World.RemoveComponents(_change, _entities);
}

[MemoryDiagnoser]
public class QueryBatchMicroBenchmarks
{
    [Params(100, 1_000, 10_000)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private QueryHandle _query;
    private ComponentId[] _change = null!;

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld();
        _fixture.CreateMoving(Amount);
        var description = new QueryDescription([_fixture.Position, _fixture.Velocity], [], [], [], [], []);
        _query = _fixture.World.CreateQuery(in description);
        _change = [_fixture.Auxiliary];
    }

    [Benchmark] public int DestroyMatching() => _fixture.World.Destroy(in _query);
    [Benchmark] public int AddMatching() => _fixture.World.AddComponents(in _query, _change);
    [Benchmark] public int RemoveMatching() => _fixture.World.RemoveComponents(in _query, _change);
}

[MemoryDiagnoser]
public class StorageAndOverlayMicroBenchmarks
{
    [Params(64, 100)] public int Amount { get; set; }
    private MicroWorld _fixture = null!;
    private Entity[] _entities = null!;
    private QueryHandle _taggedQuery;
    private QueryHandle _fullTaggedQuery;
    private QueryHandle _emptyTaggedQuery;
    private static readonly TagId s_tag = new(30_101);
    private static readonly TagId s_fullTag = new(30_102);
    private static readonly TagId s_emptyTag = new(30_103);

    [IterationSetup]
    public void Reset()
    {
        _fixture = new MicroWorld(chunkCapacity: 64);
        _entities = _fixture.CreateMoving(Amount);
        for (var i = 0; i < _entities.Length; i += 2)
            _fixture.World.AddTag(_entities[i], s_tag);
        for (var i = 0; i < _entities.Length; i++)
            _fixture.World.AddTag(_entities[i], s_fullTag);
        var description = new QueryDescription([_fixture.Position], [], [], [s_tag], [], []);
        _taggedQuery = _fixture.World.CreateQuery(in description);
        var fullDescription = new QueryDescription([_fixture.Position], [], [], [s_fullTag], [], []);
        _fullTaggedQuery = _fixture.World.CreateQuery(in fullDescription);
        var emptyDescription = new QueryDescription([_fixture.Position], [], [], [s_emptyTag], [], []);
        _emptyTaggedQuery = _fixture.World.CreateQuery(in emptyDescription);
    }

    [Benchmark] public int OverlayPartialQuery() => CountQuery();
    [Benchmark] public int OverlayFullQuery() => CountQuery(_fullTaggedQuery);
    [Benchmark] public int OverlayEmptyQuery() => CountQuery(_emptyTaggedQuery);
    [Benchmark] public int SwapBackDestroy() { var entity = _entities[0]; return _fixture.World.Destroy(entity) ? 1 : 0; }
    [Benchmark] public int ReferenceAwareMove() { var entity = _entities[0]; _fixture.World.AddComponents([_fixture.Reference], entity); return _fixture.World.IsAlive(entity) ? 1 : 0; }

    private int CountQuery() => CountQuery(_taggedQuery);

    private int CountQuery(QueryHandle query)
    {
        var state = new CountState();
        _fixture.World.QueryCursor(in query, ref state, static (ref CountState s, ref DenseChunkCursor chunk) => s.Count += chunk.SlotCount);
        return state.Count;
    }

    private struct CountState { public int Count; }
}

internal static class MicroContractSmoke
{
    public static void Run()
    {
        var fixture = new MicroWorld(chunkCapacity: 4);
        var movement2Entities = fixture.CreateMoving(8);
        var movement2QueryDescription = QueryDescription.ForComponents(fixture.Position, fixture.Velocity);
        var movement2Query = fixture.World.CreateQuery(in movement2QueryDescription);
        var movement2Position = movement2Query.CursorBind<Position>(fixture.Position, RowAccess.Read);
        var movement2Velocity = movement2Query.CursorBind<Velocity>(fixture.Velocity, RowAccess.Read);
        var movement2State = new Movement2SmokeState(movement2Position, movement2Velocity);
        fixture.World.QueryCursor(in movement2Query, ref movement2State, static (ref Movement2SmokeState s, ref DenseChunkCursor chunk) =>
        {
            var p = chunk.Resolve(s.Position);
            var v = chunk.Resolve(s.Velocity);
            while (chunk.MoveNext())
                s.Sum += p[chunk].X + v[chunk].X;
        });
        if (movement2State.Sum != movement2Entities.Length * (movement2Entities.Length + 1) / 2) throw new InvalidOperationException("Movement checksum mismatch.");
        fixture.ResetMoving(movement2Entities);
        movement2State.Sum = 0;
        fixture.World.QueryCursor(in movement2Query, ref movement2State, static (ref Movement2SmokeState s, ref DenseChunkCursor chunk) =>
        {
            var p = chunk.Resolve(s.Position);
            var v = chunk.Resolve(s.Velocity);
            while (chunk.MoveNext())
                s.Sum += p[chunk].X + v[chunk].X;
        });
        if (movement2State.Sum != movement2Entities.Length * (movement2Entities.Length + 1) / 2)
            throw new InvalidOperationException("Movement reset mismatch.");

        var movement4Entities = fixture.CreateMovement4(8);
        var movement4QueryDescription = QueryDescription.ForComponents(fixture.Movement4A, fixture.Movement4B, fixture.Movement4C, fixture.Movement4D);
        var movement4Query = fixture.World.CreateQuery(in movement4QueryDescription);
        var a = movement4Query.CursorBind<Movement4A>(fixture.Movement4A, RowAccess.Write);
        var b = movement4Query.CursorBind<Movement4B>(fixture.Movement4B, RowAccess.Write);
        var c = movement4Query.CursorBind<Movement4C>(fixture.Movement4C, RowAccess.Write);
        var d = movement4Query.CursorBind<Movement4D>(fixture.Movement4D, RowAccess.Read);
        var movement4State = new Movement4SmokeState(a, b, c, d);
        fixture.World.QueryCursor(in movement4Query, ref movement4State, static (ref Movement4SmokeState s, ref DenseChunkCursor chunk) =>
        {
            var a = chunk.Resolve(s.A);
            var b = chunk.Resolve(s.B);
            var c = chunk.Resolve(s.C);
            var d = chunk.Resolve(s.D);
            while (chunk.MoveNext())
            {
                a[chunk].Value += d[chunk].Value;
                b[chunk].Value += d[chunk].Value;
                c[chunk].Value = (a[chunk].Value + b[chunk].Value) / 2;
                s.Sum += a[chunk].Value + b[chunk].Value + c[chunk].Value + d[chunk].Value;
            }
        });
        if (movement4State.Sum != movement4Entities.Length * 20) throw new InvalidOperationException("Movement4 checksum mismatch.");
        fixture.ResetMovement4(movement4Entities);
        movement4State.Sum = 0;
        fixture.World.QueryCursor(in movement4Query, ref movement4State, static (ref Movement4SmokeState s, ref DenseChunkCursor chunk) =>
        {
            var a = chunk.Resolve(s.A);
            var b = chunk.Resolve(s.B);
            var c = chunk.Resolve(s.C);
            var d = chunk.Resolve(s.D);
            while (chunk.MoveNext())
            {
                a[chunk].Value += d[chunk].Value;
                b[chunk].Value += d[chunk].Value;
                c[chunk].Value = (a[chunk].Value + b[chunk].Value) / 2;
                s.Sum += a[chunk].Value + b[chunk].Value + c[chunk].Value + d[chunk].Value;
            }
        });
        if (movement4State.Sum != movement4Entities.Length * 20) throw new InvalidOperationException("Movement4 reset mismatch.");

        fixture.ResetMovement4(movement4Entities);
        var cursorA = movement4Query.CursorBind<Movement4A>(fixture.Movement4A, RowAccess.Read);
        var cursorB = movement4Query.CursorBind<Movement4B>(fixture.Movement4B, RowAccess.Read);
        var cursorC = movement4Query.CursorBind<Movement4C>(fixture.Movement4C, RowAccess.Read);
        var cursorD = movement4Query.CursorBind<Movement4D>(fixture.Movement4D, RowAccess.Read);
        var cursorState = new CursorSmokeState(cursorA, cursorB, cursorC, cursorD);
        fixture.World.QueryCursor(in movement4Query, ref cursorState, static (ref CursorSmokeState s, ref DenseChunkCursor chunk) =>
        {
            var a = chunk.Resolve(s.A); var b = chunk.Resolve(s.B);
            var c = chunk.Resolve(s.C); var d = chunk.Resolve(s.D);
            while (chunk.MoveNext())
                s.Sum += a[chunk].Value + b[chunk].Value + c[chunk].Value + d[chunk].Value;
        });
        if (cursorState.Sum != movement4Entities.Length * 10) throw new InvalidOperationException("Cursor checksum mismatch.");

        if (fixture.World.DestroyBatch(movement2Entities.AsSpan(0, 2)) != 2 || fixture.World.AliveEntityCount != 14)
            throw new InvalidOperationException("Destroy batch invariant failed.");
        Console.WriteLine("Micro contract smoke passed: bindings, reverse movement, movement4 reset, create and destroy batch.");
    }

    private struct Movement2SmokeState(CursorReadBinding<Position> position, CursorReadBinding<Velocity> velocity)
    {
        public CursorReadBinding<Position> Position = position;
        public CursorReadBinding<Velocity> Velocity = velocity;
        public int Sum;
    }

    private struct Movement4SmokeState(
        CursorWriteBinding<Movement4A> a,
        CursorWriteBinding<Movement4B> b,
        CursorWriteBinding<Movement4C> c,
        CursorReadBinding<Movement4D> d)
    {
        public CursorWriteBinding<Movement4A> A = a;
        public CursorWriteBinding<Movement4B> B = b;
        public CursorWriteBinding<Movement4C> C = c;
        public CursorReadBinding<Movement4D> D = d;
        public int Sum;
    }

    private struct CursorSmokeState(
        CursorReadBinding<Movement4A> a,
        CursorReadBinding<Movement4B> b,
        CursorReadBinding<Movement4C> c,
        CursorReadBinding<Movement4D> d)
    {
        public CursorReadBinding<Movement4A> A = a;
        public CursorReadBinding<Movement4B> B = b;
        public CursorReadBinding<Movement4C> C = c;
        public CursorReadBinding<Movement4D> D = d;
        public int Sum;
    }
}
