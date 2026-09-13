namespace Delta.ECS.Tests;

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
public sealed class PublicApiShapeTests
{
    [Test]
    public void TypeErasedStructuralKernelOverloadsRemainAvailable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Create), typeof(ReadOnlySpan<ComponentId>)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Create), typeof(ReadOnlySpan<ComponentId>), typeof(Span<Entity>)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Destroy), typeof(Entity)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Destroy), typeof(ReadOnlySpan<Entity>)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Add), typeof(ReadOnlySpan<Entity>), typeof(ReadOnlySpan<ComponentId>)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.Remove), typeof(ReadOnlySpan<Entity>), typeof(ReadOnlySpan<ComponentId>)).IsGenericMethod, Is.False);
        });
    }

    [Test]
    public void Entity_Uses_Default_Handle_And_Does_Not_Expose_Null()
    {
        Assert.Multiple(() =>
        {
            Assert.That(default(Entity).IsValid, Is.False);
            Assert.That(
                typeof(Entity).GetField("Null", BindingFlags.Public | BindingFlags.Static),
                Is.Null,
                "The named null handle is replaced by default(Entity).");
        });
    }

    [Test]
    public void RemovedLowLevelQueryTraversalIsAbsent()
    {
        var assembly = typeof(World).Assembly;
        Assert.Multiple(() =>
        {
            foreach (var typeName in new[]
            {
                "Delta.ECS.QueryScope",
                "Delta.ECS.QueryArchetypes",
                "Delta.ECS.QueryArchetype",
                "Delta.ECS.QueryChunks",
                "Delta.ECS.QueryArchetypeChunks",
                "Delta.ECS.QueryChunk",
                "Delta.ECS.QuerySlots",
                "Delta.ECS.ReadRow",
                "Delta.ECS.WriteRow",
                "Delta.ECS.ObjectReadValues",
                "Delta.ECS.ObjectWriteValues",
                "Delta.ECS.StampRow",
                "Delta.ECS.QueryChunkAction"
            })
            {
                Assert.That(assembly.GetType(typeName), Is.Null, $"Removed type {typeName} must stay absent.");
            }

            Assert.That(
                typeof(World).GetMethod("BeginScope", BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(Query).GetMethod(nameof(Query.AccessRead), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(Query).GetMethod(nameof(Query.AccessWrite), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(World).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(static method => method.Name == nameof(World.ForEachParallel)
                        && method.GetParameters().Any(static parameter => parameter.ParameterType.FullName == "Delta.ECS.QueryChunkAction")),
                Is.False);
        });
    }

    [Test]
    public void RemovedRawLayoutSurfaceIsAbsent()
    {
        const BindingFlags allInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.Multiple(() =>
        {
            Assert.That(
                typeof(SchemaId).GetMethod("FromUInt64", BindingFlags.Public | BindingFlags.Static),
                Is.Null);
            Assert.That(
                typeof(ComponentLayout).GetConstructor(allInstance, null, new[] { typeof(SchemaId), typeof(int), typeof(int) }, null),
                Is.Null);
            foreach (string property in new[] { "Size", "Alignment", "Stride", "RuntimeTypeHandle" })
            {
                Assert.That(typeof(ComponentLayout).GetProperty(property, allInstance), Is.Null, property);
            }

            Assert.That(typeof(ComponentLayout).GetMethod("Align", BindingFlags.Public | BindingFlags.Static), Is.Null);
        });
    }

    private static MethodInfo PublicInstanceMethod(Type type, string name, params Type[] parameterTypes)
        => type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == name
                && method.GetParameters().Select(static parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
}
