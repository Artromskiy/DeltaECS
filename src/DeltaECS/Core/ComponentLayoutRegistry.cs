namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

public sealed partial class ComponentLayoutRegistry
{
    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly Dictionary<Type, ComponentId> _primaryIdsByType = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();
    private readonly List<bool> _isTag = new();
    private readonly List<int> _tagIndices = new();
    private readonly Dictionary<Type, List<GenericRegistration>> _genericRegistrations = new();
    private int _tagCount;

    private sealed class GenericRegistration(ComponentId[] arguments, ComponentId componentId)
    {
        internal readonly ComponentId[] Arguments = arguments;
        internal readonly ComponentId ComponentId = componentId;
    }

    internal int Count => _layouts.Count;

    internal int TagCount => _tagCount;

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArgument"/> and registers the resulting type.
    /// Uses the explicitly supplied schema identity for the closed type.
    /// </summary>
    public ComponentId Register(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type genericTypeDefinition,
        SchemaId schemaId,
        ComponentId componentArgument)
    {
        Span<ComponentId> arguments = stackalloc ComponentId[1] { componentArgument };
        return RegisterGeneric(genericTypeDefinition, schemaId, arguments);
    }

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArgument0"/> and <paramref name="componentArgument1"/>
    /// and registers the resulting type.
    /// </summary>
    public ComponentId Register(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type genericTypeDefinition,
        SchemaId schemaId,
        ComponentId componentArgument0,
        ComponentId componentArgument1)
    {
        Span<ComponentId> arguments = stackalloc ComponentId[2] { componentArgument0, componentArgument1 };
        return RegisterGeneric(genericTypeDefinition, schemaId, arguments);
    }

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArguments"/> and registers the resulting type.
    /// </summary>
    private ComponentId RegisterGeneric(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type genericTypeDefinition,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments)
    {
        Type componentType = CloseGenericType(genericTypeDefinition, componentArguments);
        EnsureGenericMappingAvailable(componentType, schemaId, componentArguments);
        ComponentId componentId = RegisterRuntimeType(componentType, schemaId);
        RegisterGenericMapping(componentType, componentId, componentArguments);
        return componentId;
    }

    /// <summary>Gets the CLR type of a registered component.</summary>
    public Type GetComponentType(ComponentId componentId) => Get(componentId).RuntimeType;

    internal Type CloseGenericType(Type genericTypeDefinition, ReadOnlySpan<ComponentId> componentArguments)
    {
        ThrowHelper.ThrowIfNull(genericTypeDefinition, nameof(genericTypeDefinition));
        if (!genericTypeDefinition.IsGenericTypeDefinition)
        {
            ThrowHelper.ThrowNotGenericTypeDefinition(genericTypeDefinition);
        }

        Type[] genericArgumentTypes = genericTypeDefinition.GetGenericArguments();
        if (genericArgumentTypes.Length != componentArguments.Length)
        {
            ThrowHelper.ThrowGenericTypeArgumentCountMismatch(genericTypeDefinition, genericArgumentTypes.Length, componentArguments.Length);
        }

        for (int index = 0; index < componentArguments.Length; index++)
        {
            genericArgumentTypes[index] = GetComponentType(componentArguments[index]);
        }

        Type componentType = genericTypeDefinition.MakeGenericType(genericArgumentTypes);
        if (componentType.IsByRefLike || componentType.ContainsGenericParameters)
        {
            ThrowHelper.ThrowInvalidComponentRuntimeType(componentType);
        }

        return componentType;
    }

    internal ComponentId GetGenericComponent(Type componentType, ReadOnlySpan<ComponentId> componentArguments)
    {
        if (_genericRegistrations.TryGetValue(componentType, out List<GenericRegistration>? registrations))
        {
            foreach (GenericRegistration registration in registrations)
            {
                if (componentArguments.SequenceEqual(registration.Arguments))
                {
                    return registration.ComponentId;
                }
            }
        }

        return ThrowHelper.ThrowComponentTypeNotRegistered(componentType);
    }

    private void EnsureGenericMappingAvailable(Type componentType, SchemaId schemaId, ReadOnlySpan<ComponentId> componentArguments)
    {
        if (_genericRegistrations.TryGetValue(componentType, out List<GenericRegistration>? registrations))
        {
            foreach (GenericRegistration registration in registrations)
            {
                if (componentArguments.SequenceEqual(registration.Arguments)
                    && _layouts[registration.ComponentId.Value].SchemaId != schemaId)
                {
                    ThrowHelper.ThrowGenericRegistrationConflict(componentType);
                }
            }
        }
    }

    private void RegisterGenericMapping(Type componentType, ComponentId componentId, ReadOnlySpan<ComponentId> componentArguments)
    {
        if (!_genericRegistrations.TryGetValue(componentType, out List<GenericRegistration>? registrations))
        {
            registrations = new List<GenericRegistration>();
            _genericRegistrations.Add(componentType, registrations);
        }

        foreach (GenericRegistration registration in registrations)
        {
            if (componentArguments.SequenceEqual(registration.Arguments))
            {
                if (registration.ComponentId != componentId)
                {
                    ThrowHelper.ThrowGenericRegistrationConflict(componentType);
                }

                return;
            }
        }

        registrations.Add(new GenericRegistration(componentArguments.ToArray(), componentId));
    }

    private ComponentId RegisterRuntimeType(Type componentType, SchemaId schemaId)
    {
        var layout = new ComponentLayout(schemaId, componentType);
        return IsTagType(componentType)
            ? Register(layout, default, isTag: true)
            : Register(layout, ComponentRowOperations.ForType(componentType));
    }

    internal static bool IsTagType(Type runtimeType)
        => runtimeType.IsValueType
            && !runtimeType.IsPrimitive
            && !runtimeType.IsEnum
            && runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;

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
