using Delta.ECS;
using System.Globalization;

namespace DeltaECS.AotSample
{
    public static class Program
    {
        public static void Main()
        {
            var layouts = new ComponentLayoutRegistry();
            ComponentId positionId = layouts.Register<Position>(new SchemaId(1));
            ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(2));
            ComponentId markerId = layouts.Register<Marker>(new SchemaId(3));

            using var world = new World(layouts, chunkCapacity: 4);
            ArchetypeHandle movementArchetype = world.GetOrCreateArchetype(positionId, velocityId);
            Span<Entity> entities = stackalloc Entity[4];
            world.Create(movementArchetype, entities);

            for (int index = 0; index < entities.Length; index++)
            {
                world.Set(entities[index], positionId, new Position(index, index));
                world.Set(entities[index], velocityId, new Velocity(1, 0.5f));
            }

            QuerySpec specification = QuerySpec.WhereAll(positionId, velocityId);
            Query query = world.CreateQuery(in specification);

            world.ForEach(
                in query,
                static (ref Position position, in Velocity velocity) =>
                {
                    position.X += velocity.X;
                    position.Y += velocity.Y;
                });

            var movement = new MovementFunctor();
            world.ForEach(in query, ref movement);

            world.From(entities)
                .Where(in query)
                .ForEachEntity(
                    static (Entity entity, ref Position position, in Velocity velocity) =>
                    {
                        position.X += entity.Index + velocity.X;
                    });

            Entity marker = world.Create(markerId);
            world.Set(marker, markerId, new Marker(42));
            bool markerRead = world.TryGet<Marker>(marker, markerId, out Marker markerValue);
            bool markerDestroyed = world.Destroy(marker);

            float checksum = 0;
            for (int index = 0; index < entities.Length; index++)
            {
                Position position = world.Get<Position>(entities[index], positionId);
                checksum += position.X + position.Y;
            }

            Console.WriteLine(
                $"AOT sample: entities={world.AliveEntityCount}, checksum={checksum.ToString("0.0", CultureInfo.InvariantCulture)}, "
                + $"marker={markerRead && markerValue.Value == 42 && markerDestroyed}");
        }
    }

    public struct Position
    {
        public Position(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X;
        public float Y;
    }

    public struct Velocity
    {
        public Velocity(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X;
        public float Y;
    }

    public readonly struct Marker
    {
        public Marker(int value) => Value = value;

        public int Value { get; }
    }

    public struct MovementFunctor : IForEach
    {
        public void Invoke(ref Position position, in Velocity velocity)
        {
            position.Y += velocity.X;
        }
    }
}
