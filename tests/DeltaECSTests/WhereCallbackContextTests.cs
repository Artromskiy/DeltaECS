using NUnit.Framework;

namespace Delta.ECS.Tests;

[TestFixture]
internal sealed class WhereCallbackContextTests
{
    private static readonly int[] OrderedOffsets = [2, 3, 1];

    [Test]
    public void EntityOnlyCallbackCanBeCachedWithCollectionContext()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        Entity first = world.Create(valueId, new Value(1));
        Entity second = world.Create(valueId, new Value(2));
        Query query = world.WhereAll<Value>();
        var result = new List<Entity>();
        IOperation<List<Entity>> operation = world.Where(in query, static (in Value value) => value.Number <= 1)
            .ForEachEntity(ref result, static (ref List<Entity> entities, EntityRef entity) => entities.Add(entity));

        operation.Invoke();
        Assert.That(result, Is.EqualTo(new[] { first }));

        result.Clear();
        world.GetRef<Value>(second).Number = 0;
        operation.Invoke();
        Assert.That(result, Is.EquivalentTo(new[] { first, second }));

        var callerResult = new List<Entity>();
        operation.Invoke(ref callerResult);
        Assert.That(callerResult, Is.EquivalentTo(new[] { first, second }));
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void ComponentCallbackKeepsOwnedAndCallerContextsSeparate()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        Entity entity = world.Create(valueId, new Value(0));
        Query query = world.WhereAll<Value>();
        int context = 0;
        IOperation<int> operation = world.Where(in query, static (in Value value) => value.Number >= 0)
            .ForEach(ref context, static (ref int count, ref Value value) => value.Number = ++count);

        operation.Invoke();
        operation.Invoke();
        Assert.That(world.Get<Value>(entity).Number, Is.EqualTo(2));
        Assert.That(context, Is.Zero);

        int callerContext = 10;
        operation.Invoke(ref callerContext);
        Assert.That(callerContext, Is.EqualTo(11));
        operation.Invoke();
        Assert.That(world.Get<Value>(entity).Number, Is.EqualTo(3));
    }

    [Test]
    public void EntityCallbackSupportsPredicateContextAndMultipleComponentRows()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        ComponentId offsetId = world.Components.Register<Offset>(new SchemaId(71_002));
        Entity selected = world.Create<Value, Offset>(valueId, offsetId, new Value(1), new Offset(5));
        Entity excluded = world.Create<Value, Offset>(valueId, offsetId, new Value(8), new Offset(10));
        Query query = world.WhereAll<Value, Offset>();
        int limit = 3;
        var result = new List<Entity>();
        var operation = world.WhereEntity(in query, ref limit,
                static (ref int maximum, Entity entity, in Value value) => value.Number < maximum)
            .ForEachEntity(ref result, static (ref List<Entity> entities, EntityRef entity, ref Value value, in Offset offset) =>
            {
                entities.Add(entity);
                value.Number += offset.Number;
            });

        operation.Invoke();
        Assert.That(result, Is.EqualTo(new[] { selected }));
        Assert.That(world.Get<Value>(selected).Number, Is.EqualTo(6));
        Assert.That(world.Get<Value>(excluded).Number, Is.EqualTo(8));
    }

    [Test]
    public void CapturingCallbackUsesContextWithoutInterception()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        world.Create(valueId, new Value(2));
        Query query = world.WhereAll<Value>();
        int multiplier = 3;
        int sum = 0;
        var operation = world.Where(in query, static (in Value value) => value.Number > 0)
            .ForEach(ref sum, (ref int total, in Value value) =>
            {
                total += value.Number * multiplier;
            });

        operation.Invoke(ref sum);
        operation.Invoke(ref sum);
        Assert.That(sum, Is.EqualTo(12));
    }

    [Test]
    public void CallbackExceptionReleasesLeaseAndDoesNotCommitValueContext()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        world.Create(valueId, new Value(2));
        Query query = world.WhereAll<Value>();
        int sum = 0;
        var operation = world.Where(in query, static (in Value value) => value.Number > 0)
            .ForEachEntity(ref sum, static (ref int total, EntityRef entity) =>
            {
                total++;
                throw new InvalidOperationException("Callback failed.");
            });

        Assert.Throws<InvalidOperationException>(() => operation.Invoke(ref sum));
        Assert.That(sum, Is.Zero);
        Assert.DoesNotThrow(() => world.Create(valueId, new Value(3)));
    }

    [Test]
    public void ReadOnlyCallbackDoesNotNotifyChangeObservers()
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        world.Create(valueId, new Value(2));
        Query query = world.WhereAll<Value>();
        var changes = new List<ChangeKind>();
        var observer = world.ForEachChange<Value>(valueId, (_, kind, ref _) => changes.Add(kind));
        observer.Invoke();
        changes.Clear();
        int sum = 0;

        world.Where(in query, static (in Value value) => value.Number > 0)
            .ForEach(ref sum, static (ref int total, in Value value) => total += value.Number).Invoke(ref sum);
        observer.Invoke();
        Assert.That(sum, Is.EqualTo(2));
        Assert.That(changes, Is.Empty);

        world.Where(in query, static (in Value value) => value.Number > 0)
            .ForEach(ref sum, static (ref int total, ref Value value) => value.Number += total).Invoke(ref sum);
        observer.Invoke();
        Assert.That(changes, Is.EqualTo(new[] { ChangeKind.Modified }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrderedCallbacksPreserveContextAndLexicographicOrder(bool filtered)
    {
        using var world = new World();
        ComponentId valueId = world.Components.Register<Value>(new SchemaId(71_001));
        ComponentId offsetId = world.Components.Register<Offset>(new SchemaId(71_002));
        world.Create<Value, Offset>(valueId, offsetId, new Value(2), new Offset(1));
        world.Create<Value, Offset>(valueId, offsetId, new Value(1), new Offset(3));
        world.Create<Value, Offset>(valueId, offsetId, new Value(1), new Offset(2));
        Query query = world.WhereAll<Value, Offset>();
        var result = new List<int>();
        IOperation<List<int>> components;
        IOperation<List<int>> entities;
        if (filtered)
        {
            components = world.Where(in query, static (in Value value) => value.Number > 0)
                .OrderBy(static (in Value left, in Value right) => left.Number.CompareTo(right.Number))
                .ThenBy(static (in Offset left, in Offset right) => left.Number.CompareTo(right.Number))
                .ForEach(ref result, static (ref List<int> values, in Offset offset) => values.Add(offset.Number));
            entities = world.Where(in query, static (in Value value) => value.Number > 0)
                .OrderBy(static (in Value left, in Value right) => left.Number.CompareTo(right.Number))
                .ThenBy(static (in Offset left, in Offset right) => left.Number.CompareTo(right.Number))
                .ForEachEntity(ref result, static (ref List<int> values, EntityRef entity) => values.Add(entity.Get<Offset>().Number));
        }
        else
        {
            components = query.OrderBy(static (in Value left, in Value right) => left.Number.CompareTo(right.Number))
                .ThenBy(static (in Offset left, in Offset right) => left.Number.CompareTo(right.Number))
                .ForEach(ref result, static (ref List<int> values, in Offset offset) => values.Add(offset.Number));
            entities = query.OrderBy(static (in Value left, in Value right) => left.Number.CompareTo(right.Number))
                .ThenBy(static (in Offset left, in Offset right) => left.Number.CompareTo(right.Number))
                .ForEachEntity(ref result, static (ref List<int> values, EntityRef entity) => values.Add(entity.Get<Offset>().Number));
        }

        components.Invoke();
        Assert.That(result, Is.EqualTo(OrderedOffsets));
        result.Clear();
        entities.Invoke();
        Assert.That(result, Is.EqualTo(OrderedOffsets));
    }

    internal record struct Value(int Number);
    internal record struct Offset(int Number);
}
