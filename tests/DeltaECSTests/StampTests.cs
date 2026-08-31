namespace Delta.ECS.Tests;

using Delta.ECS;
using NUnit.Framework;

[TestFixture]
public sealed class StampTests
{
    [Test]
    public void StampIsEqualityOnlyAndCounterAdvancesLocally()
    {
        var counter = new StampCounter();

        Stamp first = counter.Next();
        Stamp second = counter.Next();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new Stamp(1)));
            Assert.That(second, Is.EqualTo(new Stamp(2)));
            Assert.That(typeof(Stamp).GetMethod("CompareTo"), Is.Null);
            Assert.That(typeof(Stamp).GetMethod("op_LessThan"), Is.Null);
            Assert.That(typeof(Stamp).GetMethod("op_Addition"), Is.Null);
        });
    }

    [Test]
    public void ComponentStampSumsEntityChunkAndArchetypeTerms()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_001));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(positionId);

        Archetype archetype = world.Archetypes[0];
        Chunk chunk = archetype.GetChunk(0);
        Stamp entityTerm = chunk.GetComponentStamp(0, 0);
        world.MarkChunkComponentWritten(chunk, 0, new Stamp(2));
        world.MarkArchetypeComponentWritten(archetype.Id, 0, new Stamp(3));

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp actual), Is.True);
        Assert.That(actual, Is.EqualTo(new Stamp(entityTerm.Value + 2 + 3)));
    }

    [Test]
    public void PointWriteIncrementsOnlyTheSelectedEntityComponentTerm()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_005));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(40_006));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity first = world.Create(positionId, velocityId);
        Entity second = world.Create(positionId, velocityId);
        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(first, velocityId, out Stamp velocityBefore), Is.True);
        Assert.That(world.TryGetComponentStamp(second, positionId, out Stamp secondBefore), Is.True);

        Assert.That(world.Set(first, positionId, new Position { X = 10 }), Is.True);

        Assert.That(world.TryGetComponentStamp(first, positionId, out Stamp firstAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(first, velocityId, out Stamp velocityAfter), Is.True);
        Assert.That(world.TryGetComponentStamp(second, positionId, out Stamp secondAfter), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(firstAfter, Is.EqualTo(new Stamp(firstBefore.Value + 1)));
            Assert.That(velocityAfter, Is.EqualTo(velocityBefore));
            Assert.That(secondAfter, Is.EqualTo(secondBefore));
        });
    }

    [Test]
    public void AddAndRemovePreserveSurvivingStampAndInitializeAddedComponent()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(40_011));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(40_012));
        using var world = new World(layouts, chunkCapacity: 2);
        Entity entity = world.Create(positionId);
        Assert.That(world.Set(entity, positionId, new Position { X = 7 }), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionBefore), Is.True);

        world.Add(new[] { velocityId }, entity);

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterAdd), Is.True);
        Assert.That(world.TryGetComponentStamp(entity, velocityId, out Stamp velocityAfterAdd), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterAdd, Is.EqualTo(positionBefore));
            Assert.That(velocityAfterAdd, Is.EqualTo(new Stamp(1)));
        });

        world.Remove(new[] { velocityId }, entity);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp positionAfterRemove), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(positionAfterRemove, Is.EqualTo(positionBefore));
            Assert.That(world.TryGetComponentStamp(entity, velocityId, out _), Is.False);
        });
    }

    [Test]
    public void QueryWritesIncrementArchetypeAndChunkTermsWithoutWorldStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_051));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        WriteAccess access = query.AccessWrite(positionId);

        using (var scope = world.BeginScope(in query))
        {
            var chunks = scope.Chunks;
            Assert.That(chunks.MoveNext(), Is.True);
            QueryChunk chunk = chunks.Current;
            QuerySlots slots = chunk.Slots;
            WriteRow row = slots.GetRow(access);
            Assert.That(slots.MoveNext(), Is.True);
            _ = row.Ref<Position>(in slots);
        }

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.Not.EqualTo(before));
    }

    [Test]
    public void ReadOnlyQueryDoesNotChangeComponentStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(40_061));
        using var world = new World(layouts);
        Entity entity = world.Create(positionId);
        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp before), Is.True);
        var query = world.CreateQuery(QuerySpec.WhereAll(positionId));
        ReadAccess access = query.AccessRead(positionId);

        using (var scope = world.BeginScope(in query))
        {
            var chunks = scope.Chunks;
            Assert.That(chunks.MoveNext(), Is.True);
            QuerySlots slots = chunks.Current.Slots;
            ReadRow row = slots.GetRow(access);
            Assert.That(slots.MoveNext(), Is.True);
            _ = row.Ref<Position>(in slots);
        }

        Assert.That(world.TryGetComponentStamp(entity, positionId, out Stamp after), Is.True);
        Assert.That(after, Is.EqualTo(before));
    }
}
