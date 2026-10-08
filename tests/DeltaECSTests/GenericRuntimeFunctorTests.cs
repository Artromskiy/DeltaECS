namespace Delta.ECS.Tests;

using System;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class GenericRuntimeFunctorTests
{
    [Test]
    public void RegistrationClosesTypesAndPreservesSourceRegistrationIdentity()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId health = layouts.Register<float>(new SchemaId(120_001));
        ComponentId mana = layouts.Register<float>(new SchemaId(120_002));
        ComponentId healthHistory = layouts.Register(typeof(History<>), health, new SchemaId(120_101));
        ComponentId manaHistory = layouts.Register(typeof(History<>), mana, new SchemaId(120_102));
        ComponentId pair = layouts.Register(typeof(Pair<,>), health, mana, new SchemaId(120_103));

        Assert.That(layouts.GetComponentType(healthHistory), Is.EqualTo(typeof(History<float>)));
        Assert.That(layouts.GetComponentType(pair), Is.EqualTo(typeof(Pair<float, float>)));
        Assert.That(healthHistory, Is.Not.EqualTo(manaHistory));
        Assert.That(layouts.Register(typeof(History<>), health, new SchemaId(120_101)), Is.EqualTo(healthHistory));
        Assert.That(layouts.Register(typeof(Pair<,>), health, mana, new SchemaId(120_103)), Is.EqualTo(pair));
        int countBeforeConflict = layouts.Count;
        Assert.Throws<InvalidOperationException>(() => layouts.Register(typeof(History<>), health, new SchemaId(120_104)));
        Assert.That(layouts.Count, Is.EqualTo(countBeforeConflict));

        Assert.That(typeof(ComponentLayoutRegistry).GetMethods()
            .Where(static method => method.Name == "Register" && !method.IsGenericMethod)
            .SelectMany(static method => method.GetParameters())
            .Any(static parameter => parameter.ParameterType.IsArray), Is.False);
        Assert.That(typeof(ComponentLayoutRegistry).GetMethods()
            .Where(static method => method.Name == "Register" && !method.IsGenericMethod)
            .SelectMany(static method => method.GetParameters())
            .Any(static parameter => parameter.ParameterType == typeof(ReadOnlySpan<ComponentId>)), Is.True);
    }

    [Test]
    public void RuntimeFunctorUsesDerivedRowsAndOneStateAcrossChunksWithTagFiltering()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<float>(new SchemaId(120_011));
        ComponentId value = layouts.Register<float>(new SchemaId(120_012));
        ComponentId history = layouts.Register(typeof(History<>), value, new SchemaId(120_111));
        ComponentId selected = layouts.Register<Selected>(new SchemaId(120_013));
        using var world = new World(layouts);
        var entities = new Entity[1_025];
        world.Create(stackalloc ComponentId[] { value, history }, entities.Length, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<float>(entities[index], value) = index + 0.5f;
            if (index % 2 == 0)
            {
                world.Add(entities[index], selected);
            }
        }

        Query query = world.WhereAll(value, history);
        Stamp before = GetStamp(world, entities[0], value);
        world.ForEach(in query, value, typeof(SaveHistory<>)).Invoke();
        Assert.That(GetStamp(world, entities[0], value), Is.Not.EqualTo(before));
        int[] ordinals = entities.Select(entity => world.Get<History<float>>(entity, history).Ordinal).Order().ToArray();
        Assert.That(ordinals, Is.EqualTo(Enumerable.Range(1, entities.Length)));

        Query filtered = query.WhereAll(selected);
        world.ForEach(in filtered, value, typeof(SaveHistory<>)).Invoke();
        for (int index = 0; index < entities.Length; index++)
        {
            History<float> saved = world.Get<History<float>>(entities[index], history);
            Assert.That(saved.Snapshot, Is.EqualTo(index + 0.5f));
            Assert.That(saved.Saves, Is.EqualTo(index % 2 == 0 ? 2 : 1));
        }
    }

    [Test]
    public void TwoGenericArgumentsCanBindThreeRowsAndReadOnlyModes()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId first = layouts.Register<int>(new SchemaId(120_021));
        ComponentId second = layouts.Register<float>(new SchemaId(120_022));
        ComponentId pair = layouts.Register(typeof(Pair<,>), first, second, new SchemaId(120_121));
        using var world = new World(layouts);
        Entity entity = world.Create(first, second, pair);
        world.GetRef<int>(entity, first) = 42;
        world.GetRef<float>(entity, second) = 1.5f;
        Query query = world.WhereAll(first, second, pair);
        Stamp beforeFirst = GetStamp(world, entity, first);
        Stamp beforeSecond = GetStamp(world, entity, second);

        world.ForEach(in query, first, second, typeof(CopyPair<,>)).Invoke();

        Pair<int, float> result = world.Get<Pair<int, float>>(entity, pair);
        Assert.That(result.First, Is.EqualTo(42));
        Assert.That(result.Second, Is.EqualTo(1.5f));
        Assert.That(GetStamp(world, entity, first), Is.EqualTo(beforeFirst));
        Assert.That(GetStamp(world, entity, second), Is.EqualTo(beforeSecond));
    }

    [Test]
    public void RuntimeFunctorWithoutExplicitQueryReusesItsLiveImplicitQueryPlan()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId value = layouts.Register<int>(new SchemaId(120_025));
        ComponentId history = layouts.Register(typeof(History<>), value, new SchemaId(120_125));
        ComponentId selected = layouts.Register<Selected>(new SchemaId(120_026));
        using var world = new World(layouts);
        Entity first = world.Create(value, history);
        Entity[] entities = { first };
        var operation = world.ForEach(entities.AsSpan(), value, typeof(SaveHistory<>));
        world.GetRef<int>(first, value) = 10;
        operation.Invoke();
        Assert.That(world.Get<History<int>>(first, history).Saves, Is.EqualTo(1));

        world.Add(first, selected);
        operation.Invoke();

        Assert.That(world.Get<History<int>>(first, history).Saves, Is.EqualTo(2));
    }

    [Test]
    public void InvalidDefinitionsConstraintsAndMissingRowsFailBeforeIteration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId value = layouts.Register<int>(new SchemaId(120_031));
        ComponentId text = layouts.Register<string>(new SchemaId(120_032));
        Assert.Throws<ArgumentException>(() => layouts.Register(typeof(int), value, new SchemaId(120_131)));
        Assert.Throws<ArgumentException>(() => layouts.Register(typeof(Pair<,>), value, new SchemaId(120_132)));
        Assert.Throws<ArgumentOutOfRangeException>(() => layouts.Register(typeof(History<>), ComponentId.Invalid, new SchemaId(120_133)));
        using var world = new World(layouts);
        Entity entity = world.Create(value);
        Query query = world.WhereAll(value);
        Stamp before = GetStamp(world, entity, value);
        Assert.Throws<ArgumentException>(() => world.ForEach(in query, text, typeof(SaveHistory<>)).Invoke());
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => world.ForEach(in query, value, typeof(SaveHistory<>)).Invoke());
        Assert.That(GetStamp(world, entity, value), Is.EqualTo(before));
        Assert.That(world.Destroy(entity), Is.True);
    }

    private static Stamp GetStamp(World world, Entity entity, ComponentId component)
    {
        Assert.That(world.TryGetComponentStamp(entity, component, out Stamp stamp), Is.True);
        return stamp;
    }

    public struct History<T>
    {
        public T Snapshot;
        public int Ordinal;
        public int Saves;
    }

    public struct Pair<TFirst, TSecond>
    {
        public TFirst First;
        public TSecond Second;
    }

    public struct Selected { }

    public struct SaveHistory<T> : IForEach where T : struct
    {
        private int _ordinal;

        public void Invoke(ref History<T> history, ref T component)
        {
            history.Snapshot = component;
            history.Ordinal = ++_ordinal;
            history.Saves++;
        }
    }

    public struct CopyPair<TFirst, TSecond> : IForEach where TFirst : struct where TSecond : struct
    {
        public void Invoke(ref Pair<TFirst, TSecond> pair, ref readonly TFirst first, TSecond second)
        {
            pair.First = first;
            pair.Second = second;
        }
    }
}
