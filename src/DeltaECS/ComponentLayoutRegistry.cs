namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class ComponentLayoutRegistry
{
    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();

    public int Count => _layouts.Count;

    public ComponentId Register(ComponentLayout layout) => Register(layout, ComponentRowOperations.Fallback);

    private ComponentId Register(ComponentLayout layout, ComponentRowOperations rowOperations)
    {
        if (_idsBySchema.TryGetValue(layout.SchemaId, out int existingId))
        {
            var existingLayout = _layouts[existingId];
            if (!existingLayout.Equals(layout))
            {
                throw new InvalidOperationException($"SchemaId {layout.SchemaId} is already registered with a different component layout.");
            }

            return new ComponentId(existingId);
        }

        if (_layouts.Count >= ComponentMask.Capacity)
        {
            throw new InvalidOperationException($"The type-erased mask supports at most {ComponentMask.Capacity} components.");
        }

        var id = new ComponentId(_layouts.Count);
        _layouts.Add(layout);
        _rowOperations.Add(rowOperations);
        _idsBySchema.Add(layout.SchemaId, id.Value);

        return id;
    }

    public ComponentId Register<T>(SchemaId schemaId, ComponentStorageClass storageClass = ComponentStorageClass.Dense) => Register(new ComponentLayout(schemaId, typeof(T), storageClass), ComponentRowOperations.For<T>());

    public ComponentId RegisterUnmanaged<T>(SchemaId schemaId, ComponentStorageClass storageClass = ComponentStorageClass.Dense)
        where T : unmanaged
    {
        return Register(
            new ComponentLayout(schemaId, typeof(T), storageClass, Unsafe.SizeOf<T>()),
            ComponentRowOperations.For<T>());
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

    public ComponentLayout Get(ComponentId id)
    {
        if (!id.IsValid || id.Value >= _layouts.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
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
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return _rowOperations[id.Value];
    }
}
