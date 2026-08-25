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
    private readonly Query _denseQuery;
    private readonly Query _movement2Query;
    private readonly Query _movement4Query;
    private readonly ComponentId _position;
    private readonly ComponentId _velocity;
    private readonly ComponentId _dense;
    private readonly ComponentId[] _movement4Ids;
    private readonly Entity[] _movement2Entities;
    private readonly Entity[] _movement4Entities;
    private readonly ReadAccess _denseBinding;
    private readonly WriteAccess _positionBinding;
    private readonly ReadAccess _velocityBinding;
    private readonly WriteAccess _movementABinding;
    private readonly WriteAccess _movementBBinding;
    private readonly WriteAccess _movementCBinding;
    private readonly ReadAccess _movementDBinding;

    public IterationScenario(int amount)
    {
        _amount = amount;
        var layouts = new ComponentLayoutRegistry();
        _dense = layouts.Register(typeof(DenseValue), new SchemaId(950_000));
        _position = layouts.Register(typeof(Position), new SchemaId(950_001));
        _velocity = layouts.Register(typeof(Velocity), new SchemaId(950_002));
        _movement4Ids =
        [
            layouts.Register(typeof(MovementA), new SchemaId(950_003)),
            layouts.Register(typeof(MovementB), new SchemaId(950_004)),
            layouts.Register(typeof(MovementC), new SchemaId(950_005)),
            layouts.Register(typeof(MovementD), new SchemaId(950_006))
        ];

        _world = new World(layouts, initialEntityCapacity: amount * 3);

        var denseEntities = new Entity[amount];
        _world.Create([_dense], denseEntities);
        for (var i = 0; i < amount; i++)
        {
            _world.Set(denseEntities[i], _dense, new DenseValue { Value = i + 1 });
        }

        _movement2Entities = new Entity[amount];
        _world.Create([_position, _velocity], _movement2Entities);
        var movement2Description = QuerySpec.WhereAll(_position, _velocity);
        _movement2Query = _world.CreateQuery(in movement2Description);

        _movement4Entities = new Entity[amount];
        _world.Create(_movement4Ids, _movement4Entities);
        var movement4Description = QuerySpec.WhereAll(_movement4Ids);
        _movement4Query = _world.CreateQuery(in movement4Description);

        var denseDescription = QuerySpec.WhereAll(_dense);
        _denseQuery = _world.CreateQuery(in denseDescription);
        _denseBinding = _denseQuery.AccessRead(_dense);
        _positionBinding = _movement2Query.AccessWrite(_position);
        _velocityBinding = _movement2Query.AccessRead(_velocity);
        _movementABinding = _movement4Query.AccessWrite(_movement4Ids[0]);
        _movementBBinding = _movement4Query.AccessWrite(_movement4Ids[1]);
        _movementCBinding = _movement4Query.AccessWrite(_movement4Ids[2]);
        _movementDBinding = _movement4Query.AccessRead(_movement4Ids[3]);
        ResetMovements();
    }

    public void ResetMovements()
    {
        for (var i = 0; i < _amount; i++)
        {
            _world.Set(_movement2Entities[i], _position, new Position { X = 1, Y = 2 });
            _world.Set(_movement2Entities[i], _velocity, new Velocity { X = 3, Y = 4 });
            _world.Set(_movement4Entities[i], _movement4Ids[0], new MovementA { Value = 1 });
            _world.Set(_movement4Entities[i], _movement4Ids[1], new MovementB { Value = 2 });
            _world.Set(_movement4Entities[i], _movement4Ids[2], new MovementC { Value = 3 });
            _world.Set(_movement4Entities[i], _movement4Ids[3], new MovementD { Value = 4 });
        }
    }

    public long DenseRead()
    {
        long sum = 0;
        using var scope = _world.OpenQuery(in _denseQuery);
        var dense = _denseBinding;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var row = slots.GetRow(dense);
                while (slots.MoveNext())
                {
                    sum += row.Ref<DenseValue>(slots).Value;
                }
            }
        }

        var expected = (long)_amount * (_amount + 1) / 2;
        return sum == expected ? sum : throw new InvalidOperationException($"Dense checksum mismatch: {sum} != {expected}.");
    }

    public double Movement2()
    {
        double sum = 0;
        using var scope = _world.OpenQuery(in _movement2Query);
        var position = _positionBinding;
        var velocity = _velocityBinding;
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var slots = chunks.Current.Slots;
                var positions = slots.GetRow(position);
                var velocities = slots.GetRow(velocity);
                while (slots.MoveNext())
                {
                    ref var currentPosition = ref positions.Ref<Position>(slots);
                    ref readonly var currentVelocity = ref velocities.Ref<Velocity>(slots);
                    currentPosition.X += currentVelocity.X / 60f;
                    currentPosition.Y += currentVelocity.Y / 60f;
                    sum += currentPosition.X + currentPosition.Y;
                }
            }
        }

        // Movement benchmarks intentionally accumulate state across invocations so
        // BenchmarkDotNet can select a throughput invocation count. The dedicated
        // smoke resets both revisions and verifies that their returned checksums agree.
        return sum;
    }

    public int Movement4()
    {
        int sum = 0;
        using var scope = _world.OpenQuery(in _movement4Query);
        var aAccess = _movementABinding;
        var bAccess = _movementBBinding;
        var cAccess = _movementCBinding;
        var dAccess = _movementDBinding;
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
                    var updatedA = a.Ref<MovementA>(slots).Value + d.Ref<MovementD>(slots).Value;
                    var updatedB = b.Ref<MovementB>(slots).Value + d.Ref<MovementD>(slots).Value;
                    a.Ref<MovementA>(slots).Value = updatedA;
                    b.Ref<MovementB>(slots).Value = updatedB;
                    c.Ref<MovementC>(slots).Value = (updatedA + updatedB) / 2;
                    sum += a.Ref<MovementA>(slots).Value + b.Ref<MovementB>(slots).Value + c.Ref<MovementC>(slots).Value + d.Ref<MovementD>(slots).Value;
                }
            }
        }

        return sum;
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
        var baseId = layouts.Register(typeof(StructuralBase), new SchemaId(951_000));
        _extra = layouts.Register(typeof(StructuralExtra), new SchemaId(951_001));
        _extraIds = [_extra];

        _createWorld = new World(layouts);
        _createArchetype = _createWorld.GetOrCreateArchetype(baseId);

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
        _addWorld.Add(_extraIds, _addEntity);
        return _addWorld.TryGet<StructuralExtra>(_addEntity, _extra, out _)
            ? 1
            : throw new InvalidOperationException("Atomic add failed.");
    }

    private int Remove()
    {
        _removeWorld.Remove(_extraIds, _removeEntity);
        return !_removeWorld.TryGet<StructuralExtra>(_removeEntity, _extra, out _)
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
        var baseId = layouts.Register(typeof(StructuralBase), new SchemaId(952_000));
        var extra = layouts.Register(typeof(StructuralExtra), new SchemaId(952_001));
        _extraIds = [extra];

        _createWorld = new World(layouts, initialEntityCapacity: _amount);
        _createArchetype = _createWorld.GetOrCreateArchetype(baseId);
        _createOutput = new Entity[_amount];

        _destroyWorld = new World(layouts, initialEntityCapacity: _amount);
        _destroyEntities = new Entity[_amount];
        _destroyWorld.Create([baseId], _destroyEntities);

        _addWorld = new World(layouts, initialEntityCapacity: _amount);
        _addEntities = new Entity[_amount];
        _addWorld.Create([baseId], _addEntities);

        _removeWorld = new World(layouts, initialEntityCapacity: _amount);
        _removeEntities = new Entity[_amount];
        _removeWorld.Create([baseId, extra], _removeEntities);
    }

    public int Run(BatchOperation operation) => operation switch
    {
        BatchOperation.Create => _createWorld.Create(_createArchetype, _createOutput),
        BatchOperation.Destroy => _destroyWorld.Destroy(_destroyEntities),
        BatchOperation.Add => _addWorld.Add(_extraIds, _addEntities),
        BatchOperation.Remove => _removeWorld.Remove(_extraIds, _removeEntities),
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
