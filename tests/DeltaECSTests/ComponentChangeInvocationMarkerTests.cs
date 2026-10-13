namespace Delta.ECS.Tests;

using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentChangeInvocationMarkerTests
{
    [Test]
    public void WritesMadeByTheChangeCallbackAreNotRecordedAsExternalChanges()
    {
        var layouts = new ComponentRegistry();
        ComponentId valueId = layouts.Register<ComponentChangeInvocationMarkerValue>(new SchemaId(72_011));
        using var world = new World(layouts);
        Entity entity = world.Create(valueId);
        var changes = new List<ChangeKind>();
        ComponentChangeOperation<ComponentChangeInvocationMarkerValue> operation = world.ForEachChange<ComponentChangeInvocationMarkerValue>(
            valueId,
            (Entity current, ChangeKind kind, ref ComponentChangeInvocationMarkerValue value) =>
            {
                changes.Add(kind);
                world.GetRef<ComponentChangeInvocationMarkerValue>(current, valueId).Value = value.Value + 1;
            });

        operation.Invoke();
        changes.Clear();
        operation.Invoke();

        Assert.That(changes, Is.Empty);

        world.GetRef<ComponentChangeInvocationMarkerValue>(entity, valueId).Value++;
        operation.Invoke();

        Assert.That(changes, Is.EqualTo(new[] { ChangeKind.Modified }));
    }

    [Test]
    public void NestedInvocationRestoresTheOuterInvocationMarker()
    {
        var layouts = new ComponentRegistry();
        ComponentId outerId = layouts.Register<ComponentChangeInvocationMarkerValue>(new SchemaId(72_012));
        ComponentId innerId = layouts.Register<ComponentChangeInvocationMarkerValue>(new SchemaId(72_013));
        using var world = new World(layouts);
        _ = world.Create(stackalloc ComponentId[2] { outerId, innerId });
        var changes = new List<ChangeKind>();
        ComponentChangeOperation<ComponentChangeInvocationMarkerValue>? inner = null;
        ComponentChangeOperation<ComponentChangeInvocationMarkerValue> outer = world.ForEachChange<ComponentChangeInvocationMarkerValue>(
            outerId,
            (Entity current, ChangeKind kind, ref ComponentChangeInvocationMarkerValue _) =>
            {
                changes.Add(kind);
                if (kind == ChangeKind.Added)
                {
                    inner!.Invoke();
                }

                world.GetRef<ComponentChangeInvocationMarkerValue>(current, outerId).Value++;
            });
        inner = world.ForEachChange<ComponentChangeInvocationMarkerValue>(
            innerId,
            (Entity current, ChangeKind _, ref ComponentChangeInvocationMarkerValue _) =>
                world.GetRef<ComponentChangeInvocationMarkerValue>(current, innerId).Value++);

        outer.Invoke();
        changes.Clear();
        outer.Invoke();

        Assert.That(changes, Is.Empty);
    }
}

internal struct ComponentChangeInvocationMarkerValue
{
    internal int Value;
}
