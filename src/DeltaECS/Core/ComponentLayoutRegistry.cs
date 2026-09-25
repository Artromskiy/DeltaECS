namespace Delta.ECS;

using System;
using System.Collections.Generic;

public sealed partial class ComponentLayoutRegistry
{
    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly Dictionary<Type, ComponentId> _primaryIdsByType = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();
    private readonly List<bool> _isTag = new();
    private readonly List<int> _tagIndices = new();
    private int _tagCount;

    internal int Count => _layouts.Count;

    internal int TagCount => _tagCount;

    private ComponentId Register(ComponentLayout layout, ComponentRowOperations rowOperations)
        => Register(layout, rowOperations, isTag: false);

    private ComponentId Register(ComponentLayout layout, ComponentRowOperations rowOperations, bool isTag)
    {
        if (_idsBySchema.TryGetValue(layout.SchemaId, out int existingId))
        {
            var existingLayout = _layouts[existingId];
            if (!existingLayout.Equals(layout) || _isTag[existingId] != isTag)
            {
                ThrowHelper.ThrowSchemaConflict(layout.SchemaId);
            }

            return new ComponentId(existingId);
        }

        var id = new ComponentId(_layouts.Count);
        _layouts.Add(layout);
        _rowOperations.Add(rowOperations);
        _isTag.Add(isTag);
        _tagIndices.Add(isTag ? _tagCount++ : -1);
        _idsBySchema.Add(layout.SchemaId, id.Value);
        _primaryIdsByType.TryAdd(layout.RuntimeType, id);

        return id;
    }

    internal bool IsTag(ComponentId id)
        => id.IsValid && (uint)id.Value < (uint)_isTag.Count && _isTag[id.Value];

    internal bool TryGetTagIndex(ComponentId id, out int tagIndex)
    {
        if (id.IsValid && (uint)id.Value < (uint)_tagIndices.Count && (tagIndex = _tagIndices[id.Value]) >= 0)
        {
            return true;
        }

        tagIndex = -1;
        return false;
    }

    internal int GetTagIndex(ComponentId id)
        => TryGetTagIndex(id, out int tagIndex) ? tagIndex : ThrowHelper.ThrowComponentIsNotTag(id);

    /// <summary>
    /// Tries to resolve the primary component registration for a CLR type.
    /// Later registrations of the same type remain addressable by their explicit ids.
    /// </summary>
    public bool TryGetPrimary(Type runtimeType, out ComponentId componentId)
    {
        ThrowHelper.ThrowIfNull(runtimeType, nameof(runtimeType));
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

    internal ComponentLayout Get(ComponentId id)
    {
        if (!id.IsValid || id.Value >= _layouts.Count)
        {
            return ThrowHelper.ThrowInvalidComponentLayoutId<ComponentLayout>();
        }

        return _layouts[id.Value];
    }

    internal bool TryGet(ComponentId id, out ComponentLayout layout)
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
