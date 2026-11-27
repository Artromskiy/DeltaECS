namespace Delta.ECS.Tests;

using System.Reflection;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityRecordCapacityTests
{
    [TestCase(1, 8)]
    [TestCase(8, 8)]
    [TestCase(9, 16)]
    [TestCase(72, 72)]
    [TestCase(73, 81)]
    public void SingleCreatesGrowRecordsByAtLeastEightOrOneEighth(int count, int capacity)
    {
        using var world = new World(initialEntityCapacity: 0);
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        for (int index = 0; index < count; index++)
        {
            world.Create(value);
        }

        Assert.That(RecordCapacity(world), Is.EqualTo(capacity));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BatchCreationReusesFreedRecordsBeforeGrowing(bool retainHandles)
    {
        using var world = new World(initialEntityCapacity: 72);
        ComponentId value = world.Components.Register<int>(new SchemaId(1));
        ComponentId extra = world.Components.Register<float>(new SchemaId(2));
        var chunk = new Entity[32];
        var other = new Entity[40];
        world.Create([value], chunk);
        world.Create([value, extra], other);
        world.Destroy(chunk);
        world.Destroy(other[0]);

        var added = new List<Entity>();
        var changes = world.ForEachChange<int>(value, (entity, kind, ref _) =>
        {
            Assert.That(kind, Is.EqualTo(ChangeKind.Added));
            added.Add(entity);
        });
        changes.Reset();

        Entity[] first = retainHandles ? new Entity[16] : [];
        Entity[] second = retainHandles ? new Entity[40] : [];
        Assert.That(world.Create(value, 16, first), Is.EqualTo(16));
        Assert.That(RecordCapacity(world), Is.EqualTo(72));
        Assert.That(world.Create(value, 40, second), Is.EqualTo(40));
        Assert.That(RecordCapacity(world), Is.EqualTo(95));

        changes.Invoke();
        Assert.That(added, Has.Count.EqualTo(56));
        Assert.That(added, Is.Unique);
        Assert.That(added.All(world.IsAlive), Is.True);
        if (retainHandles)
        {
            Assert.That(added, Is.EquivalentTo(first.Concat(second)));
        }

        Assert.That(chunk.Append(other[0]).Any(world.IsAlive), Is.False);
        Assert.That(other.Skip(1).All(world.IsAlive), Is.True);
    }

    private static int RecordCapacity(World world)
    {
        FieldInfo? field = typeof(World).GetField("_records", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(world) is EntityRecordStorage records
            ? records.Capacity
            : throw new MissingFieldException(nameof(World), "_records");
    }
}
