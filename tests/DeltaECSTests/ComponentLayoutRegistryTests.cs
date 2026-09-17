using System;
using System.Collections.Generic;
using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class ComponentLayoutRegistryTests
{
    [Test]
    public void MissingTypeIsNotResolvedAndGetThrows()
    {
        var layouts = new ComponentLayoutRegistry();

        Assert.That(layouts.TryGetPrimary<MissingComponent>(out ComponentId missing), Is.False);
        Assert.That(TryGetPrimaryByType(layouts, typeof(MissingComponent), out ComponentId missingByType), Is.False);
        Assert.That(missing, Is.EqualTo(ComponentId.Invalid));
        Assert.That(missingByType, Is.EqualTo(ComponentId.Invalid));
        Assert.Throws<KeyNotFoundException>(() => GetPrimaryByType(layouts, typeof(MissingComponent)));
    }

    [Test]
    public void FirstRegistrationWinsAsThePrimaryId()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register<Position>(new SchemaId(70_001));
        var second = layouts.Register<Position>(new SchemaId(70_002));

        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(first));
        Assert.That(GetPrimaryByType(layouts, typeof(Position)), Is.EqualTo(first));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void RepeatedSchemaReturnsSameIdAndDoesNotReplacePrimary()
    {
        var layouts = new ComponentLayoutRegistry();
        var first = layouts.Register<Position>(new SchemaId(70_011));
        var duplicate = layouts.Register<Position>(new SchemaId(70_011));

        Assert.That(duplicate, Is.EqualTo(first));
        Assert.That(layouts.Count, Is.EqualTo(1));
        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(first));
    }

    [Test]
    public void ConflictingSchemaIsRejectedWithoutChangingPrimary()
    {
        var layouts = new ComponentLayoutRegistry();
        var position = layouts.Register<Position>(new SchemaId(70_021));

        Assert.Throws<InvalidOperationException>(() =>
            layouts.Register<Velocity>(new SchemaId(70_021)));

        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(position));
        Assert.That(layouts.TryGetPrimary<Velocity>(out _), Is.False);
        Assert.That(layouts.Count, Is.EqualTo(1));
    }

    [Test]
    public void GenericResolutionSupportsReferenceAndValueTypes()
    {
        var layouts = new ComponentLayoutRegistry();
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
        var layouts = new ComponentLayoutRegistry();
        var primary = layouts.Register<Position>(new SchemaId(70_041));

        int registrationCount = 256 + 64;
        for (var index = 1; index < registrationCount; index++)
        {
            RegisterByType(layouts, typeof(int), new SchemaId((ulong)(70_041 + index)));
        }

        Assert.That(layouts.Count, Is.EqualTo(registrationCount));
        Assert.That(layouts.GetPrimary<Position>(), Is.EqualTo(primary));
        Assert.That(RegisterByType(layouts, typeof(int), new SchemaId(71_000)).Value,
            Is.EqualTo(registrationCount));
    }

    [Test]
    public void PrimaryIdsAreIsolatedPerRegistryAndWorld()
    {
        var firstLayouts = new ComponentLayoutRegistry();
        var firstPosition = firstLayouts.Register<Position>(new SchemaId(70_051));
        var firstVelocity = firstLayouts.Register<Velocity>(new SchemaId(70_052));

        var secondLayouts = new ComponentLayoutRegistry();
        var secondVelocity = secondLayouts.Register<Velocity>(new SchemaId(70_062));
        var secondPosition = secondLayouts.Register<Position>(new SchemaId(70_061));
        using var firstWorld = new World(firstLayouts);
        using var secondWorld = new World(secondLayouts);

        Assert.That(firstWorld.Layouts.GetPrimary<Position>(), Is.EqualTo(firstPosition));
        Assert.That(firstWorld.Layouts.GetPrimary<Velocity>(), Is.EqualTo(firstVelocity));
        Assert.That(secondWorld.Layouts.GetPrimary<Position>(), Is.EqualTo(secondPosition));
        Assert.That(secondWorld.Layouts.GetPrimary<Velocity>(), Is.EqualTo(secondVelocity));
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

    private static bool TryGetPrimaryByType(
        ComponentLayoutRegistry layouts,
        Type runtimeType,
        out ComponentId componentId)
        => layouts.TryGetPrimary(runtimeType, out componentId);

    private static ComponentId GetPrimaryByType(ComponentLayoutRegistry layouts, Type runtimeType)
        => layouts.GetPrimary(runtimeType);

    private static ComponentId RegisterByType(
        ComponentLayoutRegistry layouts,
        Type runtimeType,
        SchemaId schemaId)
        => layouts.Register(runtimeType, schemaId);
}
