namespace Delta.ECS.Tests;

using System;

using NUnit.Framework;

[TestFixture]
internal sealed class GeneratedDenseBindingTests
{
    [Test]
    public void BindingTracksLiveCountsAndChunkAdoption()
    {
        var layouts = new ComponentRegistry();
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
        world.Create([health], entities.Length, entities);
        Entity target = world.Create([health, velocity]);
        int count = entities.Length + 2;
        Assert.That(Visit(world, in query), Is.EqualTo(count));
        world.Add(in query, [velocity]);
        Assert.That(Visit(world, in query), Is.EqualTo(count));
        world.Remove(in query, [velocity]);
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
        var firstLayouts = new ComponentRegistry();
        firstLayouts.Register<Velocity>(new SchemaId(96003));
        ComponentId firstId = firstLayouts.Register<Health>(new SchemaId(96004));
        var secondLayouts = new ComponentRegistry();
        ComponentId secondId = secondLayouts.Register<Health>(new SchemaId(96004));
        using var first = new World(firstLayouts);
        using var second = new World(secondLayouts);
        Query firstQuery = first.WhereAll<Health>();
        Query secondQuery = second.WhereAll<Health>();
        first.Create(firstId);
        second.Create([secondId], 3);
        Assert.That(Visit(first, in firstQuery), Is.EqualTo(1));
        Assert.That(Visit(second, in secondQuery), Is.EqualTo(3));
        Assert.That(Visit(first, in firstQuery), Is.EqualTo(1));
        Assert.That(() => Visit(first, in secondQuery), Throws.ArgumentException);
    }

    [Test]
    public void ExplicitIdOperationsKeepTheirPreparedAccessSeparate()
    {
        var layouts = new ComponentRegistry();
        ComponentId firstId = layouts.Register<Health>(new SchemaId(96013));
        ComponentId secondId = layouts.Register<Health>(new SchemaId(96014));
        using var world = new World(layouts);
        Entity entity = world.Create([firstId, secondId]);
        world.GetRef<Health>(entity, firstId).Value = 10;
        world.GetRef<Health>(entity, secondId).Value = 20;
        Query query = world.WhereAll(firstId, secondId);
        var firstFunctor = new IncrementHealthFunctor();
        var secondFunctor = new IncrementHealthFunctor();
        var firstOperation = world.ForEach(in query, firstId, ref firstFunctor);
        var secondOperation = world.ForEach(in query, secondId, ref secondFunctor);

        firstOperation.Invoke(ref firstFunctor);
        secondOperation.Invoke(ref secondFunctor);
        firstOperation.Invoke(ref firstFunctor);

        Assert.That(world.Get<Health>(entity, firstId).Value, Is.EqualTo(12));
        Assert.That(world.Get<Health>(entity, secondId).Value, Is.EqualTo(21));
    }

    [Test]
    public void WritesMarkEveryMatchingArchetypeBeforeCallbackThrowsAndReleaseLease()
    {
        var layouts = new ComponentRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96005));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96006));
        using var world = new World(layouts);
        world.Create(health);
        world.Create([health, velocity]);
        Query query = world.WhereAll<Health>();
        Assert.That(() => Fail(world, in query), Throws.InvalidOperationException);
        Assert.That(() => world.Create(health), Throws.Nothing);
        Assert.That(Visit(world, in query), Is.EqualTo(3));
    }

    [Test]
    public void WriteTargetsRefreshWhenMatchingArchetypesActivateAndDeactivate()
    {
        var layouts = new ComponentRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96008));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96009));
        using var world = new World(layouts);
        Entity healthOnly = world.Create(health);
        Query query = world.WhereAll<Health>();
        int visited = 0;
        var operation = world.ForEach(in query, ref visited, static (ref int count, ref Health value) =>
        {
            count++;
            value.Value++;
        });
        operation.Invoke(ref visited);
        Assert.That(visited, Is.EqualTo(1));

        world.Create([health, velocity]);
        visited = 0;
        operation.Invoke(ref visited);
        Assert.That(visited, Is.EqualTo(2));

        world.Destroy(healthOnly);
        visited = 0;
        operation.Invoke(ref visited);
        Assert.That(visited, Is.EqualTo(1));

        world.Create(health);
        visited = 0;
        operation.Invoke(ref visited);
        Assert.That(visited, Is.EqualTo(2));
    }

    [Test]
    public void ChunkPlanRowArrayIsReusedWhenItsChunkReactivates()
    {
        var layouts = new ComponentRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96010));
        using var world = new World(layouts);
        Query query = world.WhereAll<Health>();
        Entity first = world.Create(health);
        world.GetRef<Health>(first, health).Value = 41;
        ChunkPlan originalPlan = query.Cached.MatchingChunkPlans()[0];

        Assert.That(world.Destroy(first), Is.True);
        Entity replacement = world.Create(health);
        world.GetRef<Health>(replacement, health).Value = 43;
        ChunkPlan reactivatedPlan = query.Cached.MatchingChunkPlans()[0];

        Assert.That(reactivatedPlan.Chunk, Is.SameAs(originalPlan.Chunk));
        Assert.That(reactivatedPlan.ComponentRows, Is.SameAs(originalPlan.ComponentRows));
        Assert.That(world.GetRef<Health>(replacement, health).Value, Is.EqualTo(43));
    }

    [Test]
    public void ChunkPlanRowsRemainValidWhenChunkLayoutIsAdoptedAndRestored()
    {
        var layouts = new ComponentRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96011));
        ComponentId velocity = layouts.Register<Velocity>(new SchemaId(96012));
        using var world = new World(layouts);
        var entities = new Entity[Chunk.Capacity];
        world.Create([health], entities.Length, entities);
        world.GetRef<Health>(entities[0], health).Value = 47;
        Query healthQuery = world.WhereAll<Health>();
        _ = healthQuery.Cached.MatchingChunkPlans()[0];

        Assert.That(world.Add(in healthQuery, [velocity]), Is.EqualTo(entities.Length));
        QuerySpec bothSpec = QuerySpec.WhereAll([health, velocity]);
        Query both = world.CreateQuery(in bothSpec);
        Assert.That(world.Remove(in both, velocity), Is.EqualTo(entities.Length));

        ReadOnlySpan<ChunkPlan> restoredPlans = healthQuery.Cached.MatchingChunkPlans();
        Assert.That(restoredPlans.Length, Is.EqualTo(1));
        Assert.That(world.GetRef<Health>(entities[0], health).Value, Is.EqualTo(47));
        Assert.That(world.Has(entities[0], velocity), Is.False);
        int total = 0;
        ForEachContextAction<int, Health> action = static (ref int sum, ref Health value) => sum += value.Value;
        world.ForEach(in healthQuery, ref total, action).Invoke(ref total);
        Assert.That(total, Is.EqualTo(47));
    }

    [Test]
    public void ReadOnlySignatureAndDisposedWorldRejectCachedBinding()
    {
        var layouts = new ComponentRegistry();
        ComponentId health = layouts.Register<Health>(new SchemaId(96007));
        using var world = new World(layouts);
        world.Create(health);
        Query query = world.WhereAll<Health>();
        Visit(world, in query);
        var state = new DenseBindingReadState();
        var readOperation = world.ForEach(in query, ref state, static (ref DenseBindingReadState sum, in Health value) => sum.Total += value.Value);
        for (int index = 0; index < 2; index++)
        {
            readOperation.Invoke(ref state);
        }
        Assert.That(state.Total, Is.EqualTo(2));
        // Revisit the write signature after the read signature occupied the last-binding slot.
        int visited = 0;
        var writeOperation = world.ForEach(in query, ref visited, static (ref int count, ref Health _) => count++);
        writeOperation.Invoke(ref visited);
        Assert.That(visited, Is.EqualTo(1));
        world.Dispose();
        Assert.That(() => writeOperation.Invoke(ref visited), Throws.ArgumentException);
        Assert.That(() => readOperation.Invoke(ref state), Throws.ArgumentException);
    }

    private static int Visit(World world, in Query query)
    {
        int count = 0;
        world.ForEach(in query, ref count, static (ref int visited, ref Health health) =>
        {
            visited++;
            health.Value++;
        }).Invoke(ref count);
        return count;
    }

    private static void Fail(World world, in Query query)
        => world.ForEach(in query, static (ref Health health) => ThrowFromCallback(ref health)).Invoke();

    private static void ThrowFromCallback(ref Health _)
        => throw new InvalidOperationException();
}

internal struct DenseBindingReadState
{
    internal int Total;
}

internal struct IncrementHealthFunctor : IForEach
{
    public void Invoke(ref Health health) => health.Value++;
}
