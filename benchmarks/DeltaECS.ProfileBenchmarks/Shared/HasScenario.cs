namespace DeltaECS.ProfileAdapter;

using Delta.ECS;

/// <summary>Mixed data/tag presence workload for the public Has overloads.</summary>
public sealed class HasScenario : IDisposable
{
    private readonly World _world;
    private readonly ComponentId _dataId;
    private readonly ComponentId _tagId;
    private readonly Entity[] _sequential;
    private readonly Entity[] _shuffled;

    public long ExpectedDataCount => (_sequential.Length / 2L) + (_sequential.Length & 1);

    public long ExpectedTagCount => (_sequential.Length / 2L) + (_sequential.Length & 1);

    public HasScenario(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var registry = new ComponentRegistry();
        ComponentId baseId = registry.Register<HasBaseValue>(new SchemaId(400_000));
        _dataId = registry.Register<HasDataValue>(new SchemaId(400_001));
        _tagId = registry.Register<HasTag>(new SchemaId(400_002));
        _world = new World(registry, initialEntityCapacity: amount + 8);
        _sequential = new Entity[amount];

        var componentSets = new[]
        {
            new[] { baseId, _dataId, _tagId },
            new[] { baseId, _dataId },
            new[] { baseId, _tagId },
            new[] { baseId }
        };
        var groups = new Entity[componentSets.Length][];
        int[] groupCounts = new int[componentSets.Length];
        int baseCount = amount / componentSets.Length;
        ReadOnlySpan<int> remainderOrder = amount % groups.Length == 3 ? [0, 1, 2, 3] : [0, 3, 1, 2];
        for (int group = 0; group < groups.Length; group++)
        {
            int count = baseCount + (remainderOrder[..(amount % groups.Length)].Contains(group) ? 1 : 0);
            groupCounts[group] = count;
            groups[group] = new Entity[count];
            _world.Create(componentSets[group], count, groups[group]);
        }

        int destination = 0;
        for (int row = 0; destination < amount; row++)
        {
            for (int group = 0; group < groups.Length; group++)
            {
                if (row < groupCounts[group])
                {
                    _sequential[destination++] = groups[group][row];
                }
            }
        }

        _shuffled = (Entity[])_sequential.Clone();
        new Random(42).Shuffle(_shuffled);
    }

    public long HasDataByIdSequential() => HasById(_sequential, _dataId);

    public long HasDataByIdShuffled() => HasById(_shuffled, _dataId);

    public long HasTagByIdSequential() => HasById(_sequential, _tagId);

    public long HasTagByIdShuffled() => HasById(_shuffled, _tagId);

    public long HasDataGenericSequential() => HasDataGeneric(_sequential);

    public long HasDataGenericShuffled() => HasDataGeneric(_shuffled);

    public void Dispose() => _world.Dispose();

    private long HasById(Entity[] entities, ComponentId componentId)
    {
        long count = 0;
        for (int index = 0; index < entities.Length; index++)
        {
            count += _world.Has(entities[index], componentId) ? 1 : 0;
        }

        return count;
    }

    private long HasDataGeneric(Entity[] entities)
    {
        long count = 0;
        for (int index = 0; index < entities.Length; index++)
        {
            count += _world.Has<HasDataValue>(entities[index]) ? 1 : 0;
        }

        return count;
    }
}

public struct HasBaseValue { public int Value; }

public struct HasDataValue { public int Value; }

public struct HasTag { }
