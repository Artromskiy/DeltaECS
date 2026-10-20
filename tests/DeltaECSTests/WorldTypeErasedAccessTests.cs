namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class WorldTypeErasedAccessTests
{
    [Test]
    public void TryReadAndTryWriteDispatchByComponentId()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<AccessValue>(new SchemaId(30_201));
        ComponentId tagId = layouts.Register<AccessTag>(new SchemaId(30_202));
        ComponentId missingId = layouts.Register<MissingValue>(new SchemaId(30_203));
        using var world = new World(layouts);
        Entity entity = world.Create(valueId);

        Assert.That(world.TryRead(entity, valueId, out object? value), Is.True);
        Assert.That(value, Is.TypeOf<AccessValue>());
        Assert.That(((AccessValue)value!).Value, Is.Zero);
        Assert.That(world.TryWrite(entity, valueId, new AccessValue { Value = 42 }), Is.True);
        Assert.That(world.Get<AccessValue>(entity, valueId).Value, Is.EqualTo(42));

        Assert.That(world.TryRead(entity, missingId, out _), Is.False);
        Assert.That(world.TryWrite(entity, missingId, new MissingValue()), Is.True);
        Assert.That(world.Has(entity, missingId), Is.True);
        Assert.That(world.TryWrite(entity, valueId, new AccessTag()), Is.False);
        Assert.That(world.TryRead(entity, new ComponentId(100), out _), Is.False);

        Assert.That(world.Add(entity, tagId), Is.True);
        Assert.That(world.TryRead(entity, tagId, out object? tag), Is.True);
        Assert.That(tag, Is.TypeOf<AccessTag>());
        Assert.That(world.TryWrite(entity, tagId, new AccessTag()), Is.True);
        Assert.Throws<System.NullReferenceException>(() => world.TryWrite(entity, tagId, null));

        Assert.That(world.Destroy(entity), Is.True);
        Assert.That(world.TryRead(entity, valueId, out _), Is.False);
        Assert.That(world.TryWrite(entity, valueId, new AccessValue()), Is.False);
    }

    [Test]
    public void NullReferenceValueCanBeReadButNotWritten()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ReferenceValue>(new SchemaId(30_204));
        using var world = new World(layouts);
        Entity entity = world.Create(valueId);

        Assert.Throws<System.NullReferenceException>(() => world.TryWrite(entity, valueId, null));
        Assert.That(world.TryRead(entity, valueId, out object? value), Is.True);
        Assert.That(value, Is.Null);
    }

    private sealed class ReferenceValue;

    private struct AccessValue
    {
        public int Value;
    }

    private readonly struct AccessTag;

    private readonly struct MissingValue;
}
