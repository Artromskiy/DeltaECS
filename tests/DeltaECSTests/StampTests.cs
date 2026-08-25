using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class StampTests
{
    [Test]
    public void StampIsEqualityOnlyAndMutationSourceAdvancesMonotonically()
    {
        var source = new MutationStampSource();

        Stamp first = source.Next();
        Stamp second = source.Next();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new Stamp(1)));
            Assert.That(second, Is.EqualTo(new Stamp(2)));
            Assert.That(source.Current, Is.EqualTo(second));
            Assert.That(typeof(Stamp).GetMethod("CompareTo"), Is.Null);
            Assert.That(typeof(Stamp).GetMethod("op_LessThan"), Is.Null);
            Assert.That(typeof(Stamp).GetMethod("op_Addition"), Is.Null);
        });
    }

    [Test]
    public void SetComponentChangesOnlySelectedEntityAndComponentStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_001));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_002));
        using var world = new World(layouts, chunkCapacity: 4);
        Entity first = world.Create(positionId, velocityId);
        Entity second = world.Create(positionId, velocityId);
        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstPositionBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(first, velocityId, out Stamp firstVelocityBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(second, positionId, out Stamp secondPositionBefore), Is.True);

        Assert.That(world.Set(first, positionId, new Position(10)), Is.True);

        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstPositionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(first, velocityId, out Stamp firstVelocityAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(second, positionId, out Stamp secondPositionAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(firstPositionAfter, Is.EqualTo(world.Stamp));
            Assert.That(firstPositionAfter, Is.Not.EqualTo(firstPositionBefore));
            Assert.That(firstVelocityAfter, Is.EqualTo(firstVelocityBefore));
            Assert.That(secondPositionAfter, Is.EqualTo(secondPositionBefore));
        });
    }

    [Test]
    public void AddAndRemovePreserveSurvivingStampAndStampAddedComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_011));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_012));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new Position(7)), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);

        world.Add(new[] { velocityId }, entity);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterAdd), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterAdd), Is.True);
        Stamp addStamp = world.Stamp;
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterAdd, Is.EqualTo(positionBefore));
            Assert.That(velocityAfterAdd, Is.EqualTo(addStamp));
        });

        world.Remove(new[] { velocityId }, entity);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterRemove), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterRemove, Is.EqualTo(positionBefore));
            Assert.That(world.TryGetComponentStamp(entity, velocityId, out _), Is.False);
            Assert.That(world.Stamp, Is.Not.EqualTo(addStamp));
        });
    }

    [Test]
    public void QueryBlockMovePreservesSurvivingStampsAndStampsAddedRows()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_016));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_017));
        using var world = new World(layouts, chunkCapacity: 2);
        Span<Entity> entities = stackalloc Entity[3];
        Assert.That(world.Create(stackalloc[] { positionId }, entities), Is.EqualTo(entities.Length));
        var preserved = new Stamp[entities.Length];
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Set(entities[index], positionId, new Position(index)), Is.True);
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out preserved[index]), Is.True);
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        Assert.That(world.Add(in query, new[] { velocityId }), Is.EqualTo(entities.Length));
        Stamp operationStamp = world.Stamp;

        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.TryGetComponentStamp(entities[index], positionId, out Stamp position), Is.True);
            Assert.That(world.TryGetComponentStamp(entities[index], velocityId, out Stamp velocity), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(position, Is.EqualTo(preserved[index]));
                Assert.That(velocity, Is.EqualTo(operationStamp));
            });
        }
    }

    [Test]
    public void SwapBackMovesComponentStampWithSurvivingValue()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_021));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity removed = world.Create(positionId);
        Entity survivor = world.Create(positionId);
        Assert.That(world.Set(survivor, positionId, new Position(99)), Is.True);
        Assert.That(world.TryGetComponentStamp(survivor, positionId, out Stamp before), Is.True);

        Assert.That(world.Destroy(removed), Is.True);

        Assert.That(world.TryGet(survivor, positionId, out Position value), Is.True);
        Assert.That(world.TryGetComponentStamp(survivor, positionId, out Stamp after), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(value.Value, Is.EqualTo(99));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public void DestroyedOrStaleEntityCannotExposeComponentStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_031));
        using var world = new World(layouts, chunkCapacity: 1);
        Entity stale = world.Create(positionId);
        Assert.That(world.Destroy(stale), Is.True);
        Entity replacement = world.Create(positionId);

        Assert.Multiple(() =>
        {
            Assert.That(replacement.Index, Is.EqualTo(stale.Index));
            Assert.That(replacement.Generation, Is.Not.EqualTo(stale.Generation));
            Assert.That(world.TryGetComponentStamp(stale, positionId, out Stamp staleStamp), Is.False);
            Assert.That(staleStamp, Is.EqualTo(default(Stamp)));
            Assert.That(world.TryGetComponentStamp(replacement, positionId, out _), Is.True);
        });
    }

    [Test]
    public void StructuralNoOpsDoNotAdvanceWorldStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_041));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_042));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Stamp before = world.Stamp;

        world.Add(new[] { positionId }, entity);
        world.Remove(new[] { velocityId }, entity);
        Assert.That(world.Destroy(Entity.Null), Is.False);
        Assert.That(world.Set(entity, velocityId, new Velocity(1)), Is.False);

        Assert.That(world.Stamp, Is.EqualTo(before));
    }

    [Test]
    public void QueryWriteChangesOnlyRequestedComponentRowStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_051));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_052));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId, velocityId);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        WriteAccess positionAccess = query.AccessWrite(positionId);
        ReadAccess velocityAccess = query.AccessRead(velocityId);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityBefore), Is.True);

        using (var scope = world.OpenQuery(in query))
        {
            WriteAccess position = positionAccess;
            ReadAccess velocity = velocityAccess;
            var archetypes = scope.Archetypes;
            while (archetypes.MoveNext())
            {
                var chunks = archetypes.Current.Chunks;
                while (chunks.MoveNext())
                {
                    var slots = chunks.Current.Slots;
                    WriteRow positions = slots.GetRow(position);
                    ReadRow velocities = slots.GetRow(velocity);
                    while (slots.MoveNext())
                    {
                        ref Position current = ref positions.Ref<Position>(slots);
                        ref readonly Velocity ignored = ref velocities.Ref<Velocity>(slots);
                        current = new Position(current.Value + ignored.Value + 1);
                    }
                }
            }
        }

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfter, Is.EqualTo(world.Stamp));
            Assert.That(velocityAfter, Is.EqualTo(velocityBefore));
        });
    }

    [Test]
    public void GeneratedWholeChunkWriteMaterializesExactEntityStampsOnDemand()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_061));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_062));
        using var world = new World(layouts, chunkCapacity: 4);
        var entities = new Entity[3];
        world.Create([positionId, velocityId], entities);
        for (int index = 0; index < entities.Length; index++)
        {
            Assert.That(world.Set(entities[index], positionId, new Position(index)), Is.True);
            Assert.That(world.Set(entities[index], velocityId, new Velocity(1)), Is.True);
        }

        var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));
        var functor = new StampWriteFunctor();
        world.ForEach(in query, ref functor);
        Stamp generatedStamp = world.Stamp;

        foreach (Entity entity in entities)
        {
            Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp stamp), Is.True);
            Assert.That(stamp, Is.EqualTo(generatedStamp));
        }

        Assert.That(world.Set(entities[1], positionId, new Position(42)), Is.True);
        Stamp pointStamp = world.Stamp;
        Assert.That(world.TryGetComponentStamp(entities[0], positionId, out Stamp firstStamp), Is.True);
        Assert.That(world.TryGetComponentStamp(entities[1], positionId, out Stamp secondStamp), Is.True);
        Assert.That(world.TryGetComponentStamp(entities[2], positionId, out Stamp thirdStamp), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(firstStamp, Is.EqualTo(generatedStamp));
            Assert.That(secondStamp, Is.EqualTo(pointStamp));
            Assert.That(thirdStamp, Is.EqualTo(generatedStamp));
        });

        Assert.That(world.Destroy(entities[0]), Is.True);
        Assert.That(world.TryGetComponentStamp(entities[2], positionId, out Stamp movedStamp), Is.True);
        Assert.That(movedStamp, Is.EqualTo(generatedStamp));
    }

    internal struct StampWriteFunctor : IForEach
    {
        public void Invoke(ref Position position, in Velocity velocity)
            => position = new Position(position.Value + velocity.Value);
    }

    internal readonly record struct Position(int Value);

    internal readonly record struct Velocity(int Value);
}
