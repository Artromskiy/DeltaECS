namespace DeltaECS.VersionAdapter;

using DVG.ECS;

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
    private readonly ComponentId[] _movement4Ids;
    private readonly Entity[] _movement2Entities;
    private readonly Entity[] _movement4Entities;

    public IterationScenario(int amount)
    {
        _amount = amount;
        var layouts = new ComponentLayoutRegistry();
        var dense = layouts.Register<DenseValue>(new SchemaId(950_000));
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
        _world.CreateBatch([dense], denseEntities);
        for (var i = 0; i < amount; i++)
        {
            _world.SetComponent(denseEntities[i], dense, new DenseValue { Value = i + 1 });
        }

        _movement2Entities = new Entity[amount];
        _world.CreateBatch([_position, _velocity], _movement2Entities);
        var movement2Description = QueryDescription.ForComponents(_position, _velocity);
        _movement2Query = _world.CreateQuery(in movement2Description);

        _movement4Entities = new Entity[amount];
        _world.CreateBatch(_movement4Ids, _movement4Entities);
        var movement4Description = QueryDescription.ForComponents(_movement4Ids);
        _movement4Query = _world.CreateQuery(in movement4Description);

        var denseDescription = QueryDescription.ForComponents(dense);
        _denseQuery = _world.CreateQuery(in denseDescription);
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
        long sum = 0;
        _world.Query(in _denseQuery, QueryAccess.Read, ref sum, static (ref long state, ref DenseChunkAccessor accessor) =>
        {
            var row = accessor.GetComponentRow<DenseValue>(0);
            for (var i = row.Length - 1; i >= 0; i--)
            {
                state += row[i].Value;
            }
        });

        var expected = (long)_amount * (_amount + 1) / 2;
        return sum == expected ? sum : throw new InvalidOperationException($"Dense checksum mismatch: {sum} != {expected}.");
    }

    public double Movement2()
    {
        double sum = 0;
        _world.Query(in _movement2Query, QueryAccess.Write, ref sum, static (ref double state, ref DenseChunkAccessor accessor) =>
        {
            var positions = accessor.GetComponentRow<Position>(0);
            var velocities = accessor.GetComponentRow<Velocity>(1);
            for (var i = positions.Length - 1; i >= 0; i--)
            {
                ref var position = ref positions[i];
                ref readonly var velocity = ref velocities[i];
                position.X += velocity.X / 60f;
                position.Y += velocity.Y / 60f;
                state += position.X + position.Y;
            }
        });

        var expected = _amount * (1 + 3 / 60f + 2 + 4 / 60f);
        return Math.Abs(sum - expected) < Math.Max(0.001, _amount * 0.000001)
            ? sum
            : throw new InvalidOperationException($"Movement2 checksum mismatch: {sum} != {expected}.");
    }

    public int Movement4()
    {
        var sum = 0;
        _world.Query(in _movement4Query, QueryAccess.Write, ref sum, static (ref int state, ref DenseChunkAccessor accessor) =>
        {
            var a = accessor.GetComponentRow<MovementA>(0);
            var b = accessor.GetComponentRow<MovementB>(1);
            var c = accessor.GetComponentRow<MovementC>(2);
            var d = accessor.GetComponentRow<MovementD>(3);
            for (var i = a.Length - 1; i >= 0; i--)
            {
                var updatedA = a[i].Value + d[i].Value;
                var updatedB = b[i].Value + d[i].Value;
                a[i].Value = updatedA;
                b[i].Value = updatedB;
                c[i].Value = (updatedA + updatedB) / 2;
                state += a[i].Value + b[i].Value + c[i].Value + d[i].Value;
            }
        });

        var expected = _amount * 20;
        return sum == expected ? sum : throw new InvalidOperationException($"Movement4 checksum mismatch: {sum} != {expected}.");
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
