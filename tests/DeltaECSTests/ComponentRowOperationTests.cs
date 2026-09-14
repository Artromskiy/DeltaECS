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
        var valueId = layouts.Register(typeof(int), new SchemaId(10_001));
        var managedStructId = layouts.Register(typeof(ManagedPayload), new SchemaId(10_002));
        var classId = layouts.Register(typeof(ReferencePayload), new SchemaId(10_003));
        var world = new World(layouts);
        var removed = world.Create(stackalloc[] { valueId, managedStructId, classId });
        var survivor = world.Create(stackalloc[] { valueId, managedStructId, classId });
        var reference = new ReferencePayload("survivor");
        world.Set(survivor, valueId, 42);
        world.Set(survivor, managedStructId, new ManagedPayload("managed"));
        world.Set(survivor, classId, reference);

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
    public void Reused_CreateSlot_Initializes_All_Rows_To_Default()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register(typeof(int), new SchemaId(10_011));
        var managedStructId = layouts.Register(typeof(ManagedPayload), new SchemaId(10_012));
        var classId = layouts.Register(typeof(ReferencePayload), new SchemaId(10_013));
        var world = new World(layouts);
        var old = world.Create(stackalloc[] { valueId, managedStructId, classId });
        world.Set(old, valueId, 99);
        world.Set(old, managedStructId, new ManagedPayload("old"));
        world.Set(old, classId, new ReferencePayload("old"));
        world.Destroy(old);

        var current = world.Create(stackalloc[] { valueId, managedStructId, classId });
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
    public void Reused_TransitionSlot_Initializes_Only_Added_Rows()
    {
        var layouts = new ComponentLayoutRegistry();
        var sharedId = layouts.Register(typeof(int), new SchemaId(10_021));
        var addedValueId = layouts.Register(typeof(int), new SchemaId(10_022));
        var addedReferenceId = layouts.Register(typeof(ReferencePayload), new SchemaId(10_023));
        var world = new World(layouts);

        var oldTarget = world.Create(stackalloc[] { sharedId, addedValueId, addedReferenceId });
        world.Set(oldTarget, addedValueId, 123);
        world.Set(oldTarget, addedReferenceId, new ReferencePayload("old"));
        world.Destroy(oldTarget);

        var source = world.Create(stackalloc[] { sharedId });
        world.Set(source, sharedId, 77);
        world.Add(source, new[] { addedValueId, addedReferenceId });

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
    public void SwapBack_Does_Not_Clear_Unmanaged_Tail_But_Clears_Reference_Tail()
    {
        var layouts = new ComponentLayoutRegistry();
        var valueId = layouts.Register(typeof(int), new SchemaId(10_031));
        var referenceId = layouts.Register(typeof(ReferencePayload), new SchemaId(10_032));
        var world = new World(layouts);
        var removed = world.Create(stackalloc[] { valueId, referenceId });
        var survivor = world.Create(stackalloc[] { valueId, referenceId });
        var survivorReference = new ReferencePayload("survivor");
        world.Set(removed, valueId, 11);
        world.Set(survivor, valueId, 22);
        world.Set(survivor, referenceId, survivorReference);

        Assert.That(world.Destroy(removed), Is.True);
        var archetype = world.Archetypes[0];
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
        var id = layouts.Register(typeof(int), new SchemaId(10_041));
        var world = new World(layouts);
        var first = world.Create(new[] { id });
        var second = world.Create(new[] { id });
        world.Set(second, id, 42);

        Assert.That(world.Destroy(first), Is.True);
        Assert.That(world.TryGet(second, id, out int value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(world.Destroy(second), Is.True);
        Assert.That(world.IsAlive(second), Is.False);
        Assert.That(world.TryGet(second, id, out int _), Is.False);
    }

    [Test]
    public void Invalid_Entity_Never_Returns_A_Component_Reference()
    {
        var layouts = new ComponentLayoutRegistry();
        var id = layouts.Register(typeof(int), new SchemaId(10_051));
        var world = new World(layouts);
        var invalid = default(Entity);

        Assert.That(world.IsAlive(invalid), Is.False);
        Assert.That(world.TryGet(invalid, id, out int _), Is.False);
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
