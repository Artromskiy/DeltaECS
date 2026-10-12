using System;
using System.Collections.Generic;
using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ComponentRegistryTests
{
    [Test]
    public void MissingTypeIsNotResolvedAndGetThrows()
    {
        var layouts = new ComponentRegistry();

        Assert.That(layouts.TryGetPrimary<MissingComponent>(out ComponentId missing), Is.False);
        Assert.That(TryGetPrimaryByType(layouts, typeof(MissingComponent), out ComponentId missingByType), Is.False);
        Assert.That(missing, Is.EqualTo(ComponentId.Invalid));
        Assert.That(missingByType, Is.EqualTo(ComponentId.Invalid));
        Assert.Throws<KeyNotFoundException>(() => GetPrimaryByType(layouts, typeof(MissingComponent)));
    }

    [Test]
    public void FirstRegistrationWinsAsThePrimaryId()
    {
        var layouts = new ComponentRegistry();
        var first = layouts.Register<Position>(new SchemaId(70_001));
        var second = layouts.Register<Position>(new SchemaId(70_002));

        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(first));
        Assert.That(GetPrimaryByType(layouts, typeof(Position)), Is.EqualTo(first));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void RepeatedSchemaReturnsSameIdAndDoesNotReplacePrimary()
    {
        var layouts = new ComponentRegistry();
        var first = layouts.Register<Position>(new SchemaId(70_011));
        var duplicate = layouts.Register<Position>(new SchemaId(70_011));

        Assert.That(duplicate, Is.EqualTo(first));
        Assert.That(layouts.Count, Is.EqualTo(1));
        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(first));
    }

    [Test]
    public void SchemaIdResolvesRegisteredComponentId()
    {
        var layouts = new ComponentRegistry();
        var schemaId = new SchemaId(70_016);
        ComponentId expected = layouts.Register<Position>(schemaId);

        Assert.That(layouts.TryGetComponentId(schemaId, out ComponentId actual), Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CatalogAliasesAndGroupsCanBeRegisteredThroughLayoutsAndReadThroughWorld()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(70_018));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(70_019));
        using var world = new World(layouts);
        using var anotherWorld = new World(layouts);

        layouts.AddAlias("position", positionId);
        layouts.AddAlias("position", positionId);
        layouts.AddGroup(ComponentGroups.History, positionId);
        layouts.AddGroup(ComponentGroups.History, positionId);
        layouts.AddGroup(ComponentGroups.History, velocityId);

        Assert.That(world.GetAlias("position"), Is.EqualTo(positionId));
        Assert.That(layouts.GetAlias("position"), Is.EqualTo(positionId));
        Assert.That(anotherWorld.GetAlias("position"), Is.EqualTo(positionId));
        Assert.That(world.TryGetAlias("missing", out ComponentId missing), Is.False);
        Assert.That(missing, Is.EqualTo(ComponentId.Invalid));
        Assert.That(world.GetGroup(ComponentGroups.History).ToArray(), Is.EqualTo(new[] { positionId, velocityId }));
        Assert.That(anotherWorld.GetGroup(ComponentGroups.History).ToArray(), Is.EqualTo(new[] { positionId, velocityId }));
        Assert.Throws<InvalidOperationException>(() => layouts.AddAlias("position", velocityId));
        Assert.Throws<ArgumentException>(() => layouts.AddAlias("  ", positionId));
    }

    [Test]
    public void UnknownSchemaIdDoesNotResolve()
    {
        var layouts = new ComponentRegistry();

        Assert.That(layouts.TryGetComponentId(new SchemaId(70_017), out ComponentId missing), Is.False);
        Assert.That(missing, Is.EqualTo(ComponentId.Invalid));
    }

    [Test]
    public void ConflictingSchemaIsRejectedWithoutChangingPrimary()
    {
        var layouts = new ComponentRegistry();
        var position = layouts.Register<Position>(new SchemaId(70_021));

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = layouts.Register<Velocity>(new SchemaId(70_021));
        });

        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(position));
        Assert.That(layouts.TryGetPrimary<Velocity>(out _), Is.False);
        Assert.That(layouts.Count, Is.EqualTo(1));
    }

    [Test]
    public void GenericResolutionSupportsReferenceAndValueTypes()
    {
        var layouts = new ComponentRegistry();
        var valueId = layouts.Register<ValueComponent>(new SchemaId(70_031));
        var referenceId = layouts.Register<ReferenceComponent>(new SchemaId(70_032));
        var referenceValue = new ReferenceComponent();

        Assert.That(layouts.TryGetPrimary<ValueComponent>(out ComponentId resolvedValue), Is.True);
        Assert.That(layouts.TryGetPrimary<ReferenceComponent>(out ComponentId resolvedReference), Is.True);
        Assert.That(resolvedValue, Is.EqualTo(valueId));
        Assert.That(resolvedReference, Is.EqualTo(referenceId));
        Assert.That(referenceValue, Is.Not.Null);
    }

    [Test]
    public void RegistrationContinuesBeyondTheOriginalFourWordBoundary()
    {
        var layouts = new ComponentRegistry();
        var primary = layouts.Register<Position>(new SchemaId(70_041));

        int registrationCount = 256 + 64;
        for (var index = 1; index < registrationCount; index++)
        {
            layouts.Register<int>(new SchemaId((ulong)(70_041 + index)));
        }

        Assert.That(layouts.Count, Is.EqualTo(registrationCount));
        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(primary));
        Assert.That(layouts.Register<int>(new SchemaId(71_000)).Value,
            Is.EqualTo(registrationCount));
    }

    [Test]
    public void PrimaryIdsAreIsolatedPerRegistryAndWorld()
    {
        var firstLayouts = new ComponentRegistry();
        var firstPosition = firstLayouts.Register<Position>(new SchemaId(70_051));
        var firstVelocity = firstLayouts.Register<Velocity>(new SchemaId(70_052));

        var secondLayouts = new ComponentRegistry();
        var secondVelocity = secondLayouts.Register<Velocity>(new SchemaId(70_062));
        var secondPosition = secondLayouts.Register<Position>(new SchemaId(70_061));
        using var firstWorld = new World(firstLayouts);
        using var secondWorld = new World(secondLayouts);

        Assert.That(firstWorld.Components.GetPrimary<Position>(), Is.EqualTo(firstPosition));
        Assert.That(firstWorld.Components.GetPrimary<Velocity>(), Is.EqualTo(firstVelocity));
        Assert.That(secondWorld.Components.GetPrimary<Position>(), Is.EqualTo(secondPosition));
        Assert.That(secondWorld.Components.GetPrimary<Velocity>(), Is.EqualTo(secondVelocity));
        Assert.That(firstPosition, Is.Not.EqualTo(secondPosition));
        Assert.That(firstVelocity, Is.Not.EqualTo(secondVelocity));
    }

    private readonly struct MissingComponent
    {
    }

    private readonly struct Position
    {
        public int Value { get; init; }
    }

    private readonly struct Velocity
    {
        public float Value { get; init; }
    }

    private readonly struct ValueComponent
    {
        public int Value { get; init; }
    }

    private sealed class ReferenceComponent
    {
        public int Value { get; init; }
    }

    private enum ComponentGroups
    {
        History,
    }

    private static bool TryGetPrimaryByType(
        ComponentRegistry layouts,
        Type runtimeType,
        out ComponentId componentId)
        => layouts.TryGetPrimary(runtimeType, out componentId);

    private static ComponentId GetPrimaryByType(ComponentRegistry layouts, Type runtimeType)
        => layouts.GetPrimary(runtimeType);

}
