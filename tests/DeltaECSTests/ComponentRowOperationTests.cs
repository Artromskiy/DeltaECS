using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class ComponentRowOperationTests
{
    [Test]
    public void SwapBack_Copies_Value_ManagedStruct_And_Class_Rows()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<int>(SchemaId.FromUInt64(10_001));
        var managedStructId = layouts.Register<ManagedPayload>(SchemaId.FromUInt64(10_002));
        var classId = layouts.Register<ReferencePayload>(SchemaId.FromUInt64(10_003));
        var world = new World(layouts, chunkCapacity: 4);
        var archetype = world.GetArchetype(valueId, managedStructId, classId);

        var removed = world.Create(archetype);
        var survivor = world.Create(archetype);
        var reference = new ReferencePayload("survivor");
        world.SetComponent(survivor, valueId, 42);
        world.SetComponent(survivor, managedStructId, new ManagedPayload("managed"));
        world.SetComponent(survivor, classId, reference);

        Assert.That(world.Destroy(removed), Is.True);
        Assert.That(world.TryGetComponent(survivor, valueId, out int value), Is.True);
        Assert.That(world.TryGetComponent(survivor, managedStructId, out ManagedPayload managed), Is.True);
        Assert.That(world.TryGetComponent(survivor, classId, out ReferencePayload? actualReference), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(value, Is.EqualTo(42));
            Assert.That(managed.Text, Is.EqualTo("managed"));
            Assert.That(actualReference, Is.SameAs(reference));
        });
    }

    [Test]
    public void Reused_CreateSlot_Initializes_All_Rows_To_Default()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<int>(SchemaId.FromUInt64(10_011));
        var managedStructId = layouts.Register<ManagedPayload>(SchemaId.FromUInt64(10_012));
        var classId = layouts.Register<ReferencePayload>(SchemaId.FromUInt64(10_013));
        var world = new World(layouts, chunkCapacity: 1);
        var archetype = world.GetArchetype(valueId, managedStructId, classId);

        var old = world.Create(archetype);
        world.SetComponent(old, valueId, 99);
        world.SetComponent(old, managedStructId, new ManagedPayload("old"));
        world.SetComponent(old, classId, new ReferencePayload("old"));
        world.Destroy(old);

        var current = world.Create(archetype);
        world.TryGetComponent(current, valueId, out int value);
        world.TryGetComponent(current, managedStructId, out ManagedPayload managed);
        world.TryGetComponent(current, classId, out ReferencePayload? reference);
        Assert.Multiple(() =>
        {
            Assert.That(value, Is.Zero);
            Assert.That(managed.Text, Is.Null);
            Assert.That(reference, Is.Null);
        });
    }

    [Test]
    public void Reused_TransitionSlot_Initializes_Only_Added_Rows()
    {
        var layouts = new ComponentLayoutRegistry();
        var sharedId = layouts.Register<int>(SchemaId.FromUInt64(10_021));
        var addedValueId = layouts.Register<int>(SchemaId.FromUInt64(10_022));
        var addedReferenceId = layouts.Register<ReferencePayload>(SchemaId.FromUInt64(10_023));
        var world = new World(layouts, chunkCapacity: 1);

        var oldTarget = world.Create(world.GetArchetype(sharedId, addedValueId, addedReferenceId));
        world.SetComponent(oldTarget, addedValueId, 123);
        world.SetComponent(oldTarget, addedReferenceId, new ReferencePayload("old"));
        world.Destroy(oldTarget);

        var source = world.Create(world.GetArchetype(sharedId));
        world.SetComponent(source, sharedId, 77);
        world.AddComponents(new[] { addedValueId, addedReferenceId }, source);

        world.TryGetComponent(source, sharedId, out int shared);
        world.TryGetComponent(source, addedValueId, out int addedValue);
        world.TryGetComponent(source, addedReferenceId, out ReferencePayload? addedReference);
        Assert.Multiple(() =>
        {
            Assert.That(shared, Is.EqualTo(77));
            Assert.That(addedValue, Is.Zero);
            Assert.That(addedReference, Is.Null);
        });
    }

    [Test]
    public void SwapBack_Does_Not_Clear_Unmanaged_Tail_But_Clears_Reference_Tail()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register<int>(SchemaId.FromUInt64(10_031));
        var referenceId = layouts.Register<ReferencePayload>(SchemaId.FromUInt64(10_032));
        var world = new World(layouts, chunkCapacity: 4);
        var archetypeHandle = world.GetArchetype(valueId, referenceId);
        var removed = world.Create(archetypeHandle);
        var survivor = world.Create(archetypeHandle);
        var survivorReference = new ReferencePayload("survivor");
        world.SetComponent(removed, valueId, 11);
        world.SetComponent(survivor, valueId, 22);
        world.SetComponent(survivor, referenceId, survivorReference);

        Assert.That(world.Destroy(removed), Is.True);
        var archetype = world.Archetypes[archetypeHandle.ArchetypeId];
        var chunk = archetype.GetChunk(0);
        var valueRow = (int[])chunk.GetRawComponentRow(0);
        var referenceRow = (ReferencePayload[])chunk.GetRawComponentRow(1);
        Assert.Multiple(() =>
        {
            Assert.That(chunk.Count, Is.EqualTo(1));
            Assert.That(valueRow[1], Is.EqualTo(22), "value-only tail is intentionally not cleared");
            Assert.That(referenceRow[1], Is.Null, "reference tail must be cleared for GC");
        });
    }

    [Test]
    public void SwapBack_Updates_Moved_Record_And_Stale_Generation_Is_Rejected()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register<int>(SchemaId.FromUInt64(10_041));
        var world = new World(layouts, chunkCapacity: 2);
        var first = world.Create(new[] { id });
        var second = world.Create(new[] { id });
        world.SetComponent(second, id, 42);

        Assert.That(world.Destroy(first), Is.True);
        Assert.That(world.TryGetComponent(second, id, out int value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(world.Destroy(second), Is.True);
        Assert.That(world.IsAlive(second), Is.False);
        Assert.That(world.TryGetComponent(second, id, out int _), Is.False);
    }

    [Test]
    public void Invalid_Entity_Never_Returns_A_Component_Reference()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register<int>(SchemaId.FromUInt64(10_051));
        var world = new World(layouts);
        var invalid = new Entity(999_999, 0);

        Assert.That(world.IsAlive(invalid), Is.False);
        Assert.That(world.TryGetComponent(invalid, id, out int _), Is.False);
    }

    private readonly struct ManagedPayload
    {
        public ManagedPayload(string text)
        {
            Text = text;
        }

        public string? Text { get; }
    }

    private sealed class ReferencePayload
    {
        public ReferencePayload(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
