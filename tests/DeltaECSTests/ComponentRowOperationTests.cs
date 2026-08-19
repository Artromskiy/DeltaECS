using DVG.ECS;
using NUnit.Framework;

namespace DVG.ECS.Tests;

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
