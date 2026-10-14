namespace DeltaECS.ProfileAdapter;

using Delta.ECS;

/// <summary>Repeatedly creates distinct query plans over many matching archetypes.</summary>
public sealed class QueryPlanScenario : IDisposable
{
    private const int AbsentComponentCount = 512;
    private const int OptionalComponentCount = 32;
    private const int RequiredComponentCount = 256;
    private const int ArchetypeCount = 32;
    private const int QueryCount = 512;

    private readonly World _world;
    private readonly QuerySpec[] _specs;
    private readonly Query[] _queries = new Query[QueryCount];

    public QueryPlanScenario()
    {
        var registry = new ComponentRegistry();
        var ids = new ComponentId[AbsentComponentCount + OptionalComponentCount + RequiredComponentCount];
        for (int index = 0; index < ids.Length; index++)
        {
            ids[index] = registry.Register<ProfileValue>(new SchemaId((ulong)(700_000 + index)));
        }

        _world = new World(registry, initialEntityCapacity: ArchetypeCount);
        var archetypeComponents = new ComponentId[RequiredComponentCount + 1];
        ids.AsSpan(AbsentComponentCount + OptionalComponentCount, RequiredComponentCount)
            .CopyTo(archetypeComponents);
        for (int archetype = 0; archetype < ArchetypeCount; archetype++)
        {
            archetypeComponents[^1] = ids[AbsentComponentCount + archetype];
            _world.Create(archetypeComponents);
        }

        var allRequired = ids.AsSpan(AbsentComponentCount + OptionalComponentCount, RequiredComponentCount);
        _specs = new QuerySpec[QueryCount];
        for (int index = 0; index < _specs.Length; index++)
        {
            _specs[index] = QuerySpec.WhereAll(allRequired).WithNone([ids[index]]);
        }

        QuerySpec validationSpec = QuerySpec.WhereAll(allRequired);
        Query validationQuery = _world.CreateQuery(validationSpec);
        int matchedEntities = 0;
        _world.ForEachEntity(in validationQuery, ref matchedEntities,
            static (ref int count, EntityRef _) => count++).Invoke(ref matchedEntities);
        if (matchedEntities != ArchetypeCount)
        {
            throw new InvalidOperationException($"Expected {ArchetypeCount} matching entities, got {matchedEntities}.");
        }
    }

    /// <summary>Creates 512 new plans and keeps their handles alive through the benchmark iteration.</summary>
    public int CreateQueryBatch()
    {
        int validQueries = 0;
        for (int index = 0; index < _specs.Length; index++)
        {
            Query query = _world.CreateQuery(_specs[index]);
            _queries[index] = query;
            validQueries += query.IsValid ? 1 : 0;
        }

        if (validQueries != QueryCount)
        {
            throw new InvalidOperationException($"Expected {QueryCount} valid queries, got {validQueries}.");
        }

        return validQueries;
    }

    public void Dispose() => _world.Dispose();
}
