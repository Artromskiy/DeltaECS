namespace Delta.ECS.Tests;

using System;
using NUnit.Framework;

[TestFixture]
internal sealed class GeneratedDenseBindingTests
{
    [Test]
    public void BindingTracksLiveCountsAndChunkAdoption()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96001));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96002));
        using var world = new World(layouts);
        Query query = world.WhereAll<Health>();
        Assert.That(Visit(world, in query), Is.Zero);

        Entity first = world.Create(health);
        Assert.That(Visit(world, in query), Is.EqualTo(1));
        Entity second = world.Create(health);
        Assert.That(Visit(world, in query), Is.EqualTo(2));
        world.Destroy(second);
        Assert.That(Visit(world, in query), Is.EqualTo(1));

        var entities = new Entity[Chunk.Capacity * 2 + 3];
        world.Create(new[] { health }, entities.Length, entities);
        Entity target = world.Create(health, velocity);
        int count = entities.Length + 2;
        Assert.That(Visit(world, in query), Is.EqualTo(count));
        world.Add(in query, new[] { velocity });
        Assert.That(Visit(world, in query), Is.EqualTo(count));
        world.Remove(in query, new[] { velocity });
        Assert.That(Visit(world, in query), Is.EqualTo(count));
        Assert.That(world.TryGet<Health>(first, health, out var firstHealth), Is.True);
        Assert.That(firstHealth.Value, Is.EqualTo(6));
        Assert.That(world.TryGet<Health>(target, health, out var targetHealth), Is.True);
        Assert.That(targetHealth.Value, Is.EqualTo(3));

        world.Destroy(in query);
        Assert.That(Visit(world, in query), Is.Zero);
        world.Create(health);
        Assert.That(Visit(world, in query), Is.EqualTo(1));
    }

    [Test]
    public void SameSignatureAndQueryShapeRemainWorldLocal()
    {
        var firstLayouts = new ComponentLayoutRegistry();
        firstLayouts.Register<Velocity>(new SchemaId(96003));
        ComponentId firstId = firstLayouts.Register<Health>(new SchemaId(96004));
        var secondLayouts = new ComponentLayoutRegistry();
        ComponentId secondId = secondLayouts.Register<Health>(new SchemaId(96004));
        using var first = new World(firstLayouts);
        using var second = new World(secondLayouts);
        Query firstQuery = first.WhereAll<Health>();
        Query secondQuery = second.WhereAll<Health>();
        first.Create(firstId);
        second.Create(new[] { secondId }, 3);
        Assert.That(Visit(first, in firstQuery), Is.EqualTo(1));
        Assert.That(Visit(second, in secondQuery), Is.EqualTo(3));
        Assert.That(Visit(first, in firstQuery), Is.EqualTo(1));
        Assert.That(() => Visit(first, in secondQuery), Throws.ArgumentException);
    }

    [Test]
    public void WritesMarkEveryMatchingArchetypeBeforeCallbackThrowsAndReleaseLease()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96005));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96006));
        using var world = new World(layouts);
        Entity first = world.Create(health);
        Entity second = world.Create(health, velocity);
        Query query = world.WhereAll<Health>();
        world.TryGetComponentStamp(first, health, out Stamp beforeFirst);
        world.TryGetComponentStamp(second, health, out Stamp beforeSecond);
        Assert.That(() => Fail(world, in query), Throws.InvalidOperationException);
        world.TryGetComponentStamp(first, health, out Stamp afterFirst);
        world.TryGetComponentStamp(second, health, out Stamp afterSecond);
        Assert.That(afterFirst.Value, Is.EqualTo(beforeFirst.Value + 1));
        Assert.That(afterSecond.Value, Is.EqualTo(beforeSecond.Value + 1));
        Assert.That(() => world.Create(health), Throws.Nothing);
        Assert.That(Visit(world, in query), Is.EqualTo(3));
    }

    [Test]
    public void WriteTargetsRefreshWhenMatchingArchetypesActivateAndDeactivate()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96008));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96009));
        using var world = new World(layouts);
        Entity healthOnly = world.Create(health);
        Query query = world.WhereAll<Health>();
        Assert.That(Visit(world, in query), Is.EqualTo(1));

        Entity healthAndVelocity = world.Create(health, velocity);
        world.TryGetComponentStamp(healthAndVelocity, health, out Stamp beforeNewArchetype);
        Assert.That(Visit(world, in query), Is.EqualTo(2));
        world.TryGetComponentStamp(healthAndVelocity, health, out Stamp afterNewArchetype);
        Assert.That(afterNewArchetype.Value, Is.EqualTo(beforeNewArchetype.Value + 1));

        world.Destroy(healthOnly);
        world.TryGetComponentStamp(healthAndVelocity, health, out Stamp beforeDeactivationRefresh);
        Assert.That(Visit(world, in query), Is.EqualTo(1));
        world.TryGetComponentStamp(healthAndVelocity, health, out Stamp afterDeactivationRefresh);
        Assert.That(afterDeactivationRefresh.Value, Is.EqualTo(beforeDeactivationRefresh.Value + 1));

        Entity reactivatedHealthOnly = world.Create(health);
        world.TryGetComponentStamp(reactivatedHealthOnly, health, out Stamp beforeReactivationRefresh);
        Assert.That(Visit(world, in query), Is.EqualTo(2));
        world.TryGetComponentStamp(reactivatedHealthOnly, health, out Stamp afterReactivationRefresh);
        Assert.That(afterReactivationRefresh.Value, Is.EqualTo(beforeReactivationRefresh.Value + 1));
    }

    [Test]
    public void ReadOnlySignaturePreservesStampsAndDisposedWorldRejectsCachedBinding()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96007));
        using var world = new World(layouts);
        Entity entity = world.Create(health);
        Query query = world.WhereAll<Health>();
        Visit(world, in query);
        world.TryGetComponentStamp(entity, health, out Stamp before);
        var state = new DenseBindingReadState();
        for (int index = 0; index < 2; index++)
        {
            world.ForEach(in query, ref state, static (ref DenseBindingReadState sum, in Health value) => sum.Total += value.Value);
        }
        world.TryGetComponentStamp(entity, health, out Stamp after);
        Assert.That(state.Total, Is.EqualTo(2));
        Assert.That(after, Is.EqualTo(before));
        // Revisit the write signature after the read signature occupied the last-binding slot.
        Assert.That(Visit(world, in query), Is.EqualTo(1));
        world.Dispose();
        Assert.That(() => Visit(world, in query), Throws.ArgumentException);
    }

    private static int Visit(World world, in Query query)
    {
        int count = 0;
        world.ForEach(in query, ref count, static (ref int visited, ref Health health) =>
        {
            visited++;
            health.Value++;
        });
        return count;
    }

    private static void Fail(World world, in Query query)
        => world.ForEach(in query, static (ref Health health) => ThrowFromCallback(ref health));

    private static void ThrowFromCallback(ref Health _)
        => throw new InvalidOperationException();
}

internal struct DenseBindingReadState
{
    internal int Total;
}
