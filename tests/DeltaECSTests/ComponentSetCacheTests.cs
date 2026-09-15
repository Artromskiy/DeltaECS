using NUnit.Framework;
using Delta.ECS;

namespace Delta.ECS.Tests;

[TestFixture]
public sealed class ComponentSetCacheTests
{
    [Test]
    public void SpanComponentSetsAreReusedAfterValidation()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(90_001));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(90_002));
        using var world = new World(layouts);

        ComponentSet first = world.GetOrCreateComponentSet(new[] { positionId, velocityId });
        ComponentSet second = world.GetOrCreateComponentSet(new[] { positionId, velocityId });

        Assert.That(second, Is.SameAs(first));
        Assert.That(second.Mask, Is.EqualTo(first.Mask));
    }

    [Test]
    public void TypedComponentSetUsesTheStableRuntimeTypeHandleKey()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(90_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(90_012));
        using var world = new World(layouts);
        int factoryCalls = 0;

        ComponentSet first = world.GetOrCreateComponentSet(
            typeof(TypedSetKey).TypeHandle,
            _ =>
            {
                factoryCalls++;
                return new[] { positionId, velocityId };
            });
        ComponentSet second = world.GetOrCreateComponentSet(
            typeof(TypedSetKey).TypeHandle,
            _ =>
            {
                factoryCalls++;
                return new[] { positionId, velocityId };
            });

        Assert.That(second, Is.SameAs(first));
        Assert.That(factoryCalls, Is.EqualTo(1));
    }

    [Test]
    public void InvalidComponentIdsFailBeforeTheyEnterTheCache()
    {
        using var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.GetOrCreateComponentSet(new[] { ComponentId.Invalid }));
    }

    private sealed class TypedSetKey
    {
    }
}
