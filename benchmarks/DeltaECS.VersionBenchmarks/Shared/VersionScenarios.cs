namespace DeltaECS.VersionAdapter;

using Delta.ECS;

public enum AtomicOperation
{
    Create,
    Destroy,
    Add,
    Remove
}

public enum BatchOperation
{
    Create,
    Destroy,
    Add,
    Remove
}

public sealed class IterationScenario
{
    private readonly int _amount;
    private readonly World _world;
    private readonly QueryHandle _denseQuery;
    private readonly QueryHandle _movement2Query;
    private readonly QueryHandle _movement4Query;
    private readonly ComponentId _position;
    private readonly ComponentId _velocity;
    private readonly ComponentId _dense;
    private readonly ComponentId[] _movement4Ids;
    private readonly Entity[] _movement2Entities;
    private readonly Entity[] _movement4Entities;
    private readonly CursorReadBinding<DenseValue> _denseBinding;
    private readonly CursorWriteBinding<Position> _positionBinding;
    private readonly CursorReadBinding<Velocity> _velocityBinding;
    private readonly CursorWriteBinding<MovementA> _movementABinding;
    private readonly CursorWriteBinding<MovementB> _movementBBinding;
    private readonly CursorWriteBinding<MovementC> _movementCBinding;
    private readonly CursorReadBinding<MovementD> _movementDBinding;

    public IterationScenario(int amount)
    {
        _amount = amount;
        var layouts = new ComponentLayoutRegistry();
        _dense = layouts.Register<DenseValue>(new SchemaId(950_000));
        _position = layouts.Register<Position>(new SchemaId(950_001));
        _velocity = layouts.Register<Velocity>(new SchemaId(950_002));
        _movement4Ids =
        [
            layouts.Register<MovementA>(new SchemaId(950_003)),
            layouts.Register<MovementB>(new SchemaId(950_004)),
            layouts.Register<MovementC>(new SchemaId(950_005)),
            layouts.Register<MovementD>(new SchemaId(950_006))
        ];

        _world = new World(layouts, initialEntityCapacity: amount * 3);

        var denseEntities = new Entity[amount];
        _world.CreateBatch([_dense], denseEntities);
        for (var i = 0; i < amount; i++)
        {
            _world.SetComponent(denseEntities[i], _dense, new DenseValue { Value = i + 1 });
        }

        _movement2Entities = new Entity[amount];
        _world.CreateBatch([_position, _velocity], _movement2Entities);
        var movement2Description = QueryDescription.ForComponents(_position, _velocity);
        _movement2Query = _world.CreateQuery(in movement2Description);

        _movement4Entities = new Entity[amount];
        _world.CreateBatch(_movement4Ids, _movement4Entities);
        var movement4Description = QueryDescription.ForComponents(_movement4Ids);
        _movement4Query = _world.CreateQuery(in movement4Description);

        var denseDescription = QueryDescription.ForComponents(_dense);
        _denseQuery = _world.CreateQuery(in denseDescription);
        _denseBinding = _denseQuery.CursorBind<DenseValue>(_dense, RowAccess.Read);
        _positionBinding = _movement2Query.CursorBind<Position>(_position, RowAccess.Write);
        _velocityBinding = _movement2Query.CursorBind<Velocity>(_velocity, RowAccess.Read);
        _movementABinding = _movement4Query.CursorBind<MovementA>(_movement4Ids[0], RowAccess.Write);
        _movementBBinding = _movement4Query.CursorBind<MovementB>(_movement4Ids[1], RowAccess.Write);
        _movementCBinding = _movement4Query.CursorBind<MovementC>(_movement4Ids[2], RowAccess.Write);
        _movementDBinding = _movement4Query.CursorBind<MovementD>(_movement4Ids[3], RowAccess.Read);
        ResetMovements();
    }

    public void ResetMovements()
    {
        for (var i = 0; i < _amount; i++)
        {
            _world.SetComponent(_movement2Entities[i], _position, new Position { X = 1, Y = 2 });
            _world.SetComponent(_movement2Entities[i], _velocity, new Velocity { X = 3, Y = 4 });
            _world.SetComponent(_movement4Entities[i], _movement4Ids[0], new MovementA { Value = 1 });
            _world.SetComponent(_movement4Entities[i], _movement4Ids[1], new MovementB { Value = 2 });
            _world.SetComponent(_movement4Entities[i], _movement4Ids[2], new MovementC { Value = 3 });
            _world.SetComponent(_movement4Entities[i], _movement4Ids[3], new MovementD { Value = 4 });
        }
    }

    public long DenseRead()
    {
        var state = new DenseState { Component = _denseBinding };
        _world.QueryCursor(in _denseQuery, ref state, static (ref DenseState current, ref DenseChunkCursor cursor) =>
        {
            var row = cursor.Resolve(current.Component);
            while (cursor.MoveNext()) current.Sum += row[cursor].Value;
        });

        var expected = (long)_amount * (_amount + 1) / 2;
        return state.Sum == expected ? state.Sum : throw new InvalidOperationException($"Dense checksum mismatch: {state.Sum} != {expected}.");
    }

    public double Movement2()
    {
        var state = new Movement2State { Position = _positionBinding, Velocity = _velocityBinding };
        _world.QueryCursor(in _movement2Query, ref state, static (ref Movement2State current, ref DenseChunkCursor cursor) =>
        {
            var positions = cursor.Resolve(current.Position);
            var velocities = cursor.Resolve(current.Velocity);
            while (cursor.MoveNext())
            {
                ref var position = ref positions[cursor];
                ref readonly var velocity = ref velocities[cursor];
                position.X += velocity.X / 60f;
                position.Y += velocity.Y / 60f;
                current.Sum += position.X + position.Y;
            }
        });

        // Movement benchmarks intentionally accumulate state across invocations so
        // BenchmarkDotNet can select a throughput invocation count. The dedicated
        // smoke resets both revisions and verifies that their returned checksums agree.
        return state.Sum;
    }

    public int Movement4()
    {
        var state = new Movement4State
        {
            A = _movementABinding,
            B = _movementBBinding,
            C = _movementCBinding,
            D = _movementDBinding
        };
        _world.QueryCursor(in _movement4Query, ref state, static (ref Movement4State current, ref DenseChunkCursor cursor) =>
        {
            var a = cursor.Resolve(current.A);
            var b = cursor.Resolve(current.B);
            var c = cursor.Resolve(current.C);
            var d = cursor.Resolve(current.D);
            while (cursor.MoveNext())
            {
                var updatedA = a[cursor].Value + d[cursor].Value;
                var updatedB = b[cursor].Value + d[cursor].Value;
                a[cursor].Value = updatedA;
                b[cursor].Value = updatedB;
                c[cursor].Value = (updatedA + updatedB) / 2;
                current.Sum += a[cursor].Value + b[cursor].Value + c[cursor].Value + d[cursor].Value;
            }
        });

        return state.Sum;
    }

    private struct DenseState { public CursorReadBinding<DenseValue> Component; public long Sum; }
    private struct Movement2State { public CursorWriteBinding<Position> Position; public CursorReadBinding<Velocity> Velocity; public double Sum; }
    private struct Movement4State
    {
        public CursorWriteBinding<MovementA> A;
        public CursorWriteBinding<MovementB> B;
        public CursorWriteBinding<MovementC> C;
        public CursorReadBinding<MovementD> D;
        public int Sum;
    }
}

public sealed class AtomicScenario
{
    private World _createWorld = null!;
    private World _destroyWorld = null!;
    private World _addWorld = null!;
    private World _removeWorld = null!;
    private ArchetypeHandle _createArchetype;
    private Entity _destroyEntity;
    private Entity _addEntity;
    private Entity _removeEntity;
    private ComponentId _extra;
    private ComponentId[] _extraIds = null!;

    public AtomicScenario() => Reset();

    public void Reset()
    {
        var layouts = new ComponentLayoutRegistry();
        var baseId = layouts.Register<StructuralBase>(new SchemaId(951_000));
        _extra = layouts.Register<StructuralExtra>(new SchemaId(951_001));
        _extraIds = [_extra];

        _createWorld = new World(layouts);
        _createArchetype = _createWorld.GetArchetype(baseId);

        _destroyWorld = new World(layouts);
        _destroyEntity = _destroyWorld.Create([baseId]);

        _addWorld = new World(layouts);
        _addEntity = _addWorld.Create([baseId]);

        _removeWorld = new World(layouts);
        _removeEntity = _removeWorld.Create([baseId, _extra]);
    }

    public int Run(AtomicOperation operation) => operation switch
    {
        AtomicOperation.Create => Create(),
        AtomicOperation.Destroy => Destroy(),
        AtomicOperation.Add => Add(),
        AtomicOperation.Remove => Remove(),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private int Create()
    {
        var entity = _createWorld.Create(_createArchetype);
        return _createWorld.IsAlive(entity) ? 1 : throw new InvalidOperationException("Atomic create failed.");
    }

    private int Destroy() => _destroyWorld.Destroy(_destroyEntity)
        ? 1
        : throw new InvalidOperationException("Atomic destroy failed.");

    private int Add()
    {
        _addWorld.AddComponents(_extraIds, _addEntity);
        return _addWorld.TryGetComponent<StructuralExtra>(_addEntity, _extra, out _)
            ? 1
            : throw new InvalidOperationException("Atomic add failed.");
    }

    private int Remove()
    {
        _removeWorld.RemoveComponents(_extraIds, _removeEntity);
        return !_removeWorld.TryGetComponent<StructuralExtra>(_removeEntity, _extra, out _)
            ? 1
            : throw new InvalidOperationException("Atomic remove failed.");
    }
}

public sealed class BatchScenario
{
    private readonly int _amount;
    private World _createWorld = null!;
    private World _destroyWorld = null!;
    private World _addWorld = null!;
    private World _removeWorld = null!;
    private ArchetypeHandle _createArchetype;
    private Entity[] _createOutput = null!;
    private Entity[] _destroyEntities = null!;
    private Entity[] _addEntities = null!;
    private Entity[] _removeEntities = null!;
    private ComponentId[] _extraIds = null!;

    public BatchScenario(int amount)
    {
        _amount = amount;
        Reset();
    }

    public void Reset()
    {
        var layouts = new ComponentLayoutRegistry();
        var baseId = layouts.Register<StructuralBase>(new SchemaId(952_000));
        var extra = layouts.Register<StructuralExtra>(new SchemaId(952_001));
        _extraIds = [extra];

        _createWorld = new World(layouts, initialEntityCapacity: _amount);
        _createArchetype = _createWorld.GetArchetype(baseId);
        _createOutput = new Entity[_amount];

        _destroyWorld = new World(layouts, initialEntityCapacity: _amount);
        _destroyEntities = new Entity[_amount];
        _destroyWorld.CreateBatch([baseId], _destroyEntities);

        _addWorld = new World(layouts, initialEntityCapacity: _amount);
        _addEntities = new Entity[_amount];
        _addWorld.CreateBatch([baseId], _addEntities);

        _removeWorld = new World(layouts, initialEntityCapacity: _amount);
        _removeEntities = new Entity[_amount];
        _removeWorld.CreateBatch([baseId, extra], _removeEntities);
    }

    public int Run(BatchOperation operation) => operation switch
    {
        BatchOperation.Create => _createWorld.CreateBatch(_createArchetype, _createOutput),
        BatchOperation.Destroy => _destroyWorld.DestroyBatch(_destroyEntities),
        BatchOperation.Add => _addWorld.AddComponents(_extraIds, _addEntities),
        BatchOperation.Remove => _removeWorld.RemoveComponents(_extraIds, _removeEntities),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}

internal struct DenseValue { public int Value; }
internal struct Position { public float X; public float Y; }
internal struct Velocity { public float X; public float Y; }
internal struct MovementA { public int Value; }
internal struct MovementB { public int Value; }
internal struct MovementC { public int Value; }
internal struct MovementD { public int Value; }
internal struct StructuralBase { }
internal struct StructuralExtra { }
