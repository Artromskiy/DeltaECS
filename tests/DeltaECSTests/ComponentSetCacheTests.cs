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
        Assert.That(first.Id.IsValid, Is.True);
        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(world.TryGetComponentSet(first.Id, out ComponentSet? cached), Is.True);
        Assert.That(cached, Is.SameAs(first));
    }

    [Test]
    public void EquivalentSelectorsWithDifferentOrderShareOneSetIdentity()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(90_005));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(90_006));
        using var world = new World(layouts);

        ComponentSet forward = world.GetOrCreateComponentSet(new[] { positionId, velocityId });
        ComponentSet reverse = world.GetOrCreateComponentSet(new[] { velocityId, positionId });

        Assert.Multiple(() =>
        {
            Assert.That(reverse, Is.Not.SameAs(forward));
            Assert.That(reverse.Id, Is.EqualTo(forward.Id));
            Assert.That(forward.ComponentIds[0], Is.EqualTo(positionId));
            Assert.That(reverse.ComponentIds[0], Is.EqualTo(velocityId));
        });
    }

    [Test]
    public void TypedComponentSetUsesTheStableRuntimeTypeHandleKey()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(90_011));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(90_012));
        using var world = new World(layouts);
        int factoryCalls = 0;

        ReadOnlySpan<ComponentId> first = GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<TypedSetKey>(
            world,
            _ =>
            {
                factoryCalls++;
                return new[] { positionId, velocityId };
            });
        ReadOnlySpan<ComponentId> second = GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<TypedSetKey>(
            world,
            _ =>
            {
                factoryCalls++;
                return new[] { positionId, velocityId };
            });

        Assert.That(second.Length, Is.EqualTo(2));
        Assert.That(second[0], Is.EqualTo(first[0]));
        Assert.That(second[1], Is.EqualTo(first[1]));
        Assert.That(factoryCalls, Is.EqualTo(1));

        ComponentSet positional = world.GetOrCreateComponentSet(first);
        Assert.That(world.TryGetComponentSet(positional.Id, out ComponentSet? cached), Is.True);
        Assert.That(cached, Is.SameAs(positional));
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
