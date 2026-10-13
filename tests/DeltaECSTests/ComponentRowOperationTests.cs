using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ComponentRowOperationTests
{
    [Test]
    public void SwapBackCopiesValueManagedStructAndClassRows()
    {
        var layouts = new ComponentRegistry();
        var valueId = layouts.Register<int>(new SchemaId(10_001));
        var managedStructId = layouts.Register<ManagedPayload>(new SchemaId(10_002));
        var classId = layouts.Register<ReferencePayload>(new SchemaId(10_003));
        using var world = new World(layouts);
        var removed = world.Create([valueId, managedStructId, classId]);
        var survivor = world.Create([valueId, managedStructId, classId]);
        var reference = new ReferencePayload("survivor");
        world.GetRef<int>(survivor, valueId) = 42;
        world.GetRef<ManagedPayload>(survivor, managedStructId) = new ManagedPayload("managed");
        world.GetRef<ReferencePayload>(survivor, classId) = reference;

        Assert.That(world.Destroy(removed), Is.True);
        Assert.That(world.TryGet(survivor, valueId, out int value), Is.True);
        Assert.That(world.TryGet(survivor, managedStructId, out ManagedPayload managed), Is.True);
        Assert.That(world.TryGet(survivor, classId, out ReferencePayload? actualReference), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(value, Is.EqualTo(42));
            Assert.That(managed.Text, Is.EqualTo("managed"));
            Assert.That(actualReference, Is.SameAs(reference));
        });
    }

    [Test]
    public void ReusedCreateSlotInitializesAllRowsToDefault()
    {
        var layouts = new ComponentRegistry();
        var valueId = layouts.Register<int>(new SchemaId(10_011));
        var managedStructId = layouts.Register<ManagedPayload>(new SchemaId(10_012));
        var classId = layouts.Register<ReferencePayload>(new SchemaId(10_013));
        using var world = new World(layouts);
        var old = world.Create([valueId, managedStructId, classId]);
        world.GetRef<int>(old, valueId) = 99;
        world.GetRef<ManagedPayload>(old, managedStructId) = new ManagedPayload("old");
        world.GetRef<ReferencePayload>(old, classId) = new ReferencePayload("old");
        world.Destroy(old);

        var current = world.Create([valueId, managedStructId, classId]);
        world.TryGet(current, valueId, out int value);
        world.TryGet(current, managedStructId, out ManagedPayload managed);
        world.TryGet(current, classId, out ReferencePayload? reference);
        Assert.Multiple(() =>
        {
            Assert.That(value, Is.Zero);
            Assert.That(managed.Text, Is.Null);
            Assert.That(reference, Is.Null);
        });
    }

    [Test]
    public void ReusedTransitionSlotInitializesOnlyAddedRows()
    {
        var layouts = new ComponentRegistry();
        var sharedId = layouts.Register<int>(new SchemaId(10_021));
        var addedValueId = layouts.Register<int>(new SchemaId(10_022));
        var addedReferenceId = layouts.Register<ReferencePayload>(new SchemaId(10_023));
        using var world = new World(layouts);

        var oldTarget = world.Create([sharedId, addedValueId, addedReferenceId]);
        world.GetRef<int>(oldTarget, addedValueId) = 123;
        world.GetRef<ReferencePayload>(oldTarget, addedReferenceId) = new ReferencePayload("old");
        world.Destroy(oldTarget);

        var source = world.Create([sharedId]);
        world.GetRef<int>(source, sharedId) = 77;
        world.Add(source, [addedValueId, addedReferenceId]);

        world.TryGet(source, sharedId, out int shared);
        world.TryGet(source, addedValueId, out int addedValue);
        world.TryGet(source, addedReferenceId, out ReferencePayload? addedReference);
        Assert.Multiple(() =>
        {
            Assert.That(shared, Is.EqualTo(77));
            Assert.That(addedValue, Is.Zero);
            Assert.That(addedReference, Is.Null);
        });
    }

    [Test]
    public void SwapBackDoesNotClearUnmanagedTailButClearsReferenceTail()
    {
        var layouts = new ComponentRegistry();
        var valueId = layouts.Register<int>(new SchemaId(10_031));
        var referenceId = layouts.Register<ReferencePayload>(new SchemaId(10_032));
        using var world = new World(layouts);
        var removed = world.Create([valueId, referenceId]);
        var survivor = world.Create([valueId, referenceId]);
        var survivorReference = new ReferencePayload("survivor");
        world.GetRef<int>(removed, valueId) = 11;
        world.GetRef<int>(survivor, valueId) = 22;
        world.GetRef<ReferencePayload>(survivor, referenceId) = survivorReference;

        Assert.That(world.Destroy(removed), Is.True);
        var archetype = world.Archetypes[0];
        var chunk = archetype.GetChunk(0);
        int[] valueRow = (int[])chunk.GetRawComponentRow(0);
        var referenceRow = (ReferencePayload[])chunk.GetRawComponentRow(1);
        Assert.Multiple(() =>
        {
            Assert.That(chunk.Count, Is.EqualTo(1));
            Assert.That(valueRow[1], Is.EqualTo(22), "value-only tail is intentionally not cleared");
            Assert.That(referenceRow[1], Is.Null, "reference tail must be cleared for GC");
        });
    }

    [Test]
    public void SwapBackUpdatesMovedRecordAndStaleGenerationIsRejected()
    {
        var layouts = new ComponentRegistry();
        var id = layouts.Register<int>(new SchemaId(10_041));
        using var world = new World(layouts);
        var first = world.Create([id]);
        var second = world.Create([id]);
        world.GetRef<int>(second, id) = 42;

        Assert.That(world.Destroy(first), Is.True);
        Assert.That(world.TryGet(second, id, out int value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(world.Destroy(second), Is.True);
        Assert.That(world.IsAlive(second), Is.False);
        Assert.That(world.TryGet(second, id, out int _), Is.False);
    }

    [Test]
    public void InvalidEntityNeverReturnsAComponentReference()
    {
        var layouts = new ComponentRegistry();
        var id = layouts.Register<int>(new SchemaId(10_051));
        using var world = new World(layouts);
        var invalid = default(Entity);

        Assert.That(world.IsAlive(invalid), Is.False);
        Assert.That(world.TryGet(invalid, id, out int _), Is.False);
    }

    private readonly struct ManagedPayload(string text)
    {

        public string? Text { get; } = text;
    }

    private sealed class ReferencePayload(string name)
    {

        public string Name { get; } = name;
    }
}
