using System;
using System.Collections.Generic;
using Delta.ECS;
using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class OrderedQueryTests
{
    private static readonly int[] InitialStableOrder = [4, 1, 2, 3, 0];
    private static readonly int[] UpdatedStableOrder = [0, 4, 1, 2, 3];
    private static readonly int[] OrderedKeyValues = [1, 1, 1, 2, 2];
    private static readonly int[] SourceEntityOrder = [0, 1, 2, 3, 4];
    private static readonly int[] AscendingEntityOrder = [0, 1, 2];
    private static readonly int[] DescendingEntityOrder = [2, 1, 0];
    private static readonly int[] StableFourEntityOrder = [1, 2, 0, 3];

    [Test]
    public void OrderedForEachUsesStableLexicographicOrderAndReevaluatesKeys()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId keyId = layouts.Register<OrderKey>(new SchemaId(71_001));
        ComponentId tieId = layouts.Register<TieKey>(new SchemaId(71_002));
        ComponentId tagId = layouts.Register<OrderTag>(new SchemaId(71_003));
        using var world = new World(layouts);
        var entities = new Entity[5];
        world.Create(stackalloc ComponentId[] { keyId, tieId, tagId }, entities.Length, entities);

        Set(0, 2, 1);
        Set(1, 1, 2);
        Set(2, 1, 2);
        Set(3, 2, 0);
        Set(4, 1, 1);

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { keyId, tieId, tagId }));
        var keyAndTieComparer = default(OrderTieComparer);
        var tagComparer = default(OrderTagComparer);
        OrderedQuery ordered = query
            .OrderBy(keyId, tieId, ref keyAndTieComparer)
            .ThenBy(tagId, ref tagComparer);

        Assert.That(Collect(ordered), Is.EqualTo(InitialStableOrder));
        Assert.That(ordered.First(), Is.EqualTo(entities[4]));
        Entity expectedFiltered = Array.Find(entities, static entity => entity.Index == 2);
        Assert.That(ordered.First(entity => entity.Index == 2 || entity.Index == 3), Is.EqualTo(expectedFiltered));

        var orderedValues = new OrderedValueCollector { Values = new int[entities.Length] };
        ordered.ForEach(ref orderedValues);
        Assert.That(orderedValues.Values, Is.EqualTo(OrderedKeyValues));

        world.GetRef<OrderKey>(entities[0], keyId).Value = 0;
        Assert.That(Collect(ordered), Is.EqualTo(UpdatedStableOrder));

        var sourceOrder = new List<int>();
        world.ForEachEntity(in query, ref sourceOrder,
            static (ref List<int> indices, EntityRef entity) => indices.Add(entity.Index));
        Assert.That(sourceOrder, Is.EqualTo(SourceEntityOrder));

        void Set(int index, int key, int tie)
        {
            world.GetRef<OrderKey>(entities[index], keyId).Value = key;
            world.GetRef<TieKey>(entities[index], tieId).Value = tie;
        }
    }

    [Test]
    public void OrderBySupportsPrimaryAndExplicitRegistrationsAndTagKeys()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId primaryId = layouts.Register<OrderKey>(new SchemaId(71_011));
        ComponentId secondaryId = layouts.Register<OrderKey>(new SchemaId(71_012));
        ComponentId tagId = layouts.Register<OrderTag>(new SchemaId(71_013));
        using var world = new World(layouts);
        var entities = new Entity[3];
        world.Create(stackalloc ComponentId[] { primaryId, secondaryId, tagId }, entities.Length, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.GetRef<OrderKey>(entities[index], primaryId).Value = entities.Length - index;
            world.GetRef<OrderKey>(entities[index], secondaryId).Value = index;
        }

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { primaryId, secondaryId, tagId }));
        var orderKeyComparer = default(OrderKeyComparer);
        var tagComparer = default(OrderTagComparer);
        Assert.That(Collect(query.OrderBy(ref orderKeyComparer)), Is.EqualTo(DescendingEntityOrder));
        Assert.That(Collect(query.OrderBy(secondaryId, ref orderKeyComparer)), Is.EqualTo(AscendingEntityOrder));
        Assert.That(Collect(query.OrderBy(stackalloc ComponentId[] { secondaryId }, ref orderKeyComparer)), Is.EqualTo(AscendingEntityOrder));
        Assert.That(Collect(query.OrderBy(tagId, ref tagComparer)), Is.EqualTo(AscendingEntityOrder));
        Assert.That(Collect(query.OrderBy(ref tagComparer).ThenBy(ref orderKeyComparer)), Is.EqualTo(DescendingEntityOrder));
    }

    [Test]
    public void DelegateComparersSupportContextEntityAndChaining()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId keyId = layouts.Register<OrderKey>(new SchemaId(71_015));
        using var world = new World(layouts);
        var entities = new Entity[4];
        world.Create(stackalloc ComponentId[] { keyId }, entities.Length, entities);
        world.GetRef<OrderKey>(entities[0], keyId).Value = 2;
        world.GetRef<OrderKey>(entities[1], keyId).Value = 1;
        world.GetRef<OrderKey>(entities[2], keyId).Value = 1;
        world.GetRef<OrderKey>(entities[3], keyId).Value = 2;

        Query query = world.CreateQuery(QuerySpec.WhereAll(keyId));
        int offset = 7;
        OrderedQuery componentOrder = query.OrderBy(
            static (in OrderKey left, in OrderKey right) => left.Value.CompareTo(right.Value));
        OrderedQuery contextOrder = query.OrderBy(
            in offset,
            static (in int offset, in OrderKey left, in OrderKey right) =>
                (left.Value + offset).CompareTo(right.Value + offset));
        OrderedQuery entityOrder = query.OrderBy(
            static (Entity leftEntity, in OrderKey left, Entity rightEntity, in OrderKey right) =>
            {
                int valueOrder = left.Value.CompareTo(right.Value);
                return valueOrder != 0 ? valueOrder : leftEntity.Index.CompareTo(rightEntity.Index);
            });
        OrderedQuery chained = query
            .OrderBy(static (in OrderKey left, in OrderKey right) => left.Value.CompareTo(right.Value))
            .ThenBy(static (Entity leftEntity, in OrderKey left, Entity rightEntity, in OrderKey right) =>
                leftEntity.Index.CompareTo(rightEntity.Index));
        OrderedQuery contextualChain = query
            .OrderBy(keyId, in offset,
                static (in int state, in OrderKey left, in OrderKey right) =>
                    (left.Value + state).CompareTo(right.Value + state))
            .ThenBy(keyId, in offset,
                static (in int state, Entity leftEntity, in OrderKey left, Entity rightEntity, in OrderKey right) =>
                    (leftEntity.Index + state).CompareTo(rightEntity.Index + state));
        int capturedOffset = 0;
        OrderedQuery capturedDelegate = query
            .OrderBy((in OrderKey left, in OrderKey right) => left.Value.CompareTo(right.Value) + capturedOffset)
            .ThenBy((in OrderKey left, in OrderKey right) => left.Value.CompareTo(right.Value) + capturedOffset);

        Assert.That(Collect(componentOrder), Is.EqualTo(StableFourEntityOrder));
        Assert.That(Collect(contextOrder), Is.EqualTo(StableFourEntityOrder));
        Assert.That(Collect(entityOrder), Is.EqualTo(StableFourEntityOrder));
        Assert.That(Collect(chained), Is.EqualTo(StableFourEntityOrder));
        Assert.That(Collect(contextualChain), Is.EqualTo(StableFourEntityOrder));
        Assert.That(Collect(capturedDelegate), Is.EqualTo(StableFourEntityOrder));
    }

    [Test]
    public void FirstReturnsDefaultForEmptyOrderedQuery()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId keyId = layouts.Register<OrderKey>(new SchemaId(71_021));
        ComponentId tagId = layouts.Register<OrderTag>(new SchemaId(71_022));
        using var world = new World(layouts);
        Entity entity = world.Create(stackalloc ComponentId[] { keyId });
        world.GetRef<OrderKey>(entity, keyId).Value = 3;

        Query query = world.CreateQuery(QuerySpec.WhereAll(stackalloc ComponentId[] { keyId }));
        var keyComparer = default(OrderKeyComparer);
        OrderedQuery ordered = query
            .WhereAll(tagId)
            .OrderBy(ref keyComparer);

        Assert.That(ordered.First(), Is.EqualTo(default(Entity)));
        Assert.That(ordered.First(static _ => true), Is.EqualTo(default(Entity)));
        Assert.That(ordered.FirstEntity(), Is.EqualTo(default(Entity)));
    }

    [Test]
    public void OrderingKeyMustBeRequiredAndHaveMatchingRegistrationType()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId keyId = layouts.Register<OrderKey>(new SchemaId(71_031));
        ComponentId tieId = layouts.Register<TieKey>(new SchemaId(71_032));
        using var world = new World(layouts);
        Query query = world.CreateQuery(QuerySpec.WhereAll(keyId));

        Assert.Multiple(() =>
        {
            Assert.That(
                () =>
                {
                    var comparer = default(TieKeyComparer);
                    return query.OrderBy(tieId, ref comparer);
                },
                Throws.ArgumentException.With.Message.Contains("WhereAll"));
            Assert.That(
                () =>
                {
                    var comparer = default(OrderKeyComparer);
                    return query.OrderBy(tieId, ref comparer);
                },
                Throws.ArgumentException.With.Message.Contains("not"));
        });
    }

    private static int[] Collect(OrderedQuery query)
    {
        var result = new List<int>();
        query.ForEachEntity(ref result,
            static (ref List<int> indices, EntityRef entity) => indices.Add(entity.Index));
        return result.ToArray();
    }

    internal struct OrderKey
    {
        public int Value;
    }

    internal struct TieKey
    {
        public int Value;
    }

    internal struct OrderTag
    {
    }

    internal struct OrderedValueCollector : IForEach
    {
        public int[] Values;
        public int Count;

        public void Invoke(in OrderKey key)
            => Values[Count++] = key.Value;
    }

    internal struct OrderKeyComparer : IComponentComparer
    {
        public int Invoke(in OrderKey left, in OrderKey right) => left.Value.CompareTo(right.Value);
    }

    internal struct TieKeyComparer : IComponentComparer
    {
        public int Invoke(in TieKey left, in TieKey right) => left.Value.CompareTo(right.Value);
    }

    internal struct OrderTagComparer : IComponentComparer
    {
        public int Invoke(in OrderTag left, in OrderTag right) => 0;
    }

    internal struct OrderTieComparer : IComponentComparer
    {
        public int Invoke(in OrderKey leftKey, in TieKey leftTie, in OrderKey rightKey, in TieKey rightTie)
        {
            int keyComparison = leftKey.Value.CompareTo(rightKey.Value);
            return keyComparison != 0 ? keyComparison : leftTie.Value.CompareTo(rightTie.Value);
        }
    }
}
