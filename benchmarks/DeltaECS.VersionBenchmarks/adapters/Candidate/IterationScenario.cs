namespace DeltaECS.VersionAdapter;

using Delta.ECS;

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

    public IterationScenario(int amount)
    {
        _amount = amount;
        var layouts = new ComponentLayoutRegistry();
        _dense = layouts.Register<DenseValue>(new SchemaId(950_000));
        _position = layouts.Register<Position>(new SchemaId(950_001));
        _velocity = layouts.Register<Velocity>(new SchemaId(950_002));
        _movement4Ids =
        [
            layouts.Register<MovementA>( new SchemaId(950_003)),
            layouts.Register<MovementB>( new SchemaId(950_004)),
            layouts.Register<MovementC>( new SchemaId(950_005)),
            layouts.Register<MovementD>( new SchemaId(950_006))
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
        _world.ForEach(in _denseQuery, ref sum,
            static (ref long checksum, in DenseValue value) => checksum += value.Value);

        var expected = (long)_amount * (_amount + 1) / 2;
        return sum == expected ? sum : throw new InvalidOperationException($"Dense checksum mismatch: {sum} != {expected}.");
    }

    public double Movement2()
    {
        double sum = 0;
        _world.ForEach(in _movement2Query, ref sum,
            static (ref double checksum, ref Position position, in Velocity velocity) =>
            {
                position.X += velocity.X / 60f;
                position.Y += velocity.Y / 60f;
                checksum += position.X + position.Y;
            });

        return sum;
    }

    public int Movement4()
    {
        int sum = 0;
        _world.ForEach(in _movement4Query, ref sum,
            static (ref int checksum,
                ref MovementA a,
                ref MovementB b,
                ref MovementC c,
                in MovementD d) =>
            {
                var updatedA = a.Value + d.Value;
                var updatedB = b.Value + d.Value;
                a.Value = updatedA;
                b.Value = updatedB;
                c.Value = (updatedA + updatedB) / 2;
                checksum += a.Value + b.Value + c.Value + d.Value;
            });

        return sum;
    }
}
