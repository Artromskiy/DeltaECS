namespace DeltaECS.ProfileAdapter;

using Delta.ECS;

/// <summary>Same public-API workload is compiled once against each runtime revision.</summary>
public sealed class ProfileScenario : IDisposable
{
    private readonly World _world;
    private readonly ComponentId _primary;
    private readonly ComponentId _optional;
    private readonly ComponentId _sortPrimary;
    private readonly ComponentId _sortSecondary;
    private readonly ComponentId _structural;
    private readonly ComponentId[] _optionalIds;
    private readonly Entity[] _entities;
    private readonly Entity[] _shuffledEntities;
    private readonly Query _denseQuery;
    private readonly Query _sortQuery;
    private readonly OrderedQuery _orderedQuery;
    private int _toggle;
    private int _structuralToggle;
    private bool _structuralPresent;
    private long _denseChecksum;

    public ProfileScenario(int amount, int registeredComponents, int archetypes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegative(registeredComponents);
        if (archetypes is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(archetypes));

        var registry = new ComponentRegistry();
        for (int index = 0; index < registeredComponents; index++)
            registry.Register<ProfileValue>(new SchemaId((ulong)(100_000 + index)));
        int optionalCount = Math.Max(1, (int)Math.Ceiling(Math.Log2(archetypes)));
        _optionalIds = new ComponentId[optionalCount];
        for (int index = 0; index < optionalCount; index++)
            _optionalIds[index] = registry.Register<ProfileValue>(new SchemaId((ulong)(200_000 + index)));
        _primary = registry.Register<ProfileValue>(new SchemaId(300_000));
        _optional = registry.Register<ProfileValue>(new SchemaId(300_001));
        _sortPrimary = registry.Register<ProfileValue>(new SchemaId(300_002));
        _sortSecondary = registry.Register<ProfileValue>(new SchemaId(300_003));
        _structural = registry.Register<ProfileValue>(new SchemaId(300_004));
        _world = new World(registry, initialEntityCapacity: amount + 8);
        _entities = new Entity[amount];
        _world.Create([_primary, _sortPrimary, _sortSecondary], amount, _entities);
        _shuffledEntities = (Entity[])_entities.Clone();
        new Random(42).Shuffle(_shuffledEntities);
        _denseQuery = _world.CreateQuery(QuerySpec.WhereAll(_primary));
        _sortQuery = _world.CreateQuery(QuerySpec.WhereAll([_sortPrimary, _sortSecondary]));
        _orderedQuery = _sortQuery
            .OrderBy(_sortPrimary, _sortSecondary,
                static (in ProfileValue leftPrimary, in ProfileValue leftSecondary, in ProfileValue rightPrimary, in ProfileValue rightSecondary) =>
                {
                    int primary = leftPrimary.Value.CompareTo(rightPrimary.Value);
                    return primary != 0 ? primary : leftSecondary.Value.CompareTo(rightSecondary.Value);
                });
        for (int index = 0; index < amount; index++)
        {
            var value = new ProfileValue { Value = index + 1 };
            _world.GetRef<ProfileValue>(_entities[index], _primary) = value;
            var sortPrimary = new ProfileValue { Value = (index * 73) % 19 };
            var sortSecondary = new ProfileValue { Value = index };
            _world.GetRef<ProfileValue>(_entities[index], _sortPrimary) = sortPrimary;
            _world.GetRef<ProfileValue>(_entities[index], _sortSecondary) = sortSecondary;
            if ((index & 1) == 0)
            {
                var optionalValue = new ProfileValue { Value = index % 5 };
                _world.Add(_entities[index], _optional);
                _world.GetRef<ProfileValue>(_entities[index], _optional) = optionalValue;
            }
            int shape = index % archetypes;
            for (int bit = 0; bit < optionalCount; bit++)
            {
                if ((shape & (1 << bit)) != 0)
                {
                    var marker = new ProfileValue { Value = bit + 3 };
                    _world.Add(_entities[index], _optionalIds[bit]);
                    _world.GetRef<ProfileValue>(_entities[index], _optionalIds[bit]) = marker;
                }
            }
        }
    }

    public long LookupShuffled()
    {
        long sum = 0;
        int length = _entities.Length;
        for (int ordinal = 0; ordinal < length; ordinal++)
        {
            Entity entity = _shuffledEntities[ordinal];
            sum += _world.Get<ProfileValue>(entity, _primary).Value;
            sum += _world.TryGet(entity, _optional, out ProfileValue value) ? value.Value : 0;
            sum += _world.Has(entity, _optional) ? 1 : 0;
            sum += _world.IsAlive(entity) ? 1 : 0;
        }
        return sum;
    }

    public int StructuralToggle()
    {
        Entity entity = _entities[Math.Min(1, _entities.Length - 1)];
        bool add = !_structuralPresent;
        bool changed = add ? _world.Add(entity, _structural) : _world.Remove(entity, _structural);
        _structuralPresent = add;
        if (add)
        {
            var value = new ProfileValue { Value = 7 };
            _world.GetRef<ProfileValue>(entity, _structural) = value;
        }
        int result = ++_structuralToggle;
        return changed && _world.IsAlive(entity) ? (add ? result : -result) : 0;
    }

    public int CreateDestroy()
    {
        Entity entity = _world.Create(_primary);
        var value = new ProfileValue { Value = ++_toggle };
        _world.GetRef<ProfileValue>(entity, _primary) = value;
        return _world.Destroy(entity) && !_world.IsAlive(entity) ? value.Value : -value.Value;
    }

    public long StableSortTwoKeys()
    {
        long checksum = 0;
        try
        {
            foreach (Entity entity in _orderedQuery.BeginForEach()) checksum = unchecked(checksum * 31 + entity.Index);
        }
        finally { _orderedQuery.EndForEach(); }
        return checksum;
    }

    public long DenseEntityWalk()
    {
        _denseChecksum = 0;
        _world.ForEachEntity(in _denseQuery, ref _denseChecksum,
            static (ref long sum, EntityRef entity) => sum += entity.Index).Invoke(ref _denseChecksum);
        return _denseChecksum;
    }

    public void Dispose() => _world.Dispose();

    public string DescribeAllocations()
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            Iterations = 1000,
            LookupBytes = MeasureAllocations(() => LookupShuffled()),
            SortBytes = MeasureAllocations(() => StableSortTwoKeys()),
            DenseBytes = MeasureAllocations(() => DenseEntityWalk()),
            StructuralBytes = MeasureAllocations(() => StructuralToggle()),
            CreateDestroyBytes = MeasureAllocations(() => CreateDestroy())
        });

    private static long MeasureAllocations(Action operation)
    {
        for (int index = 0; index < 64; index++)
        {
            operation();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            operation();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    public string DescribeStorage()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var archetypes = (System.Collections.IList)typeof(World).GetProperty("Archetypes", flags)!.GetValue(_world)!;
        long indexBytes = 0;
        foreach (object archetype in archetypes)
        {
            object? storage = archetype.GetType().GetField("_componentIndices", flags)?.GetValue(archetype);
            if (storage is not null)
            {
                indexBytes += (int)storage.GetType().GetProperty("Length", flags)!.GetValue(storage)! * sizeof(int);
            }
        }

        Type recordType = typeof(World).Assembly.GetType("Delta.ECS.EntityRecord")!;
        int stride = (int)typeof(System.Runtime.CompilerServices.Unsafe).GetMethod("SizeOf")!
            .MakeGenericMethod(recordType).Invoke(null, null)!;
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            RuntimeFramework = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>(typeof(World).Assembly)!.FrameworkName,
            Archetypes = archetypes.Count,
            ComponentIndexBytes = indexBytes,
            EntityRecordStride = stride,
            EntityCapacity = _entities.Length + 8
        });
    }
}

public struct ProfileValue { public int Value; }
