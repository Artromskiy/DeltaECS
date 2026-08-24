namespace Delta.ECS.Tests;

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
public sealed class PublicApiShapeTests
{
    private static readonly Type[] QueryChainTypes =
    [
        typeof(Query),
        typeof(QueryScope),
        typeof(QueryArchetypes),
        typeof(QueryArchetype),
        typeof(QueryChunks),
        typeof(QueryChunk),
        typeof(QuerySlots),
        typeof(ReadAccess),
        typeof(WriteAccess),
        typeof(QueryChunkCursor),
        typeof(ReadRow),
        typeof(WriteRow),
        typeof(ObjectReadValues),
        typeof(ObjectWriteValues)
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
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.AddComponents), typeof(ComponentId[]), typeof(ReadOnlySpan<Entity>)).IsGenericMethod, Is.False);
            Assert.That(PublicInstanceMethod(typeof(World), nameof(World.RemoveComponents), typeof(ComponentId[]), typeof(ReadOnlySpan<Entity>)).IsGenericMethod, Is.False);
        });
    }

    [Test]
    public void QueryAccessAndRowChainIsTypeErasedUntilTerminalRef()
    {
        var genericTypes = QueryChainTypes
            .Where(static type => type.IsGenericType)
            .ToArray();
        var genericMethods = QueryChainTypes
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(static method => method.IsGenericMethod)
            .ToArray();
        var nonTerminalGenericMethods = genericMethods
            .Where(static method => method.Name != nameof(ReadRow.Ref))
            .ToArray();
        var invalidTerminalMethods = genericMethods
            .Where(static method => method.Name == nameof(ReadRow.Ref))
            .Where(static method => method.GetGenericArguments().Length != 1)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(genericTypes, Is.Empty, "Query/access/row types must not be generic types.");
            Assert.That(
                nonTerminalGenericMethods,
                Is.Empty,
                "Only terminal ReadRow.Ref<T>/WriteRow.Ref<T> may be generic: "
                    + string.Join(", ", nonTerminalGenericMethods.Select(static method => method.ToString())));
            Assert.That(invalidTerminalMethods, Is.Empty);
            Assert.That(genericMethods, Is.Not.Empty, "The typed row boundary must remain present.");
        });
    }

    [Test]
    public void ExplicitQueryPathPreservesThreeLoopPublicShape()
    {
        var createQuery = PublicInstanceMethod(typeof(World), nameof(World.CreateQuery), typeof(QuerySpec).MakeByRefType());
        var openQuery = PublicInstanceMethod(typeof(World), nameof(World.OpenQuery), typeof(Query).MakeByRefType());
        var bindRead = PublicInstanceMethod(typeof(QueryScope), nameof(QueryScope.Bind), typeof(ReadAccess));
        var bindWrite = PublicInstanceMethod(typeof(QueryScope), nameof(QueryScope.Bind), typeof(WriteAccess));
        var archetypeMoveNext = PublicInstanceMethod(typeof(QueryArchetypes), nameof(QueryArchetypes.MoveNext));
        var chunkMoveNext = PublicInstanceMethod(typeof(QueryChunks), nameof(QueryChunks.MoveNext));
        var slotMoveNext = PublicInstanceMethod(typeof(QuerySlots), nameof(QuerySlots.MoveNext));
        var getRead = PublicInstanceMethod(typeof(QuerySlots), nameof(QuerySlots.GetRow), typeof(ReadAccess));
        var getWrite = PublicInstanceMethod(typeof(QuerySlots), nameof(QuerySlots.GetRow), typeof(WriteAccess));

        Assert.Multiple(() =>
        {
            Assert.That(createQuery.ReturnType, Is.EqualTo(typeof(Query)));
            Assert.That(openQuery.ReturnType, Is.EqualTo(typeof(QueryScope)));
            Assert.That(typeof(QueryScope).GetProperty(nameof(QueryScope.Archetypes))?.PropertyType, Is.EqualTo(typeof(QueryArchetypes)));
            Assert.That(bindRead.ReturnType, Is.EqualTo(typeof(ReadAccess)));
            Assert.That(bindWrite.ReturnType, Is.EqualTo(typeof(WriteAccess)));
            Assert.That(archetypeMoveNext.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(typeof(QueryArchetypes).GetProperty(nameof(QueryArchetypes.Current))?.PropertyType, Is.EqualTo(typeof(QueryArchetype)));
            Assert.That(chunkMoveNext.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(typeof(QueryArchetype).GetProperty(nameof(QueryArchetype.Chunks))?.PropertyType, Is.EqualTo(typeof(QueryChunks)));
            Assert.That(typeof(QueryChunks).GetProperty(nameof(QueryChunks.Current))?.PropertyType, Is.EqualTo(typeof(QueryChunk)));
            Assert.That(slotMoveNext.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(typeof(QueryChunk).GetProperty(nameof(QueryChunk.Slots))?.PropertyType, Is.EqualTo(typeof(QuerySlots)));
            Assert.That(getRead.ReturnType, Is.EqualTo(typeof(ReadRow)));
            Assert.That(getWrite.ReturnType, Is.EqualTo(typeof(WriteRow)));
        });
    }

    private static MethodInfo PublicInstanceMethod(Type type, string name, params Type[] parameterTypes)
        => type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == name
                && method.GetParameters().Select(static parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
}
