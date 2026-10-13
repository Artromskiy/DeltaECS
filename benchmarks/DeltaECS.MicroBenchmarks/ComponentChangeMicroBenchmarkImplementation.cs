using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

public class ForEachWriteObserverMicroBenchmarkImplementation
{
    private ComponentWriteFixture[] _componentFixtures = null!;
    private TagWriteFixture[] _tagFixtures = null!;
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    [GlobalSetup]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The fixture arrays own these instances and GlobalCleanup disposes every entry.")]
    public void Setup()
    {
        _componentFixtures =
        [
            new ComponentWriteFixture(Amount, 1, 0),
            new ComponentWriteFixture(Amount, 1, 1),
            new ComponentWriteFixture(Amount, 1, 4),
            new ComponentWriteFixture(Amount, 4, 0),
            new ComponentWriteFixture(Amount, 4, 1),
            new ComponentWriteFixture(Amount, 4, 4),
            new ComponentWriteFixture(Amount, 4, 16)
        ];
        _tagFixtures =
        [
            new TagWriteFixture(Amount, 1, 0),
            new TagWriteFixture(Amount, 1, 1),
            new TagWriteFixture(Amount, 1, 4),
            new TagWriteFixture(Amount, 4, 0),
            new TagWriteFixture(Amount, 4, 1),
            new TagWriteFixture(Amount, 4, 4),
            new TagWriteFixture(Amount, 4, 16)
        ];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (ComponentWriteFixture fixture in _componentFixtures)
        {
            fixture.Dispose();
        }

        foreach (TagWriteFixture fixture in _tagFixtures)
        {
            fixture.Dispose();
        }
    }

    [Benchmark] public void ForEachOneComponentNoSubscribers() => _componentFixtures[0].Invoke();
    [Benchmark] public void ForEachOneComponentOneSubscriber() => _componentFixtures[1].Invoke();
    [Benchmark] public void ForEachOneComponentFourSubscribers() => _componentFixtures[2].Invoke();
    [Benchmark] public void ForEachFourComponentsNoSubscribers() => _componentFixtures[3].Invoke();
    [Benchmark] public void ForEachFourComponentsOneSubscriber() => _componentFixtures[4].Invoke();
    [Benchmark] public void ForEachFourComponentsFourSubscribers() => _componentFixtures[5].Invoke();
    [Benchmark] public void ForEachFourComponentsSixteenSubscribers() => _componentFixtures[6].Invoke();
    [Benchmark] public void ForEachOneTagNoSubscribers() => _tagFixtures[0].Invoke();
    [Benchmark] public void ForEachOneTagOneSubscriber() => _tagFixtures[1].Invoke();
    [Benchmark] public void ForEachOneTagFourSubscribers() => _tagFixtures[2].Invoke();
    [Benchmark] public void ForEachFourTagsNoSubscribers() => _tagFixtures[3].Invoke();
    [Benchmark] public void ForEachFourTagsOneSubscriber() => _tagFixtures[4].Invoke();
    [Benchmark] public void ForEachFourTagsFourSubscribers() => _tagFixtures[5].Invoke();
    [Benchmark] public void ForEachFourTagsSixteenSubscribers() => _tagFixtures[6].Invoke();
}

public class ForEachChangeMicroBenchmarkImplementation : IDisposable
{
    private ForEachChangeComponentFixture _componentFixture = null!;
    private ForEachChangeComponentFixture? _firstComponentFixture;
    private ForEachChangeTagFixture _tagFixture = null!;
    private ForEachChangeTagFixture? _firstTagFixture;
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    [GlobalSetup]
    public void Setup()
    {
        _componentFixture = new ForEachChangeComponentFixture(Amount);
        _componentFixture.Operation.Invoke();
        _tagFixture = new ForEachChangeTagFixture(Amount);
        _tagFixture.Operation.Invoke();
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    [IterationSetup(Target = nameof(ForEachChangeComponentFirstInvocation))]
    public void SetupFirstComponentInvocation() => _firstComponentFixture = new ForEachChangeComponentFixture(Amount);

    [IterationCleanup(Target = nameof(ForEachChangeComponentFirstInvocation))]
    public void CleanupFirstComponentInvocation()
    {
        _firstComponentFixture?.Dispose();
        _firstComponentFixture = null;
    }

    [IterationSetup(Target = nameof(ForEachChangeTagFirstInvocation))]
    public void SetupFirstTagInvocation() => _firstTagFixture = new ForEachChangeTagFixture(Amount);

    [IterationCleanup(Target = nameof(ForEachChangeTagFirstInvocation))]
    public void CleanupFirstTagInvocation()
    {
        _firstTagFixture?.Dispose();
        _firstTagFixture = null;
    }

    [IterationSetup(Target = nameof(ForEachChangeComponentOnePercentModified))]
    public void MarkOnePercentComponents() => _componentFixture.MarkModified(Math.Max(1, Amount / 100));

    [IterationSetup(Target = nameof(ForEachChangeComponentFiftyPercentModified))]
    public void MarkFiftyPercentComponents() => _componentFixture.MarkModified(Amount / 2);

    [IterationSetup(Target = nameof(ForEachChangeComponentOneHundredPercentModified))]
    public void MarkAllComponents() => _componentFixture.MarkModified(Amount);

    [IterationSetup(Target = nameof(ForEachChangeTagOnePercentModified))]
    public void MarkOnePercentTags() => _tagFixture.MarkModified(Math.Max(1, Amount / 100));

    [IterationSetup(Target = nameof(ForEachChangeTagFiftyPercentModified))]
    public void MarkFiftyPercentTags() => _tagFixture.MarkModified(Amount / 2);

    [IterationSetup(Target = nameof(ForEachChangeTagOneHundredPercentModified))]
    public void MarkAllTags() => _tagFixture.MarkModified(Amount);

    [Benchmark, InvocationCount(1)] public void ForEachChangeComponentFirstInvocation() => _firstComponentFixture!.Operation.Invoke();
    [Benchmark] public void ForEachChangeComponentsNoChanges() => _componentFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeComponentOnePercentModified() => _componentFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeComponentFiftyPercentModified() => _componentFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeComponentOneHundredPercentModified() => _componentFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeTagFirstInvocation() => _firstTagFixture!.Operation.Invoke();
    [Benchmark] public void ForEachChangeTagsNoChanges() => _tagFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeTagOnePercentModified() => _tagFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeTagFiftyPercentModified() => _tagFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeTagOneHundredPercentModified() => _tagFixture.Operation.Invoke();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _componentFixture?.Dispose();
        _tagFixture?.Dispose();
        _firstComponentFixture?.Dispose();
        _firstTagFixture?.Dispose();
    }
}

internal struct ChangeWriteA { public int Value; }
internal struct ChangeWriteB { public int Value; }
internal struct ChangeWriteC { public int Value; }
internal struct ChangeWriteD { public int Value; }
internal struct ChangeWriteUnused { public int Value { get; set; } }
internal struct ChangeWriteTagA { }
internal struct ChangeWriteTagB { }
internal struct ChangeWriteTagC { }
internal struct ChangeWriteTagD { }

internal sealed class ComponentWriteFixture : IDisposable
{
    private readonly ComponentId[] _componentIds;
    private readonly Entity[] _entities;
    private readonly IOperation _write;
    private readonly IOperation[] _subscribers;

    internal World World { get; }

    internal ComponentWriteFixture(int amount, int componentCount, int subscriberCount)
    {
        var layouts = new ComponentRegistry();
        _componentIds =
        [
            layouts.Register<ChangeWriteA>(new SchemaId(93_001)),
            layouts.Register<ChangeWriteB>(new SchemaId(93_002)),
            layouts.Register<ChangeWriteC>(new SchemaId(93_003)),
            layouts.Register<ChangeWriteD>(new SchemaId(93_004))
        ];
        ComponentId unusedId = layouts.Register<ChangeWriteUnused>(new SchemaId(93_005));
        World = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        var entityComponents = new ComponentId[componentCount + 1];
        _componentIds.AsSpan(0, componentCount).CopyTo(entityComponents);
        entityComponents[^1] = unusedId;
        World.Create(entityComponents, amount, _entities);

        QuerySpec description = QuerySpec.WhereAll(_componentIds.AsSpan(0, componentCount));
        Query query = World.CreateQuery(in description);
        _write = componentCount switch
        {
            1 => World.ForEach(in query, static (ref ChangeWriteA a) => a.Value++),
            4 => World.ForEach(in query,
                static (ref ChangeWriteA a, ref ChangeWriteB b, ref ChangeWriteC c, ref ChangeWriteD d) =>
                {
                    a.Value++;
                    b.Value++;
                    c.Value++;
                    d.Value++;
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(componentCount))
        };

        _subscribers = new IOperation[subscriberCount];
        for (int index = 0; index < subscriberCount; index++)
        {
            _subscribers[index] = CreateSubscriber(index % componentCount);
            _subscribers[index].Invoke();
        }
    }
    public void Dispose() { World.Dispose(); GC.SuppressFinalize(this); }
    internal void Invoke() => _write.Invoke();

    private IOperation CreateSubscriber(int index) => index switch
    {
        0 => World.ForEachChange<ChangeWriteA>(_componentIds[index], static (entity, change, ref component) => { }),
        1 => World.ForEachChange<ChangeWriteB>(_componentIds[index], static (entity, change, ref component) => { }),
        2 => World.ForEachChange<ChangeWriteC>(_componentIds[index], static (entity, change, ref component) => { }),
        3 => World.ForEachChange<ChangeWriteD>(_componentIds[index], static (entity, change, ref component) => { }),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal sealed class TagWriteFixture : IDisposable
{
    private readonly ComponentId[] _tagIds;
    private readonly IOperation _write;
    private readonly IOperation[] _subscribers;

    internal World World { get; }

    internal TagWriteFixture(int amount, int tagCount, int subscriberCount)
    {
        var layouts = new ComponentRegistry();
        _tagIds =
        [
            layouts.Register<ChangeWriteTagA>(new SchemaId(93_011)),
            layouts.Register<ChangeWriteTagB>(new SchemaId(93_012)),
            layouts.Register<ChangeWriteTagC>(new SchemaId(93_013)),
            layouts.Register<ChangeWriteTagD>(new SchemaId(93_014))
        ];
        ComponentId unusedId = layouts.Register<ChangeWriteUnused>(new SchemaId(93_015));
        World = new World(layouts, initialEntityCapacity: amount);
        var entities = new ComponentId[tagCount + 1];
        _tagIds.AsSpan(0, tagCount).CopyTo(entities);
        entities[^1] = unusedId;
        World.Create(entities, amount);

        QuerySpec description = QuerySpec.WhereAll(_tagIds.AsSpan(0, tagCount));
        Query query = World.CreateQuery(in description);
        _write = tagCount switch
        {
            1 => World.ForEach(in query, static (ref ChangeWriteTagA tag) => { }),
            4 => World.ForEach(in query,
                static (ref ChangeWriteTagA a, ref ChangeWriteTagB b, ref ChangeWriteTagC c, ref ChangeWriteTagD d) => { }),
            _ => throw new ArgumentOutOfRangeException(nameof(tagCount))
        };

        _subscribers = new IOperation[subscriberCount];
        for (int index = 0; index < subscriberCount; index++)
        {
            _subscribers[index] = CreateSubscriber(index % tagCount);
            _subscribers[index].Invoke();
        }
    }
    public void Dispose() { World.Dispose(); GC.SuppressFinalize(this); }
    internal void Invoke() => _write.Invoke();

    private IOperation CreateSubscriber(int index) => index switch
    {
        0 => World.ForEachChange<ChangeWriteTagA>(_tagIds[index], static (entity, change, ref tag) => { }),
        1 => World.ForEachChange<ChangeWriteTagB>(_tagIds[index], static (entity, change, ref tag) => { }),
        2 => World.ForEachChange<ChangeWriteTagC>(_tagIds[index], static (entity, change, ref tag) => { }),
        3 => World.ForEachChange<ChangeWriteTagD>(_tagIds[index], static (entity, change, ref tag) => { }),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

internal sealed class ForEachChangeComponentFixture : IDisposable
{
    private readonly ComponentId _componentId;
    private readonly Entity[] _entities;

    internal World World { get; }
    internal ComponentChangeOperation<ChangeWriteA> Operation { get; }
    internal long NotificationCount { get; private set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "The fixed-seed shuffle creates deterministic benchmark data and is not security-sensitive.")]
    internal ForEachChangeComponentFixture(int amount)
    {
        var layouts = new ComponentRegistry();
        _componentId = layouts.Register<ChangeWriteA>(new SchemaId(93_001));
        ComponentId unusedId = layouts.Register<ChangeWriteUnused>(new SchemaId(93_005));
        World = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        World.Create([_componentId, unusedId], amount, _entities);
        if (MicroBenchmarkConfiguration.ScatterChanges) { new Random(93_001).Shuffle(_entities); }
        Operation = World.ForEachChange<ChangeWriteA>(
            _componentId,
            MicroBenchmarkConfiguration.CountChangeCallbacks ? Record : static (Entity entity, ChangeKind change, ref ChangeWriteA component) => { });
    }

    public void Dispose() { World.Dispose(); GC.SuppressFinalize(this); }

    internal void MarkModified(int count)
    {
        for (int index = 0; index < count; index++)
        {
            World.GetRef<ChangeWriteA>(_entities[index], _componentId) = default;
        }
    }

    private void Record(Entity entity, ChangeKind change, ref ChangeWriteA component) => NotificationCount++;
}

internal sealed class ForEachChangeTagFixture : IDisposable
{
    private readonly ComponentId _tagId;
    private readonly Entity[] _entities;

    internal World World { get; }
    internal ComponentChangeOperation<ChangeWriteTagA> Operation { get; }
    internal long NotificationCount { get; private set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "The fixed-seed shuffle creates deterministic benchmark data and is not security-sensitive.")]
    internal ForEachChangeTagFixture(int amount)
    {
        var layouts = new ComponentRegistry();
        _tagId = layouts.Register<ChangeWriteTagA>(new SchemaId(93_011));
        ComponentId unusedId = layouts.Register<ChangeWriteUnused>(new SchemaId(93_005));
        World = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        World.Create([_tagId, unusedId], amount, _entities);
        if (MicroBenchmarkConfiguration.ScatterChanges) { new Random(93_001).Shuffle(_entities); }
        Operation = World.ForEachChange<ChangeWriteTagA>(
            _tagId,
            MicroBenchmarkConfiguration.CountChangeCallbacks ? Record : static (Entity entity, ChangeKind change, ref ChangeWriteTagA tag) => { });
    }

    public void Dispose() { World.Dispose(); GC.SuppressFinalize(this); }

    internal void MarkModified(int count)
    {
        for (int index = 0; index < count; index++)
        {
            World.GetRef<ChangeWriteTagA>(_entities[index], _tagId) = default;
        }
    }

    private void Record(Entity entity, ChangeKind change, ref ChangeWriteTagA component) => NotificationCount++;
}
