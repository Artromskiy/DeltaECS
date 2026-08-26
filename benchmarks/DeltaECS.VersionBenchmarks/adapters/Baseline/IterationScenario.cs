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
    private readonly AccessRequest _denseAccess;
    private readonly AccessRequest _positionAccess;
    private readonly AccessRequest _velocityAccess;
    private readonly AccessRequest _movementAAccess;
    private readonly AccessRequest _movementBAccess;
    private readonly AccessRequest _movementCAccess;
    private readonly AccessRequest _movementDAccess;

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
        _world.CreateBatch(new[] { _dense }, denseEntities);
        for (var i = 0; i < amount; i++)
        {
            var value = new DenseValue { Value = i + 1 };
            _world.SetComponent(denseEntities[i], _dense, in value);
        }

        _movement2Entities = new Entity[amount];
        _world.CreateBatch(new[] { _position, _velocity }, _movement2Entities);
        var movement2Description = new QuerySpec(
            new[] { _position, _velocity },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        _movement2Query = _world.CreateQuery(in movement2Description);

        _movement4Entities = new Entity[amount];
        _world.CreateBatch(_movement4Ids, _movement4Entities);
        var movement4Description = new QuerySpec(
            _movement4Ids,
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        _movement4Query = _world.CreateQuery(in movement4Description);

        var denseDescription = new QuerySpec(
            new[] { _dense },
            Array.Empty<ComponentId>(),
            Array.Empty<ComponentId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>(),
            Array.Empty<TagId>());
        _denseQuery = _world.CreateQuery(in denseDescription);
        _denseAccess = _denseQuery.Access(_dense, AccessMode.Read);
        _positionAccess = _movement2Query.Access(_position, AccessMode.Write);
        _velocityAccess = _movement2Query.Access(_velocity, AccessMode.Read);
        _movementAAccess = _movement4Query.Access(_movement4Ids[0], AccessMode.Write);
        _movementBAccess = _movement4Query.Access(_movement4Ids[1], AccessMode.Write);
        _movementCAccess = _movement4Query.Access(_movement4Ids[2], AccessMode.Write);
        _movementDAccess = _movement4Query.Access(_movement4Ids[3], AccessMode.Read);
        ResetMovements();
    }

    public void ResetMovements()
    {
        for (var i = 0; i < _amount; i++)
        {
            var position = new Position { X = 1, Y = 2 };
            var velocity = new Velocity { X = 3, Y = 4 };
            var a = new MovementA { Value = 1 };
            var b = new MovementB { Value = 2 };
            var c = new MovementC { Value = 3 };
            var d = new MovementD { Value = 4 };
            _world.SetComponent(_movement2Entities[i], _position, in position);
            _world.SetComponent(_movement2Entities[i], _velocity, in velocity);
            _world.SetComponent(_movement4Entities[i], _movement4Ids[0], in a);
            _world.SetComponent(_movement4Entities[i], _movement4Ids[1], in b);
            _world.SetComponent(_movement4Entities[i], _movement4Ids[2], in c);
            _world.SetComponent(_movement4Entities[i], _movement4Ids[3], in d);
        }
    }

    public long DenseRead()
    {
        var context = new ReadContext<long>(_denseAccess);
        _world.Query(in _denseQuery, ref context, static (ref ReadContext<long> context, ref QueryChunkCursor cursor) =>
        {
            var row = cursor.GetRead(context.Access);
            while (cursor.MoveNext())
            {
                context.Sum += row.Ref<DenseValue>(cursor).Value;
            }
        });

        var expected = (long)_amount * (_amount + 1) / 2;
        return context.Sum == expected ? context.Sum : throw new InvalidOperationException($"Dense checksum mismatch: {context.Sum} != {expected}.");
    }

    public double Movement2()
    {
        var context = new Movement2Context(_positionAccess, _velocityAccess);
        _world.Query(in _movement2Query, ref context, static (ref Movement2Context context, ref QueryChunkCursor cursor) =>
        {
            var positions = cursor.GetWrite(context.Position);
            var velocities = cursor.GetRead(context.Velocity);
            while (cursor.MoveNext())
            {
                ref var position = ref positions.Ref<Position>(cursor);
                ref readonly var velocity = ref velocities.Ref<Velocity>(cursor);
                position.X += velocity.X / 60f;
                position.Y += velocity.Y / 60f;
                context.Sum += position.X + position.Y;
            }
        });

        return context.Sum;
    }

    public int Movement4()
    {
        var context = new Movement4Context(_movementAAccess, _movementBAccess, _movementCAccess, _movementDAccess);
        _world.Query(in _movement4Query, ref context, static (ref Movement4Context context, ref QueryChunkCursor cursor) =>
        {
            var a = cursor.GetWrite(context.A);
            var b = cursor.GetWrite(context.B);
            var c = cursor.GetWrite(context.C);
            var d = cursor.GetRead(context.D);
            while (cursor.MoveNext())
            {
                var updatedA = a.Ref<MovementA>(cursor).Value + d.Ref<MovementD>(cursor).Value;
                var updatedB = b.Ref<MovementB>(cursor).Value + d.Ref<MovementD>(cursor).Value;
                a.Ref<MovementA>(cursor).Value = updatedA;
                b.Ref<MovementB>(cursor).Value = updatedB;
                c.Ref<MovementC>(cursor).Value = (updatedA + updatedB) / 2;
                context.Sum += a.Ref<MovementA>(cursor).Value + b.Ref<MovementB>(cursor).Value + c.Ref<MovementC>(cursor).Value + d.Ref<MovementD>(cursor).Value;
            }
        });

        return context.Sum;
    }

    private struct ReadContext<T>(AccessRequest Access)
    {
        public AccessRequest Access = Access;
        public T Sum = default!;
    }

    private struct Movement2Context(AccessRequest position, AccessRequest velocity)
    {
        public AccessRequest Position = position;
        public AccessRequest Velocity = velocity;
        public double Sum;
    }

    private struct Movement4Context(AccessRequest a, AccessRequest b, AccessRequest c, AccessRequest d)
    {
        public AccessRequest A = a;
        public AccessRequest B = b;
        public AccessRequest C = c;
        public AccessRequest D = d;
        public int Sum;
    }
}
