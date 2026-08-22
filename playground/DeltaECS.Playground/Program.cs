using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
var positionId = layouts.Register<Position>(new SchemaId(1));
var velocityId = layouts.Register<Velocity>(new SchemaId(2));
var world = new World(layouts, chunkCapacity: 4);
var archetype = world.GetArchetype(positionId, velocityId);

var entities = new Entity[8];
world.CreateBatch(archetype, entities);
for (var i = 0; i < entities.Length; i++)
{
    world.SetComponent(entities[i], positionId, new Position { X = i, Y = 0 });
    world.SetComponent(entities[i], velocityId, new Velocity { X = 1, Y = 0.5f });
}

var description = QueryDescription.ForComponents(positionId, velocityId);
var query = world.CreateQuery(in description);
var writePosition = query.CursorBind<Position>(positionId, RowAccess.Write);
var readVelocity = query.CursorBind<Velocity>(velocityId, RowAccess.Read);

Console.WriteLine("Dense archetype -> chunk -> slot iteration:");
using (var scope = world.IterateDense(in query))
{
    var position = scope.Prepare(writePosition);
    var velocity = scope.Prepare(readVelocity);
    var archetypes = scope.Archetypes;

    while (archetypes.MoveNext())
    {
        var chunks = archetypes.Current.Chunks;
        while (chunks.MoveNext())
        {
            var slots = chunks.Current.Slots;
            var positions = slots.Resolve(position);
            var velocities = slots.Resolve(velocity);

            while (slots.MoveNext())
            {
                ref var p = ref positions[slots];
                ref readonly var v = ref velocities[slots];
                p.X += v.X;
                p.Y += v.Y;
                Console.WriteLine($"  slot {slots.CurrentIndex}: ({p.X}, {p.Y})");
            }
        }
    }
}

Console.WriteLine("Callback/action query iteration:");
var readPosition = query.CursorBind<Position>(positionId, RowAccess.Read);
var actionState = new ActionState
{
    Position = readPosition,
    Velocity = readVelocity
};
world.QueryCursor(in query, ref actionState, static (ref ActionState state, ref DenseChunkCursor cursor) =>
{
    var positions = cursor.Resolve(state.Position);
    var velocities = cursor.Resolve(state.Velocity);
    while (cursor.MoveNext())
    {
        state.Checksum += positions[cursor].X + velocities[cursor].X;
    }
});
Console.WriteLine($"  observable checksum: {actionState.Checksum}");

public struct Position
{
    public float X;
    public float Y;
}

public struct Velocity
{
    public float X;
    public float Y;
}

public struct ActionState
{
    public CursorReadBinding<Position> Position;
    public CursorReadBinding<Velocity> Velocity;
    public float Checksum;
}
