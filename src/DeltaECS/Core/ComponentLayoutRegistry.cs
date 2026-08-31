namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

public sealed partial class ComponentLayoutRegistry
{
    private static readonly MethodInfo _containsReferencesMethod = typeof(RuntimeHelpers).GetMethod(
        nameof(RuntimeHelpers.IsReferenceOrContainsReferences),
        BindingFlags.Public | BindingFlags.Static)
        ?? ThrowHelper.ThrowMissingRuntimeHelper();

    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly Dictionary<Type, ComponentId> _primaryIdsByType = new();
    private readonly Dictionary<Type, bool> _containsReferencesByType = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();

    public int Count => _layouts.Count;

    public ComponentId Register(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type runtimeType,
        SchemaId schemaId)
        => Register(
            runtimeType,
            schemaId,
            ContainsReferences(runtimeType));

    internal ComponentId Register(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type runtimeType,
        SchemaId schemaId,
        bool containsReferences)
        => Register(
            new ComponentLayout(schemaId, runtimeType),
            ComponentRowOperations.ForRuntimeType(containsReferences));

    public ComponentId Register(ComponentLayout layout)
        => Register(
            layout,
            ComponentRowOperations.ForRuntimeType(
                layout.RuntimeType is not { } runtimeType || ContainsReferences(runtimeType)));

    private bool ContainsReferences(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type runtimeType)
    {
        if (_containsReferencesByType.TryGetValue(runtimeType, out bool containsReferences))
        {
            return containsReferences;
        }

        containsReferences = _containsReferencesMethod.MakeGenericMethod(runtimeType).Invoke(null, null) is true;
        _containsReferencesByType.Add(runtimeType, containsReferences);
        return containsReferences;
    }

    private ComponentId Register(ComponentLayout layout, ComponentRowOperations rowOperations)
    {
        if (_idsBySchema.TryGetValue(layout.SchemaId, out int existingId))
        {
            var existingLayout = _layouts[existingId];
            if (!existingLayout.Equals(layout))
            {
                ThrowHelper.ThrowSchemaConflict(layout.SchemaId);
            }

            return new ComponentId(existingId);
        }

        var id = new ComponentId(_layouts.Count);
        _layouts.Add(layout);
        _rowOperations.Add(rowOperations);
        _idsBySchema.Add(layout.SchemaId, id.Value);
        if (layout.RuntimeType is { } runtimeType)
        {
            _primaryIdsByType.TryAdd(runtimeType, id);
        }

        return id;
    }

    public bool TryGetId(SchemaId schemaId, out ComponentId componentId)
    {
        if (_idsBySchema.TryGetValue(schemaId, out int id))
        {
            componentId = new ComponentId(id);
            return true;
        }

        componentId = ComponentId.Invalid;
        return false;
    }

    /// <summary>
    /// Tries to resolve the primary component registration for a CLR type.
    /// Later registrations of the same type remain addressable by their explicit ids.
    /// </summary>
    public bool TryGetPrimary(Type runtimeType, out ComponentId componentId)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        if (_primaryIdsByType.TryGetValue(runtimeType, out componentId))
        {
            return true;
        }

        componentId = ComponentId.Invalid;
        return false;
    }

    /// <summary>Gets the primary component registration for a CLR type.</summary>
    public ComponentId GetPrimary(Type runtimeType)
    {
        if (TryGetPrimary(runtimeType, out ComponentId componentId))
        {
            return componentId;
        }

        return ThrowHelper.ThrowComponentTypeNotRegistered(runtimeType);
    }

    public ComponentLayout Get(ComponentId id)
    {
        if (!id.IsValid || id.Value >= _layouts.Count)
        {
            return ThrowHelper.ThrowInvalidComponentLayoutId<ComponentLayout>();
        }

        return _layouts[id.Value];
    }

    public bool TryGet(ComponentId id, out ComponentLayout layout)
    {
        if (id.IsValid && id.Value < _layouts.Count)
        {
            layout = _layouts[id.Value];
            return true;
        }

        layout = default;
        return false;
    }

    internal ComponentRowOperations GetRowOperations(ComponentId id)
    {
        if (!id.IsValid || id.Value >= _rowOperations.Count)
        {
            return ThrowHelper.ThrowInvalidComponentLayoutId<ComponentRowOperations>();
        }

        return _rowOperations[id.Value];
    }
}
