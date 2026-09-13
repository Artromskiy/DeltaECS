namespace Delta.ECS.Tests;

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
public sealed class PublicApiShapeTests
{
    private static readonly Type[] InternalQueryTypes =
    [
        typeof(QueryScope),
        typeof(QueryArchetypes),
        typeof(QueryArchetype),
        typeof(QueryChunks),
        typeof(QueryArchetypeChunks),
        typeof(QueryChunk),
        typeof(QuerySlots),
        typeof(ReadRow),
        typeof(WriteRow),
        typeof(ObjectReadValues),
        typeof(ObjectWriteValues),
        typeof(StampRow),
        typeof(QueryChunkAction)
    ];

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
    public void LowLevelQueryTraversalIsInternal()
    {
        Assert.Multiple(() =>
        {
            foreach (var type in InternalQueryTypes)
            {
                Assert.That(type.IsPublic, Is.False, $"{type.Name} must stay outside the public API.");
            }

            Assert.That(
                typeof(World).GetMethod(nameof(World.BeginScope), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(Query).GetMethod(nameof(Query.AccessRead), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(Query).GetMethod(nameof(Query.AccessWrite), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(World).GetProperty(nameof(World.ArchetypeVersion), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(World).GetMethod(nameof(World.CollectAliveEntities), BindingFlags.Public | BindingFlags.Instance),
                Is.Null);
            Assert.That(
                typeof(World).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(static method => method.Name == nameof(World.ForEachParallel)
                        && method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(QueryChunkAction))),
                Is.False);
        });
    }

    private static MethodInfo PublicInstanceMethod(Type type, string name, params Type[] parameterTypes)
        => type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == name
                && method.GetParameters().Select(static parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
}
