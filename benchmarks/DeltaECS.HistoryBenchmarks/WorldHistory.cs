namespace Delta.ECS.HistoryBenchmarks;

using System;

// Restore-focused copy of SkyPirates history iteration over random entity compositions.
internal sealed class WorldHistory : IDisposable
{
    private const int HistoryComponentCount = 400;
    private const int ComponentTypeCount = 4;
    private const int RandomArchetypeCount = 4_096;
    private readonly World _world;
    private readonly Entity[] _entities;
    private readonly ComponentId[] _componentIds = new ComponentId[HistoryComponentCount];
    private readonly ComponentId[][] _entityComponentIds;
    private readonly IOperation[] _componentRestores = new IOperation[HistoryComponentCount];
    private readonly ArchetypeForEachOperation<RestoreContext> _archetypeRestore;

    internal WorldHistory(int entityCount)
    {
        var layouts = new ComponentLayoutRegistry();
        for (int index = 0; index < HistoryComponentCount; index++)
        {
            _componentIds[index] = RegisterComponent(layouts, index, new SchemaId((ulong)(index + 1)));
        }

        _world = new World(layouts, initialEntityCapacity: entityCount);
        _entities = new Entity[entityCount];
        _entityComponentIds = new ComponentId[entityCount][];
        var random = new Random(314159);
        ComponentId[][] archetypes = CreateRandomArchetypes(random);
        for (int entityIndex = 0; entityIndex < entityCount; entityIndex++)
        {
            ComponentId[] entityComponentIds = archetypes[random.Next(archetypes.Length)];
            ComponentMembershipCount += entityComponentIds.Length;
            Entity entity = _world.Create(entityComponentIds);
            _entities[entityIndex] = entity;
            _entityComponentIds[entityIndex] = entityComponentIds;
            foreach (ComponentId componentId in entityComponentIds)
            {
                SetComponentValue(entity, componentId, -1);
            }
        }

        _archetypeRestore = _world.ForEachArchetype(default(RestoreContext));
        for (int index = 0; index < HistoryComponentCount; index++)
        {
            Query query = _world.WhereAll(_componentIds[index]);
            _componentRestores[index] = CreateComponentRestore(in query, _componentIds[index], index);
            AddArchetypeRestore(in query, _componentIds[index], index);
        }
    }

    internal int ComponentMembershipCount { get; private set; }

    internal void RestoreByComponent()
    {
        for (int index = 0; index < _componentRestores.Length; index++)
        {
            _componentRestores[index].Invoke();
        }
    }

    internal void RestoreByArchetype() => _archetypeRestore.Invoke();

    internal ulong Checksum()
    {
        ulong checksum = 0;
        for (int entityIndex = 0; entityIndex < _entities.Length; entityIndex++)
        {
            Entity entity = _entities[entityIndex];
            foreach (ComponentId componentId in _entityComponentIds[entityIndex])
            {
                checksum = (checksum * 31) + (uint)GetComponentValue(entity, componentId);
            }
        }

        return checksum;
    }

    internal ulong ExpectedChecksum()
    {
        ulong checksum = 0;
        for (int entityIndex = 0; entityIndex < _entities.Length; entityIndex++)
        {
            Entity entity = _entities[entityIndex];
            foreach (ComponentId componentId in _entityComponentIds[entityIndex])
            {
                checksum = (checksum * 31) + (uint)(entity.Index + componentId.Value + 1);
            }
        }

        return checksum;
    }

    public void Dispose() => _world.Dispose();

    private static ComponentId RegisterComponent(ComponentLayoutRegistry layouts, int index, SchemaId schemaId) => (index % ComponentTypeCount) switch
    {
        0 => layouts.Register<HistoryPosition>(schemaId),
        1 => layouts.Register<HistoryVelocity>(schemaId),
        2 => layouts.Register<HistoryHealth>(schemaId),
        _ => layouts.Register<HistoryArmor>(schemaId)
    };

    private ComponentId[][] CreateRandomArchetypes(Random random)
    {
        var archetypes = new ComponentId[RandomArchetypeCount][];
        var candidates = new int[HistoryComponentCount];
        for (int index = 0; index < archetypes.Length; index++)
        {
            for (int componentIndex = 0; componentIndex < candidates.Length; componentIndex++)
            {
                candidates[componentIndex] = componentIndex;
            }

            int componentCount = random.Next(5, 21);
            var componentIds = new ComponentId[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                int selectedIndex = random.Next(componentIndex, candidates.Length);
                (candidates[componentIndex], candidates[selectedIndex]) = (candidates[selectedIndex], candidates[componentIndex]);
                componentIds[componentIndex] = _componentIds[candidates[componentIndex]];
            }

            archetypes[index] = componentIds;
        }

        return archetypes;
    }

    private IOperation CreateComponentRestore(in Query query, ComponentId id, int index) => (index % ComponentTypeCount) switch
    {
        0 => _world.ForEachEntity(in query, id, new RestorePosition(id)),
        1 => _world.ForEachEntity(in query, id, new RestoreVelocity(id)),
        2 => _world.ForEachEntity(in query, id, new RestoreHealth(id)),
        _ => _world.ForEachEntity(in query, id, new RestoreArmor(id))
    };

    private void AddArchetypeRestore(in Query query, ComponentId id, int index)
    {
        switch (index % ComponentTypeCount)
        {
            case 0: _archetypeRestore.Process<HistoryPosition, ArchetypeRestorePosition>(in query, id, new ArchetypeRestorePosition(id)); break;
            case 1: _archetypeRestore.Process<HistoryVelocity, ArchetypeRestoreVelocity>(in query, id, new ArchetypeRestoreVelocity(id)); break;
            case 2: _archetypeRestore.Process<HistoryHealth, ArchetypeRestoreHealth>(in query, id, new ArchetypeRestoreHealth(id)); break;
            default: _archetypeRestore.Process<HistoryArmor, ArchetypeRestoreArmor>(in query, id, new ArchetypeRestoreArmor(id)); break;
        }
    }

    private static int SnapshotValue(EntityRef entity, ComponentId componentId) => entity.Index + componentId.Value + 1;

    private void SetComponentValue(Entity entity, ComponentId componentId, int value)
    {
        switch (componentId.Value % ComponentTypeCount)
        {
            case 0: _world.GetRef<HistoryPosition>(entity, componentId).Value = value; break;
            case 1: _world.GetRef<HistoryVelocity>(entity, componentId).Value = value; break;
            case 2: _world.GetRef<HistoryHealth>(entity, componentId).Value = value; break;
            default: _world.GetRef<HistoryArmor>(entity, componentId).Value = value; break;
        }
    }

    private int GetComponentValue(Entity entity, ComponentId componentId) => (componentId.Value % ComponentTypeCount) switch
    {
        0 => _world.Get<HistoryPosition>(entity, componentId).Value,
        1 => _world.Get<HistoryVelocity>(entity, componentId).Value,
        2 => _world.Get<HistoryHealth>(entity, componentId).Value,
        _ => _world.Get<HistoryArmor>(entity, componentId).Value
    };

    internal struct RestoreContext;

    internal readonly struct RestorePosition(ComponentId id) : IForEachEntity
    {
        public void Invoke(EntityRef entity, ref HistoryPosition component) => component.Value = SnapshotValue(entity, id);
    }

    internal readonly struct RestoreVelocity(ComponentId id) : IForEachEntity
    {
        public void Invoke(EntityRef entity, ref HistoryVelocity component) => component.Value = SnapshotValue(entity, id);
    }

    internal readonly struct RestoreHealth(ComponentId id) : IForEachEntity
    {
        public void Invoke(EntityRef entity, ref HistoryHealth component) => component.Value = SnapshotValue(entity, id);
    }

    internal readonly struct RestoreArmor(ComponentId id) : IForEachEntity
    {
        public void Invoke(EntityRef entity, ref HistoryArmor component) => component.Value = SnapshotValue(entity, id);
    }

    internal readonly struct ArchetypeRestorePosition(ComponentId id) : IArchetypeForEachComponent<RestoreContext, HistoryPosition>
    {
        public void Invoke(ref RestoreContext context, EntityRef entity, ref HistoryPosition component) => component.Value = entity.Index + id.Value + 1;
    }

    internal readonly struct ArchetypeRestoreVelocity(ComponentId id) : IArchetypeForEachComponent<RestoreContext, HistoryVelocity>
    {
        public void Invoke(ref RestoreContext context, EntityRef entity, ref HistoryVelocity component) => component.Value = entity.Index + id.Value + 1;
    }

    internal readonly struct ArchetypeRestoreHealth(ComponentId id) : IArchetypeForEachComponent<RestoreContext, HistoryHealth>
    {
        public void Invoke(ref RestoreContext context, EntityRef entity, ref HistoryHealth component) => component.Value = entity.Index + id.Value + 1;
    }

    internal readonly struct ArchetypeRestoreArmor(ComponentId id) : IArchetypeForEachComponent<RestoreContext, HistoryArmor>
    {
        public void Invoke(ref RestoreContext context, EntityRef entity, ref HistoryArmor component) => component.Value = entity.Index + id.Value + 1;
    }
}
