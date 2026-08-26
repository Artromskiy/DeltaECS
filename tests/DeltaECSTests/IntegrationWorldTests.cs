namespace Delta.ECS.Tests;

using System;
using Delta.ECS.Integration;
using NUnit.Framework;

[TestFixture]
public sealed class IntegrationWorldTests
{
    [Test]
    public void LifecycleValidatesStateAndUpdateIsANoOpSafePoint()
    {
        using var storage = new World();
        IEcsWorld world = storage;

        Assert.Throws<InvalidOperationException>(world.Update);
        world.Initialize();
        Stamp before = world.Stamp;

        world.Update();

        Assert.That(world.Stamp, Is.EqualTo(before));
        Assert.Throws<InvalidOperationException>(world.Initialize);

        world.Shutdown();
        Assert.Throws<InvalidOperationException>(world.Update);
        Assert.Throws<InvalidOperationException>(world.Shutdown);
    }

    [Test]
    public void CatalogRefreshesIndependentlyAndDescribesUnsupportedRawLayouts()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(50_001));
        using var storage = new World(layouts);
        IEcsWorld world = storage;

        ComponentCatalog first = world.Catalog;
        Stamp worldStamp = world.Stamp;
        ComponentId rawId = layouts.Register(new ComponentLayout(new SchemaId(50_002), size: 16, alignment: 8));
        ComponentCatalog second = world.Catalog;

        Assert.Multiple(() =>
        {
            Assert.That(first.Components.Length, Is.EqualTo(1));
            Assert.That(first.Components.Span[0].Id, Is.EqualTo(positionId));
            Assert.That(first.Components.Span[0].Schema, Is.EqualTo(new SchemaId(50_001)));
            Assert.That(first.Components.Span[0].ValueType, Is.EqualTo(typeof(Position)));
            Assert.That(first.Components.Span[0].Capabilities, Is.EqualTo(ComponentCapabilities.Read | ComponentCapabilities.Write));
            Assert.That(first.Stamp, Is.Not.EqualTo(second.Stamp));
            Assert.That(second.Components.Length, Is.EqualTo(2));
            Assert.That(second.Components.Span[0].Id.Value, Is.LessThan(second.Components.Span[1].Id.Value));
            Assert.That(second.Components.Span[1].Id, Is.EqualTo(rawId));
            Assert.That(second.Components.Span[1].Schema, Is.EqualTo(new SchemaId(50_002)));
            Assert.That(second.Components.Span[1].Capabilities, Is.EqualTo(ComponentCapabilities.None));
            Assert.That(world.Stamp, Is.EqualTo(worldStamp));
        });

        world.Initialize();
        _ = world.Create(ReadOnlySpan<ComponentId>.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(world.Catalog.Stamp, Is.EqualTo(second.Stamp));
            Assert.That(world.Stamp, Is.Not.EqualTo(worldStamp));
        });
        world.Shutdown();
    }

    [Test]
    public void ZeroComponentEntitySupportsAddFirstRemoveLastAndDestroy()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(50_011));
        using var storage = new World(layouts, chunkCapacity: 2);
        IEcsWorld world = storage;
        world.Initialize();

        Entity entity = world.Create(ReadOnlySpan<ComponentId>.Empty);
        ComponentId[] destination = [positionId];

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(storage.IsAlive(entity), Is.True);
            Assert.That(world.TryGetComponents(entity, destination, out int emptyCount), Is.True);
            Assert.That(emptyCount, Is.Zero);
            Assert.That(destination[0], Is.EqualTo(positionId));
        });

        Assert.That(world.Add(entity, stackalloc[] { positionId, positionId }), Is.True);
        Assert.That(world.Add(entity, stackalloc[] { positionId }), Is.False);
        Assert.That(world.TryGetComponents(entity, destination, out int addedCount), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(addedCount, Is.EqualTo(1));
            Assert.That(destination[0], Is.EqualTo(positionId));
        });

        Stamp beforeRemove = world.Stamp;
        Assert.That(world.Remove(entity, stackalloc[] { positionId }), Is.True);
        Assert.That(world.Remove(entity, stackalloc[] { positionId }), Is.False);
        Assert.That(world.Stamp, Is.Not.EqualTo(beforeRemove));
        Assert.That(world.TryGetComponents(entity, destination, out int removedCount), Is.True);
        Assert.That(removedCount, Is.Zero);

        Assert.That(world.Add(entity, stackalloc[] { positionId }), Is.True);
        Assert.That(world.Destroy(entity), Is.True);
        destination[0] = positionId;
        Assert.Multiple(() =>
        {
            Assert.That(world.Destroy(entity), Is.False);
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(world.IsAlive(new Entity(int.MaxValue, int.MaxValue)), Is.False);
            Assert.That(world.TryGetComponents(entity, destination, out int deadCount), Is.False);
            Assert.That(deadCount, Is.Zero);
            Assert.That(destination[0], Is.EqualTo(positionId));
        });

        world.Shutdown();
    }

    [Test]
    public void StructuralChangesAreAtomicAndReportNoOps()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(50_021));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(50_022));
        var unknownId = new ComponentId(200);
        using var storage = new World(layouts);
        IEcsWorld world = storage;
        world.Initialize();
        Assert.Throws<ArgumentException>(() => world.Create(new[] { unknownId }));
        Entity entity = world.Create(stackalloc[] { positionId });
        Stamp before = world.Stamp;

        Assert.Throws<ArgumentException>(() => world.Add(entity, new[] { velocityId, unknownId }));
        Assert.That(world.Stamp, Is.EqualTo(before));
        Assert.That(world.TryRead(entity, velocityId, out _, out EcsReadError missing), Is.False);
        Assert.That(missing.Code, Is.EqualTo(EcsReadErrorCode.ComponentMissing));

        Assert.That(world.Add(entity, ReadOnlySpan<ComponentId>.Empty), Is.False);
        Assert.Throws<ArgumentException>(() => world.Remove(entity, new[] { positionId, unknownId }));
        Assert.That(world.TryRead(entity, positionId, out _, out EcsReadError retained), Is.True);
        Assert.That(retained.Code, Is.EqualTo(EcsReadErrorCode.None));
        Assert.That(world.Stamp, Is.EqualTo(before));

        world.Shutdown();
    }

    [Test]
    public void ObjectReadWriteValidatesErrorsTypesAndExactStamps()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId positionId = layouts.Register(typeof(Position), new SchemaId(50_031));
        ComponentId velocityId = layouts.Register(typeof(Velocity), new SchemaId(50_032));
        using var storage = new World(layouts);
        IEcsWorld world = storage;
        world.Initialize();
        Entity entity = world.Create(stackalloc[] { positionId });

        Assert.That(world.TryRead(entity, positionId, out ComponentSnapshot initial, out EcsReadError readError), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(initial.Value, Is.EqualTo(default(Position)));
            Assert.That(readError.Code, Is.EqualTo(EcsReadErrorCode.None));
        });

        Assert.That(world.TryWrite(entity, positionId, new Position(42), initial.Stamp, out Stamp written, out EcsWriteError writeError), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(world.Stamp));
            Assert.That(writeError.Code, Is.EqualTo(EcsWriteErrorCode.None));
        });
        Assert.That(world.TryRead(entity, positionId, out ComponentSnapshot updated, out _), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(updated.Value, Is.EqualTo(new Position(42)));
            Assert.That(updated.Stamp, Is.EqualTo(written));
        });

        Stamp beforeFailures = world.Stamp;
        Assert.That(world.TryWrite(entity, positionId, new Position(43), initial.Stamp, out Stamp staleWritten, out EcsWriteError stale), Is.False);
        Assert.That(world.TryWrite(entity, positionId, new Velocity(1), written, out _, out EcsWriteError wrongType), Is.False);
        Assert.That(world.TryWrite(entity, positionId, null, written, out _, out EcsWriteError nullValue), Is.False);
        Assert.That(world.TryRead(entity, velocityId, out _, out EcsReadError missing), Is.False);
        Assert.That(world.TryRead(entity, new ComponentId(200), out _, out EcsReadError unknown), Is.False);
        Assert.That(world.TryWrite(entity, velocityId, new Velocity(1), default, out _, out EcsWriteError missingWrite), Is.False);
        Assert.That(world.TryWrite(entity, new ComponentId(200), new Position(1), default, out _, out EcsWriteError unknownWrite), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(staleWritten, Is.EqualTo(default(Stamp)));
            Assert.That(stale.Code, Is.EqualTo(EcsWriteErrorCode.StaleStamp));
            Assert.That(wrongType.Code, Is.EqualTo(EcsWriteErrorCode.InvalidValue));
            Assert.That(nullValue.Code, Is.EqualTo(EcsWriteErrorCode.InvalidValue));
            Assert.That(missing.Code, Is.EqualTo(EcsReadErrorCode.ComponentMissing));
            Assert.That(unknown.Code, Is.EqualTo(EcsReadErrorCode.ComponentUnknown));
            Assert.That(missingWrite.Code, Is.EqualTo(EcsWriteErrorCode.ComponentMissing));
            Assert.That(unknownWrite.Code, Is.EqualTo(EcsWriteErrorCode.ComponentUnknown));
            Assert.That(world.Stamp, Is.EqualTo(beforeFailures));
        });

        Assert.That(world.Destroy(entity), Is.True);
        Assert.That(world.TryRead(entity, positionId, out _, out EcsReadError deadRead), Is.False);
        Assert.That(world.TryWrite(entity, positionId, new Position(1), written, out _, out EcsWriteError deadWrite), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(deadRead.Code, Is.EqualTo(EcsReadErrorCode.EntityNotAlive));
            Assert.That(deadWrite.Code, Is.EqualTo(EcsWriteErrorCode.EntityNotAlive));
        });

        world.Shutdown();
    }

    [Test]
    public void MutableReferenceIdentityIsPreservedAndDirectMutationDoesNotAdvanceStamp()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId referenceId = layouts.Register(typeof(MutableReference), new SchemaId(50_041));
        using var storage = new World(layouts);
        IEcsWorld world = storage;
        world.Initialize();
        Entity entity = world.Create(stackalloc[] { referenceId });
        Assert.That(world.TryRead(entity, referenceId, out ComponentSnapshot empty, out _), Is.True);
        Assert.That(empty.Value, Is.Null);

        var value = new MutableReference { Value = 7 };
        Assert.That(world.TryWrite(entity, referenceId, value, empty.Stamp, out Stamp written, out _), Is.True);
        Assert.That(world.TryRead(entity, referenceId, out ComponentSnapshot observed, out _), Is.True);
        Assert.That(observed.Value, Is.SameAs(value));

        value.Value = 9;
        Assert.That(world.TryRead(entity, referenceId, out ComponentSnapshot mutated, out _), Is.True);
        var mutatedValue = mutated.Value as MutableReference;
        Assert.Multiple(() =>
        {
            Assert.That(mutated.Value, Is.SameAs(value));
            Assert.That(mutatedValue?.Value, Is.EqualTo(9));
            Assert.That(mutated.Stamp, Is.EqualTo(written));
            Assert.That(world.Stamp, Is.EqualTo(written));
        });

        Assert.That(world.TryWrite(entity, referenceId, value, mutated.Stamp, out Stamp rewritten, out _), Is.True);
        Assert.That(rewritten, Is.Not.EqualTo(written));
        world.Shutdown();
    }

    private readonly record struct Position(int Value);

    private readonly record struct Velocity(int Value);

    private sealed class MutableReference
    {
        public int Value { get; set; }
    }
}
