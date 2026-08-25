using Delta.ECS;

var layouts = new ComponentLayoutRegistry();
var positionId = layouts.Register<Position>(new SchemaId(1));
var velocityId = layouts.Register<Velocity>(new SchemaId(2));
using var world = new World(layouts, chunkCapacity: 4);

var entities = new Entity[8];
var archetype = world.GetOrCreateArchetype(positionId, velocityId);
world.Create(archetype, entities);

for (var i = 0; i < entities.Length; i++)
{
    world.Set(entities[i], positionId, new Position { X = i });
    world.Set(entities[i], velocityId, new Velocity { X = 1, Y = 0.5f });
}

var spec = QuerySpec.WhereAll(positionId, velocityId);
var query = world.CreateQuery(in spec);

// Explicit Query is required before generated callbacks execute.
world.ForEach(in query, static (ref Position position, in Velocity velocity) =>
{
    position.X += velocity.X;
    position.Y += velocity.Y;
});
// Ordered sequence API: filter the supplied entities, then run an entity callback.
world.From(entities).Where(in query).ForEachEntity(static entity => Console.WriteLine($"updated {entity}"));
world.ForEach(in query, (ref Position p) => { });

var functor = new Functor();
world.ForEach(in query, ref functor);

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

struct Functor : IForEach
{
    public void Invoke(ref Position p) => _ = p;
}
