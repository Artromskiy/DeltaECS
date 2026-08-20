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
        return layouts;
    }

    public static (ComponentId Position, ComponentId Velocity, ComponentId Auxiliary, ComponentId Reference) Register(ComponentLayoutRegistry layouts)
        => (layouts.Register<Position>(new SchemaId(30_001)), layouts.Register<Velocity>(new SchemaId(30_002)),
            layouts.Register<Auxiliary>(new SchemaId(30_003)), layouts.Register<ReferenceValue>(new SchemaId(30_004)));
}

public struct Position { public int X; public int Y; }
public struct Velocity { public int X; public int Y; }
public struct Auxiliary { public int Value; }
public sealed class ReferenceValue { public int Value; }

internal sealed class MicroWorld
{
    public readonly ComponentLayoutRegistry Layouts = new();
    public readonly ComponentId Position;
    public readonly ComponentId Velocity;
    public readonly ComponentId Auxiliary;
    public readonly ComponentId Reference;

    public MicroWorld(int chunkCapacity = 64)
    {
        (Position, Velocity, Auxiliary, Reference) = MicroIds.Register(Layouts);
        World = new World(Layouts, initialEntityCapacity: 100_000, chunkCapacity: chunkCapacity);
    }

    public World World { get; }
    public ComponentId[] Moving => [Position, Velocity];
    public ComponentId[] Wide => [Position, Velocity, Auxiliary, Reference];

    public Entity[] CreateMoving(int amount)
    {
        var entities = new Entity[amount];
        World.CreateBatch(Moving, entities);
        for (var i = 0; i < entities.Length; i++)
        {
            World.SetComponent(entities[i], Position, new Position { X = i, Y = i + 1 });
            World.SetComponent(entities[i], Velocity, new Velocity { X = 1, Y = 2 });
        }
        return entities;
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
    private Entity[] _entities = null!;
    private QueryHandle _query;
    private ReadRowBinding<Position> _position;
    private ReadRowBinding<Velocity> _velocity;
    private WriteRowBinding<Position> _writePosition;
    private WriteRowBinding<Velocity> _writeVelocity;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MicroWorld();
        _entities = _fixture.CreateMoving(Amount);
        var description = QueryDescription.ForComponents(_fixture.Position, _fixture.Velocity);
        _query = _fixture.World.CreateQuery(in description);
        _position = _query.Bind<Position>(_fixture.Position, RowAccess.Read);
        _velocity = _query.Bind<Velocity>(_fixture.Velocity, RowAccess.Read);
        _writePosition = _query.Bind<Position>(_fixture.Position, RowAccess.Write);
        _writeVelocity = _query.Bind<Velocity>(_fixture.Velocity, RowAccess.Write);
    }

    [IterationSetup]
    public void Reset() => Setup();

    [Benchmark]
    public int Movement2Forward()
    {
        var state = new MovementState(_writePosition, _writeVelocity);
        _fixture.World.Query(in _query, ref state, static (ref MovementState s, ref DenseChunkAccessor chunk) =>
        {
            var positions = chunk.GetRow(s.Position);
            var velocities = chunk.GetRow(s.Velocity);
            for (var i = 0; i < chunk.SlotCount; i++)
                s.Checksum += (positions[i].X += velocities[i].X) + (positions[i].Y += velocities[i].Y);
        });
        return state.Checksum;
    }

    [Benchmark]
    public int Movement2Reverse()
    {
        var state = new MovementState(_writePosition, _writeVelocity);
        _fixture.World.Query(in _query, ref state, static (ref MovementState s, ref DenseChunkAccessor chunk) =>
        {
            var positions = chunk.GetRow(s.Position);
            var velocities = chunk.GetRow(s.Velocity);
            for (var i = chunk.SlotCount - 1; i >= 0; i--)
                s.Checksum += (positions[i].X += velocities[i].X) + (positions[i].Y += velocities[i].Y);
        });
        return state.Checksum;
    }

    [Benchmark]
    public int Movement4Reverse()
    {
        var state = new Movement4State(_writePosition, _writeVelocity);
        _fixture.World.Query(in _query, ref state, static (ref Movement4State s, ref DenseChunkAccessor chunk) =>
        {
            var positions = chunk.GetRow(s.WritePosition);
            var velocities = chunk.GetRow(s.WriteVelocity);
            for (var i = chunk.SlotCount - 1; i >= 0; i--)
            {
                positions[i].X += velocities[i].X;
                positions[i].Y += velocities[i].Y;
                s.Checksum += positions[i].X + positions[i].Y;
            }
        });
        return state.Checksum;
    }

    private struct MovementState(WriteRowBinding<Position> position, WriteRowBinding<Velocity> velocity)
    {
        public WriteRowBinding<Position> Position = position;
        public WriteRowBinding<Velocity> Velocity = velocity;
        public int Checksum;
    }

    private struct Movement4State(WriteRowBinding<Position> position, WriteRowBinding<Velocity> velocity)
    {
        public WriteRowBinding<Position> WritePosition = position;
        public WriteRowBinding<Velocity> WriteVelocity = velocity;
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
        _fixture.World.Query(in query, ref state, static (ref CountState s, ref DenseChunkAccessor chunk) => s.Count += chunk.SlotCount);
        return state.Count;
    }

    private struct CountState { public int Count; }
}

internal static class MicroContractSmoke
{
    public static void Run()
    {
        var fixture = new MicroWorld(chunkCapacity: 4);
        var entities = fixture.CreateMoving(8);
        var queryDescription = QueryDescription.ForComponents(fixture.Position, fixture.Velocity);
        var query = fixture.World.CreateQuery(in queryDescription);
        var position = query.Bind<Position>(fixture.Position, RowAccess.Read);
        var velocity = query.Bind<Velocity>(fixture.Velocity, RowAccess.Read);
        var state = new CheckState(position, velocity);
        fixture.World.Query(in query, ref state, static (ref CheckState s, ref DenseChunkAccessor chunk) =>
        {
            var p = chunk.GetRow(s.Position);
            var v = chunk.GetRow(s.Velocity);
            for (var i = chunk.SlotCount - 1; i >= 0; i--) s.Sum += p[i].X + v[i].X;
        });
        if (state.Sum != entities.Length * (entities.Length + 1) / 2) throw new InvalidOperationException("Movement checksum mismatch.");
        if (fixture.World.DestroyBatch(entities.AsSpan(0, 2)) != 2 || fixture.World.AliveEntityCount != 6)
            throw new InvalidOperationException("Destroy batch invariant failed.");
        Console.WriteLine("Micro contract smoke passed: bindings, reverse movement, create and destroy batch.");
    }

    private struct CheckState(ReadRowBinding<Position> position, ReadRowBinding<Velocity> velocity)
    {
        public ReadRowBinding<Position> Position = position;
        public ReadRowBinding<Velocity> Velocity = velocity;
        public int Sum;
    }
}
