using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
var positionId = layouts.Register(typeof(Position), new SchemaId(1));
var velocityId = layouts.Register(typeof(Velocity), new SchemaId(2));
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
var writePosition = query.AccessWrite(positionId);
var readVelocity = query.AccessRead(velocityId);

Console.WriteLine("Dense archetype -> chunk -> slot iteration:");
using var scope = world.OpenQuery(in query);

var position = scope.Bind(writePosition);
var velocity = scope.Bind(readVelocity);
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

Console.WriteLine("Second dense query iteration checksum:");
var checksum = 0f;
using var checksumScope = world.OpenQuery(in query);
var checksumPosition = checksumScope.Bind(query.AccessWrite(positionId));
var checksumVelocity = checksumScope.Bind(query.AccessRead(velocityId));
var checksumArchetypes = checksumScope.Archetypes;

while (checksumArchetypes.MoveNext())
{
    var chunks = checksumArchetypes.Current.Chunks;
    while (chunks.MoveNext())
    {
        var slots = chunks.Current.Slots;
        var positions = slots.Get(checksumPosition);
        var velocities = slots.Get(checksumVelocity);

        while (slots.MoveNext())
        {
            checksum += positions.Ref<Position>(slots).X + velocities.Ref<Velocity>(slots).X;
        }
    }
}

Console.WriteLine($"  observable checksum: {checksum}");
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
