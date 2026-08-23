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

var spec = QuerySpec.ForComponents(positionId, velocityId);
var query = world.CreateQuery(in spec);
var writePosition = query.Access<Position>(positionId, AccessMode.Write);
var readVelocity = query.Access<Velocity>(velocityId, AccessMode.Read);

Console.WriteLine("Dense archetype -> chunk -> slot iteration:");
using var scope = world.OpenQuery(in query);

var position = scope.BindWrite(writePosition);
var velocity = scope.BindRead(readVelocity);
var archetypes = scope.Archetypes;

while (archetypes.MoveNext())
{
    var chunks = archetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var positions = slots.Get(position);
        var velocities = slots.Get(velocity);

        while (slots.MoveNext())
        {
            ref var p = ref positions.Ref<Position>(slots);
            ref readonly var v = ref velocities.Ref<Velocity>(slots);
            p.X += v.X;
            p.Y += v.Y;
            Console.WriteLine($"  slot {slots.CurrentIndex}: ({p.X}, {p.Y})");
        }
    }
}

Console.WriteLine("Callback/action query iteration:");
var readPosition = query.Access<Position>(positionId, AccessMode.Read);
var actionState = new ActionState
{
    Position = readPosition,
    Velocity = readVelocity
};
world.Query(in query, ref actionState, static (ref ActionState state, ref QueryChunkCursor cursor) =>
{
    var positions = cursor.Get(state.Position);
    var velocities = cursor.Get(state.Velocity);
    while (cursor.MoveNext())
    {
        state.Checksum += positions.Ref<Position>(cursor).X + velocities.Ref<Velocity>(cursor).X;
    }
});
Console.WriteLine($"  observable checksum: {actionState.Checksum}");
Console.ReadLine();
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
    public ReadRequest Position;
    public ReadRequest Velocity;
    public float Checksum;
}
