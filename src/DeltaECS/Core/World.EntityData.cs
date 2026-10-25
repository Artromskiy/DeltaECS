namespace Delta.ECS;

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

public sealed partial class World
{
    private readonly ConcurrentDictionary<ComponentId, EntityData.ComponentReader?> _entityDataReaders = new();
    private readonly ConcurrentDictionary<int, EntityDataReadRoute[]> _entityDataSourceReadRoutes = new();
    private readonly ConditionalWeakTable<EntityData, EntityDataReadPlans> _entityDataReadPlans = new();

    internal int CopyEntityData(Entity entity, EntityData destination, bool add, bool set)
    {
        EnsureExecutionAccess();
        if (!TryResolveAliveLocation(entity, out Chunk chunk, out int slotIndex))
        {
            return 0;
        }
        Archetype archetype = Archetypes[chunk.ArchetypeId];
        if (!add)
        {
            EntityDataReadPlans plans = GetEntityDataReadPlans(destination);
            if (!plans.Routes.TryGetValue(archetype.Id, out EntityDataReadRoute[]? routes))
            {
                routes = BuildEntityDataSetRoutes(archetype, destination.ComponentIds);
                routes = plans.Routes.GetOrAdd(archetype.Id, routes);
            }
            int applied = 0;
            foreach (EntityDataReadRoute route in routes)
            {
                applied += route.Copy(chunk, slotIndex, destination, add: false, set: true);
            }
            return applied;
        }

        EntityDataReadRoute[] sourceRoutes = GetEntityDataSourceRoutes(archetype);
        ReadOnlySpan<int> tags = chunk.GetActiveTagIndices();
        int rowIndex = 0;
        int tagIndex = 0;
        int count = 0;
        // Both inputs are ordered by registration, preserving snapshot insertion order.
        while (rowIndex < sourceRoutes.Length || tagIndex < tags.Length)
        {
            if (rowIndex < sourceRoutes.Length
                && (tagIndex == tags.Length
                    || sourceRoutes[rowIndex].ComponentId.Value < _layouts.GetTagComponentId(tags[tagIndex]).Value))
            {
                count += sourceRoutes[rowIndex++].Copy(chunk, slotIndex, destination, add: true, set);
                continue;
            }
            int tag = tags[tagIndex++];
            if (chunk.HasTag(tag, slotIndex))
            {
                ComponentId id = _layouts.GetTagComponentId(tag);
                EntityData.ComponentReader? reader = GetEntityDataReader(id);
                if (reader is not null)
                {
                    count += reader.Copy(destination, id, null, slotIndex, add: true, set);
                }
            }
        }
        return count;
    }

    private EntityDataReadRoute[] GetEntityDataSourceRoutes(Archetype archetype)
    {
        if (_entityDataSourceReadRoutes.TryGetValue(archetype.Id, out EntityDataReadRoute[]? routes))
        {
            return routes;
        }
        ReadOnlySpan<ComponentId> ids = archetype.ComponentIds;
        routes = new EntityDataReadRoute[ids.Length];
        int count = 0;
        for (int row = 0; row < ids.Length; row++)
        {
            ComponentId id = ids[row];
            EntityData.ComponentReader? reader = GetEntityDataReader(id);
            if (reader is not null)
            {
                routes[count++] = new EntityDataReadRoute(id, row, reader);
            }
        }
        Array.Resize(ref routes, count);
        return _entityDataSourceReadRoutes.GetOrAdd(archetype.Id, routes);
    }

    private EntityDataReadRoute[] BuildEntityDataSetRoutes(Archetype archetype, ReadOnlySpan<ComponentId> ids)
    {
        var routes = new EntityDataReadRoute[ids.Length];
        int count = 0;
        foreach (ComponentId id in ids)
        {
            int row;
            if (_layouts.TryGetTagIndex(id, out int tagIndex))
            {
                row = -tagIndex - 1;
            }
            else if (!archetype.TryGetComponentIndex(id, out row))
            {
                continue;
            }
            EntityData.ComponentReader? reader = GetEntityDataReader(id);
            if (reader is not null)
            {
                routes[count++] = new EntityDataReadRoute(id, row, reader);
            }
        }
        Array.Resize(ref routes, count);
        return routes;
    }

    private EntityData.ComponentReader? GetEntityDataReader(ComponentId id)
    {
        if (_entityDataReaders.TryGetValue(id, out EntityData.ComponentReader? reader))
        {
            return reader;
        }
        var visitor = new EntityDataReaderVisitor();
        _layouts.Visit(id, visitor);
        return _entityDataReaders.GetOrAdd(id, visitor.Reader);
    }

    private EntityDataReadPlans GetEntityDataReadPlans(EntityData data)
    {
        if (_entityDataReadPlans.TryGetValue(data, out EntityDataReadPlans? plans)
            && plans.ShapeVersion == data.ShapeVersion && plans.RegistrationCount == _layouts.Count)
        {
            return plans;
        }
        lock (_entityDataPlanGate)
        {
            if (_entityDataReadPlans.TryGetValue(data, out plans)
                && plans.ShapeVersion == data.ShapeVersion && plans.RegistrationCount == _layouts.Count)
            {
                return plans;
            }
            plans = new EntityDataReadPlans(data.ShapeVersion, _layouts.Count);
            _entityDataReadPlans.Remove(data);
            _entityDataReadPlans.Add(data, plans);
            return plans;
        }
    }

    private readonly struct EntityDataReadRoute(ComponentId componentId, int row, EntityData.ComponentReader reader)
    {
        internal ComponentId ComponentId => componentId;

        internal int Copy(Chunk chunk, int slotIndex, EntityData destination, bool add, bool set)
        {
            if (row < 0)
            {
                return chunk.HasTag(-row - 1, slotIndex)
                    ? reader.Copy(destination, componentId, null, slotIndex, add, set)
                    : 0;
            }
            return reader.Copy(destination, componentId, chunk.GetRawComponentRowTrusted(row), slotIndex, add, set);
        }
    }

    private sealed class EntityDataReaderVisitor : IUnconstrainedComponentVisitor
    {
        internal EntityData.ComponentReader? Reader { get; private set; }
        public void Visit<T>(ComponentId componentId) => Reader = EntityData.GetComponentReader<T>();
    }

    private sealed class EntityDataReadPlans(int shapeVersion, int registrationCount)
    {
        internal int ShapeVersion => shapeVersion;
        internal int RegistrationCount => registrationCount;
        internal ConcurrentDictionary<int, EntityDataReadRoute[]> Routes { get; } = new();
    }
}
