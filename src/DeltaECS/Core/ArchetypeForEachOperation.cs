namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>Processes an entity selected by an archetype iteration query.</summary>
public interface IArchetypeForEachEntity<TContext>
{
    /// <summary>Processes the current borrowed entity using caller-owned state.</summary>
    void Invoke(ref TContext context, EntityRef entity);
}

/// <summary>Processes a required component with direct typed access during archetype iteration.</summary>
public interface IArchetypeForEachComponent<TContext, TComponent>
{
    /// <summary>Processes the current entity and its writable component reference.</summary>
    void Invoke(ref TContext context, EntityRef entity, ref TComponent component);
}

/// <summary>A reusable operation with query-specific archetype processors.</summary>
/// <remarks>Queries and processors are added during setup. Each invocation reuses the matching archetype plan.</remarks>
public sealed class ArchetypeForEachOperation<TContext> : IOperation<TContext>
{
    private readonly World _world;
    private readonly List<IArchetypeForEachCase<TContext>> _cases = new();
    private ArchetypeForEachPlan[] _plans = Array.Empty<ArchetypeForEachPlan>();
    private TContext _context = default!;
    private int _caseVersion;
    private int _plannedCaseVersion = -1;
    private uint _writeVersion;
    private bool _invoking;

    internal ArchetypeForEachOperation(World world, TContext context)
    {
        _world = world;
        _context = context;
    }

    /// <summary>Adds a query and its entity processor to this shared archetype traversal.</summary>
    public ArchetypeForEachOperation<TContext> Process<TProcessor>(in Query query, TProcessor processor)
        where TProcessor : struct, IArchetypeForEachEntity<TContext>
    {
        if (_invoking)
        {
            ThrowHelper.ThrowCannotAddArchetypeProcessorWhileRunning();
        }

        if (!query.IsValid || !ReferenceEquals(query.Owner, _world))
        {
            ThrowHelper.ThrowInvalidQuery(nameof(query));
        }

        _cases.Add(new ArchetypeForEachCase<TContext, TProcessor>(query.Cached, processor));
        _caseVersion++;
        return this;
    }

    /// <summary>Adds a query and processor with a component row bound once per matching archetype.</summary>
    public ArchetypeForEachOperation<TContext> Process<TComponent, TProcessor>(in Query query, ComponentId componentId, TProcessor processor)
        where TProcessor : struct, IArchetypeForEachComponent<TContext, TComponent>
    {
        if (_invoking)
        {
            ThrowHelper.ThrowCannotAddArchetypeProcessorWhileRunning();
        }

        if (!query.IsValid || !ReferenceEquals(query.Owner, _world))
        {
            ThrowHelper.ThrowInvalidQuery(nameof(query));
        }

        _world.ValidateGeneratedComponentType<TComponent>(componentId);
        if (!query.Cached.RequiresAllData(componentId))
        {
            ThrowHelper.ThrowArchetypeComponentMustBeRequired(componentId);
        }

        _cases.Add(new ArchetypeForEachComponentCase<TContext, TComponent, TProcessor>(query.Cached, componentId, processor));
        _caseVersion++;
        return this;
    }

    /// <summary>Runs every processor over the rows of its matching archetypes.</summary>
    public void Invoke() => Invoke(ref _context);

    /// <summary>Runs every matching query processor using caller-owned state.</summary>
    public void Invoke(ref TContext context)
    {
        if (_world.IsDisposed)
        {
            ThrowHelper.ThrowDisposedWorld();
        }

        _world.EnsureExecutionAccess();
        if (_invoking)
        {
            ThrowHelper.ThrowArchetypeOperationAlreadyRunning();
        }

        if (_cases.Count == 0)
        {
            return;
        }

        EnsurePlans();
        uint writeVersion = NextWriteVersion();
        TContext contextCopy = context;
        _invoking = true;
        _world.BeginQueryLease();
        try
        {
            Execute(ref contextCopy, writeVersion);
        }
        finally
        {
            _world.EndQueryLease();
            _invoking = false;
        }

        context = contextCopy;
    }

    private void EnsurePlans()
    {
        List<Archetype> archetypes = _world.Archetypes;
        if (_plannedCaseVersion == _caseVersion && _plans.Length == archetypes.Count)
        {
            return;
        }

        var plans = new ArchetypeForEachPlan[archetypes.Count];
        for (int archetypeIndex = 0; archetypeIndex < archetypes.Count; archetypeIndex++)
        {
            Archetype archetype = archetypes[archetypeIndex];
            int matchingCount = 0;
            for (int caseIndex = 0; caseIndex < _cases.Count; caseIndex++)
            {
                matchingCount += _cases[caseIndex].TryBindArchetype(archetype, out _) ? 1 : 0;
            }

            var matchingCases = new ArchetypeForEachBinding[matchingCount];
            int matchIndex = 0;
            for (int caseIndex = 0; caseIndex < _cases.Count; caseIndex++)
            {
                if (_cases[caseIndex].TryBindArchetype(archetype, out int componentIndex))
                {
                    matchingCases[matchIndex++] = new ArchetypeForEachBinding(
                        caseIndex,
                        componentIndex,
                        _cases[caseIndex].HasTagFilters);
                }
            }

            plans[archetypeIndex] = new ArchetypeForEachPlan(
                archetype,
                matchingCases,
                new uint[archetype.ComponentCount]);
        }

        _plans = plans;
        _plannedCaseVersion = _caseVersion;
    }

    private uint NextWriteVersion()
    {
        if (_writeVersion != uint.MaxValue)
        {
            return ++_writeVersion;
        }

        for (int planIndex = 0; planIndex < _plans.Length; planIndex++)
        {
            _plans.RefAt(planIndex).MarkedWriteVersions.AsSpan().Clear();
        }

        _writeVersion = 1;
        return _writeVersion;
    }

    private void Execute(ref TContext context, uint writeVersion)
    {
        for (int planIndex = 0; planIndex < _plans.Length; planIndex++)
        {
            ref readonly ArchetypeForEachPlan plan = ref _plans.RefAt(planIndex);
            if (plan.Archetype.ActiveChunkCount == 0)
            {
                continue;
            }

            MarkWrites(in plan, writeVersion);
            for (int caseIndex = 0; caseIndex < plan.Cases.Length; caseIndex++)
            {
                ref readonly ArchetypeForEachBinding binding = ref plan.Cases.RefAt(caseIndex);
                _cases[binding.CaseIndex].Execute(ref context, plan.Archetype, binding.ComponentIndex);
            }
        }
    }

    private void MarkWrites(in ArchetypeForEachPlan plan, uint writeVersion)
    {
        Stamp[] stamps = _world.GetArchetypeComponentStamps(plan.Archetype.Id);
        for (int bindingIndex = 0; bindingIndex < plan.Cases.Length; bindingIndex++)
        {
            ref readonly ArchetypeForEachBinding binding = ref plan.Cases.RefAt(bindingIndex);
            if (binding.ComponentIndex < 0
                || plan.MarkedWriteVersions.RefAt(binding.ComponentIndex) == writeVersion)
            {
                continue;
            }

            IArchetypeForEachCase<TContext> @case = _cases[binding.CaseIndex];
            if (binding.HasTagFilters && !@case.HasMatchingEntities(plan.Archetype))
            {
                continue;
            }

            plan.MarkedWriteVersions.RefAt(binding.ComponentIndex) = writeVersion;
            ref Stamp stamp = ref stamps.RefAt(binding.ComponentIndex);
            stamp = stamp.Next();
        }
    }
}

internal interface IArchetypeForEachCase<TContext>
{
    bool HasTagFilters { get; }
    bool TryBindArchetype(Archetype archetype, out int componentIndex);
    bool HasMatchingEntities(Archetype archetype);
    void Execute(ref TContext context, Archetype archetype, int componentIndex);
}

internal sealed class ArchetypeForEachCase<TContext, TProcessor> : IArchetypeForEachCase<TContext>
    where TProcessor : struct, IArchetypeForEachEntity<TContext>
{
    private readonly QueryPlan _query;
    private TProcessor _processor;

    internal ArchetypeForEachCase(QueryPlan query, TProcessor processor)
    {
        _query = query;
        _processor = processor;
    }

    public bool TryBindArchetype(Archetype archetype, out int componentIndex)
    {
        componentIndex = -1;
        return _query.MatchesArchetype(archetype);
    }

    public bool HasTagFilters => _query.HasTagFilters;

    public bool HasMatchingEntities(Archetype archetype) => _query.HasMatchingEntities(archetype);

    public void Execute(ref TContext context, Archetype archetype, int componentIndex)
    {
        GeneratedEntityRefView view = default;
        EntityRef entity = GeneratedForEachRuntime.CreateEntityRef(_query.Owner, ref view);
        for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
        {
            Chunk chunk = archetype.GetActiveChunk(chunkIndex);
            view.SetChunk(chunk);
            if (!_query.HasTagFilters || !_query.TryGetTagSlots(chunk, out ReadOnlySpan<int> tagSlots))
            {
                int count = chunk.Count;
                for (int slotIndex = 0; slotIndex < count; slotIndex++)
                {
                    view.SetSlot(slotIndex);
                    _processor.Invoke(ref context, entity);
                }

                continue;
            }

            for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)
            {
                view.SetSlot(tagSlots.RefAt(tagIndex));
                _processor.Invoke(ref context, entity);
            }
        }
    }
}

internal sealed class ArchetypeForEachComponentCase<TContext, TComponent, TProcessor> : IArchetypeForEachCase<TContext>
    where TProcessor : struct, IArchetypeForEachComponent<TContext, TComponent>
{
    private readonly QueryPlan _query;
    private readonly ComponentId _componentId;
    private TProcessor _processor;

    internal ArchetypeForEachComponentCase(QueryPlan query, ComponentId componentId, TProcessor processor)
    {
        _query = query;
        _componentId = componentId;
        _processor = processor;
    }

    public bool TryBindArchetype(Archetype archetype, out int componentIndex)
    {
        if (_query.MatchesArchetype(archetype))
        {
            return archetype.TryGetComponentIndex(_componentId, out componentIndex);
        }

        componentIndex = -1;
        return false;
    }

    public bool HasTagFilters => _query.HasTagFilters;

    public bool HasMatchingEntities(Archetype archetype) => _query.HasMatchingEntities(archetype);

    public void Execute(ref TContext context, Archetype archetype, int componentIndex)
    {
        GeneratedEntityRefView view = default;
        EntityRef entity = GeneratedForEachRuntime.CreateEntityRef(_query.Owner, ref view);
        for (int chunkIndex = 0; chunkIndex < archetype.ActiveChunkCount; chunkIndex++)
        {
            Chunk chunk = archetype.GetActiveChunk(chunkIndex);
            view.SetChunk(chunk);
            Span<TComponent> components = chunk.GetComponentRow<TComponent>(componentIndex);
            ref TComponent component = ref components.GetRefAtZero();
            int chunkCount = chunk.Count;
            if (_query.HasTagFilters && _query.TryGetTagSlots(chunk, out ReadOnlySpan<int> tagSlots))
            {
                int currentSlot = 0;
                for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)
                {
                    int selectedSlot = tagSlots.RefAt(tagIndex);
                    int slotAdvance = selectedSlot - currentSlot;
                    component = ref Unsafe.Add(ref component, slotAdvance);
                    view.SetSlot(selectedSlot);
                    _processor.Invoke(ref context, entity, ref component);
                    currentSlot = selectedSlot;
                }

                continue;
            }

            int denseSlotIndex = 0;
            while (denseSlotIndex < chunkCount)
            {
                view.SetSlot(denseSlotIndex);
                _processor.Invoke(ref context, entity, ref component);
                component = ref Unsafe.Add(ref component, 1);
                denseSlotIndex++;
            }
        }
    }
}

internal readonly struct ArchetypeForEachBinding(int caseIndex, int componentIndex, bool hasTagFilters)
{
    internal readonly int CaseIndex = caseIndex;
    internal readonly int ComponentIndex = componentIndex;
    internal readonly bool HasTagFilters = hasTagFilters;
}

internal readonly struct ArchetypeForEachPlan(
    Archetype archetype,
    ArchetypeForEachBinding[] cases,
    uint[] markedWriteVersions)
{
    internal readonly Archetype Archetype = archetype;
    internal readonly ArchetypeForEachBinding[] Cases = cases;
    internal readonly uint[] MarkedWriteVersions = markedWriteVersions;
}
