using BenchmarkDotNet.Attributes;
using Delta.ECS;

namespace Delta.ECS.MicroBenchmarks;

internal struct ChangeWriteA { public int Value; }
internal struct ChangeWriteB { public int Value; }
internal struct ChangeWriteC { public int Value; }
internal struct ChangeWriteD { public int Value; }

internal sealed class ChangeTrackingFixture : IDisposable
{
    private readonly ComponentId[] _componentIds;
    private readonly Entity[] _entities;
    private readonly IOperation _write;
    private readonly IOperation[] _subscribers;

    internal ChangeTrackingFixture(int amount, int writtenComponentCount, int subscriberCount)
    {
        var layouts = new ComponentRegistry();
        _componentIds =
        [
            layouts.Register<ChangeWriteA>(new SchemaId(93_001)),
            layouts.Register<ChangeWriteB>(new SchemaId(93_002)),
            layouts.Register<ChangeWriteC>(new SchemaId(93_003)),
            layouts.Register<ChangeWriteD>(new SchemaId(93_004))
        ];
        World = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        World.Create(_componentIds.AsSpan(0, writtenComponentCount), amount, _entities);
        QuerySpec description = QuerySpec.WhereAll(_componentIds.AsSpan(0, writtenComponentCount));
        Query query = World.CreateQuery(in description);
        _write = writtenComponentCount switch
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
            _ => throw new ArgumentOutOfRangeException(nameof(writtenComponentCount))
        };

        _subscribers = new IOperation[subscriberCount];
        for (int subscriberIndex = 0; subscriberIndex < subscriberCount; subscriberIndex++)
        {
            _subscribers[subscriberIndex] = CreateSubscriber(subscriberIndex % writtenComponentCount);
        }

        for (int index = 0; index < _subscribers.Length; index++)
        {
            _subscribers[index].Invoke();
        }
    }

    internal World World { get; }

    internal void Invoke() => _write.Invoke();

    public void Dispose() => World.Dispose();

    private IOperation CreateSubscriber(int componentIndex) => componentIndex switch
    {
        0 => World.ForEachChange<ChangeWriteA>(_componentIds[componentIndex], static (Entity entity, ChangeKind change, ref ChangeWriteA component) => { }),
        1 => World.ForEachChange<ChangeWriteB>(_componentIds[componentIndex], static (Entity entity, ChangeKind change, ref ChangeWriteB component) => { }),
        2 => World.ForEachChange<ChangeWriteC>(_componentIds[componentIndex], static (Entity entity, ChangeKind change, ref ChangeWriteC component) => { }),
        3 => World.ForEachChange<ChangeWriteD>(_componentIds[componentIndex], static (Entity entity, ChangeKind change, ref ChangeWriteD component) => { }),
        _ => throw new ArgumentOutOfRangeException(nameof(componentIndex))
    };
}

public class ForEachWriteObserverMicroBenchmarkImplementation
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private ChangeTrackingFixture[] _fixtures = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixtures =
        [
            new ChangeTrackingFixture(Amount, 1, 0),
            new ChangeTrackingFixture(Amount, 1, 1),
            new ChangeTrackingFixture(Amount, 1, 4),
            new ChangeTrackingFixture(Amount, 4, 0),
            new ChangeTrackingFixture(Amount, 4, 1),
            new ChangeTrackingFixture(Amount, 4, 4),
            new ChangeTrackingFixture(Amount, 4, 16)
        ];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        for (int index = 0; index < _fixtures.Length; index++)
        {
            _fixtures[index].Dispose();
        }
    }

    [Benchmark] public void ForEachOneComponentNoSubscribers() => _fixtures[0].Invoke();
    [Benchmark] public void ForEachOneComponentOneSubscriber() => _fixtures[1].Invoke();
    [Benchmark] public void ForEachOneComponentFourSubscribers() => _fixtures[2].Invoke();
    [Benchmark] public void ForEachFourComponentsNoSubscribers() => _fixtures[3].Invoke();
    [Benchmark] public void ForEachFourComponentsOneSubscriber() => _fixtures[4].Invoke();
    [Benchmark] public void ForEachFourComponentsFourSubscribers() => _fixtures[5].Invoke();
    [Benchmark] public void ForEachFourComponentsSixteenSubscribers() => _fixtures[6].Invoke();
}

internal sealed class ForEachChangeFixture : IDisposable
{
    private readonly ComponentId _componentId;
    private readonly Entity[] _entities;

    internal ForEachChangeFixture(int amount)
    {
        var layouts = new ComponentRegistry();
        _componentId = layouts.Register<ChangeWriteA>(new SchemaId(93_001));
        World = new World(layouts, initialEntityCapacity: amount);
        _entities = new Entity[amount];
        World.Create(_componentId, amount, _entities);
        Operation = World.ForEachChange<ChangeWriteA>(
            _componentId,
            static (Entity entity, ChangeKind change, ref ChangeWriteA component) => { });
    }

    internal World World { get; }

    internal ComponentChangeOperation<ChangeWriteA> Operation { get; }

    internal void MarkModified(int count)
    {
        for (int index = 0; index < count; index++)
        {
            World.GetRef<ChangeWriteA>(_entities[index], _componentId).Value++;
        }
    }

    public void Dispose() => World.Dispose();
}

public class ForEachChangeMicroBenchmarkImplementation : IDisposable
{
    public int Amount { get; set; } = MicroBenchmarkConfiguration.CurrentAmount;

    private ForEachChangeFixture _steadyFixture = null!;
    private ForEachChangeFixture? _firstInvocationFixture;

    [GlobalSetup]
    public void Setup()
    {
        _steadyFixture = new ForEachChangeFixture(Amount);
        _steadyFixture.Operation.Invoke();
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    [IterationSetup(Target = nameof(ForEachChangeFirstInvocation))]
    public void SetupFirstInvocation() => _firstInvocationFixture = new ForEachChangeFixture(Amount);

    [IterationCleanup(Target = nameof(ForEachChangeFirstInvocation))]
    public void CleanupFirstInvocation()
    {
        _firstInvocationFixture?.Dispose();
        _firstInvocationFixture = null;
    }

    [IterationSetup(Target = nameof(ForEachChangeOnePercentModified))]
    public void MarkOnePercentModified() => _steadyFixture.MarkModified(Math.Max(1, Amount / 100));

    [IterationSetup(Target = nameof(ForEachChangeFiftyPercentModified))]
    public void MarkFiftyPercentModified() => _steadyFixture.MarkModified(Amount / 2);

    [Benchmark, InvocationCount(1)] public void ForEachChangeFirstInvocation() => _firstInvocationFixture!.Operation.Invoke();
    [Benchmark] public void ForEachChangeNoChanges() => _steadyFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeOnePercentModified() => _steadyFixture.Operation.Invoke();
    [Benchmark, InvocationCount(1)] public void ForEachChangeFiftyPercentModified() => _steadyFixture.Operation.Invoke();

    public void Dispose()
    {
        _steadyFixture.Dispose();
        _firstInvocationFixture?.Dispose();
        GC.SuppressFinalize(this);
    }
}
