namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

/// <summary>Registers component CLR types and their stable schema identities.</summary>
public sealed partial class ComponentRegistry
{
    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly Dictionary<Type, ComponentId> _primaryIdsByType = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();
    private readonly List<IGeneratedComponentTypeToken> _componentTypeTokens = new();
    private readonly List<IComponentRegistrationToken> _componentVisitorTokens = new();
    private readonly Dictionary<(RuntimeTypeHandle ComponentType, RuntimeTypeHandle InterfaceType), IComponentInterfaceVisitorRoute> _interfaceVisitorRoutes = new();
    private readonly Dictionary<(RuntimeTypeHandle EnumType, ulong GroupId), ComponentGroupMembers> _componentGroups = new();
    private readonly Dictionary<string, ComponentId> _componentIdsByAlias = new(StringComparer.Ordinal);
    private int[] _tagIndices = [];
    private readonly Dictionary<Type, List<GenericRegistration>> _genericRegistrations = new();

    internal int Count => _layouts.Count;

    internal int TagCount { get; private set; }

    /// <summary>Visits a compatible registered component type and does nothing for an invalid ID or unsupported visitor.</summary>
    public void Visit(ComponentId componentId, IComponentVisitor? visitor) => _ = TryVisit(componentId, visitor);

    /// <summary>Visits a compatible component type; returns false for an invalid ID, null visitor, or unsupported route.</summary>
    public bool TryVisit(ComponentId componentId, IComponentVisitor? visitor)
    {
        if (visitor is null || !TryGet(componentId, out _))
        {
            return false;
        }

        if (visitor is not IConstrainedComponentVisitor constrainedVisitor)
        {
            return _componentVisitorTokens[componentId.Value].TryVisit(componentId, visitor);
        }

        RuntimeTypeHandle componentType = _componentTypeTokens[componentId.Value].ComponentType.TypeHandle;
        var key = (componentType, constrainedVisitor.ConstraintType);
        if (_interfaceVisitorRoutes.TryGetValue(key, out IComponentInterfaceVisitorRoute? route))
        {
            return route.TryVisit(componentId, visitor);
        }

        return _componentVisitorTokens[componentId.Value].TryVisit(componentId, visitor);
    }

    /// <summary>Registers one component from the generated component catalog.</summary>
    public ComponentId Register(IGeneratedComponentRegistration registration)
    {
        ThrowHelper.ThrowIfNull(registration, nameof(registration));
        if (registration.SchemaId.Value == 0)
        {
            ThrowHelper.ThrowGeneratedComponentSchemaIdZero(registration.ComponentType);
        }

        return registration.Register(this);
    }

    /// <summary>Gets the generated component IDs assigned to an enum group.</summary>
    /// <remarks>The returned span remains valid if the registry later registers more generated components.</remarks>
    public ReadOnlySpan<ComponentId> GetGroup<TEnum>(TEnum groupId) where TEnum : struct, Enum
        => _componentGroups.TryGetValue((typeof(TEnum).TypeHandle, GetGroupValue(groupId)), out ComponentGroupMembers? members)
            ? members.GetComponents()
            : [];

    /// <summary>Adds a registered component to a group in this catalog.</summary>
    public void AddGroup<TEnum>(TEnum groupId, ComponentId componentId) where TEnum : struct, Enum
    {
        _ = GetComponentType(componentId);
        RegisterGeneratedComponentGroup(typeof(TEnum).TypeHandle, GetGroupValue(groupId), componentId);
    }

    /// <summary>Adds an alias for a registered component in this catalog.</summary>
    public void AddAlias(string alias, ComponentId componentId)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            ThrowHelper.ThrowInvalidComponentAlias(nameof(alias));
        }

        _ = GetComponentType(componentId);
        if (_componentIdsByAlias.TryGetValue(alias, out ComponentId existing))
        {
            if (existing == componentId)
            {
                return;
            }

            ThrowHelper.ThrowComponentAliasConflict(alias, existing, componentId);
        }

        _componentIdsByAlias.Add(alias, componentId);
    }

    /// <summary>Tries to get the component registered under an alias in this catalog.</summary>
    public bool TryGetAlias(string alias, out ComponentId componentId)
    {
        if (!string.IsNullOrWhiteSpace(alias) && _componentIdsByAlias.TryGetValue(alias, out componentId))
        {
            return true;
        }

        componentId = ComponentId.Invalid;
        return false;
    }

    /// <summary>Gets the component registered under an alias in this catalog.</summary>
    public ComponentId GetAlias(string alias)
        => TryGetAlias(alias, out ComponentId componentId)
            ? componentId
            : ThrowHelper.ThrowComponentAliasNotFound(alias);

    /// <summary>Registers a generated component's enum group membership.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void RegisterGeneratedComponentGroup(RuntimeTypeHandle enumType, ulong groupId, ComponentId componentId)
    {
        var key = (enumType, groupId);
        if (!_componentGroups.TryGetValue(key, out ComponentGroupMembers? members))
        {
            members = new ComponentGroupMembers();
            _componentGroups.Add(key, members);
        }

        members.Add(componentId);
    }

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArgument"/> and registers the resulting type.
    /// Uses the explicitly supplied schema identity for the closed type.
    /// </summary>
    public ComponentId Register(Type genericTypeDefinition, ComponentId componentArgument, SchemaId schemaId)
    {
        Span<ComponentId> arguments = [componentArgument];
        return Register(genericTypeDefinition, arguments, schemaId);
    }

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArguments"/> and registers the resulting type.
    /// The span length must match the generic definition's arity.
    /// </summary>
    public ComponentId Register(
        Type genericTypeDefinition,
        ReadOnlySpan<ComponentId> componentArguments,
        SchemaId schemaId)
        => RegisterGeneric(genericTypeDefinition, schemaId, componentArguments);

    /// <summary>Gets the CLR type of a registered component.</summary>
    public Type GetComponentType(ComponentId componentId) => Get(componentId).RuntimeType;

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

    /// <summary>Tries to resolve a component registration from its stable schema identity.</summary>
    public bool TryGetComponentId(SchemaId schemaId, out ComponentId componentId)
    {
        if (_idsBySchema.TryGetValue(schemaId, out int value))
        {
            componentId = new ComponentId(value);
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

    internal static bool IsTagType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type runtimeType)
        => runtimeType.IsValueType
            && !runtimeType.IsPrimitive
            && !runtimeType.IsEnum
            && runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0
            && (runtimeType.StructLayoutAttribute?.Size ?? 0) <= 1;

    internal ComponentId RegisterGeneratedGenericDefinition(
        Type genericTypeDefinition,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments)
        => RegisterGeneric(genericTypeDefinition, schemaId, componentArguments);

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

    internal IGeneratedComponentTypeToken[] GetComponentTypeTokens(ReadOnlySpan<ComponentId> componentIds)
    {
        var tokens = new IGeneratedComponentTypeToken[componentIds.Length];
        for (int index = 0; index < componentIds.Length; index++)
        {
            ComponentId componentId = componentIds[index];
            _ = Get(componentId);
            tokens[index] = _componentTypeTokens[componentId.Value];
        }

        return tokens;
    }

    internal ComponentId RegisterGeneratedGenericComponent<T>(SchemaId schemaId, ReadOnlySpan<ComponentId> componentArguments, IGeneratedComponentTypeToken typeToken)
    {
        ThrowHelper.ThrowIfNull(typeToken, nameof(typeToken));
        if (typeToken.ComponentType != typeof(T))
        {
            ThrowHelper.ThrowGeneratedGenericComponentTypeMismatch(typeof(T), typeToken.ComponentType);
        }

        EnsureGenericMappingAvailable(typeof(T), schemaId, componentArguments);
        var layout = new ComponentLayout(schemaId, typeof(T));
        bool isTag = IsTagType(typeof(T));
        ComponentRegistrationTypeToken registrationToken = CreateComponentRegistrationTypeToken(typeToken);
        ComponentId componentId = isTag
            ? Register(layout, default, typeToken, registrationToken, isTag: true)
            : Register(layout, ComponentRowOperations.ForType<T>(), typeToken, registrationToken);
        RegisterGenericMapping(typeof(T), componentId, componentArguments);
        return componentId;
    }

    internal bool IsTag(ComponentId id) => TryGetTagIndex(id, out _);

    internal bool TryGetTagIndex(ComponentId id, out int tagIndex)
    {
        if ((uint)id.Value < (uint)_layouts.Count && (tagIndex = _tagIndices[id.Value]) >= 0)
        {
            return true;
        }

        tagIndex = -1;
        return false;
    }

    internal int GetTagIndex(ComponentId id)
        => TryGetTagIndex(id, out int tagIndex) ? tagIndex : ThrowHelper.ThrowComponentIsNotTag(id);

    internal ComponentLayout Get(ComponentId id)
    {
        if (!id.IsValid || id.Value >= _layouts.Count)
        {
            return ThrowHelper.ThrowInvalidComponentLayoutId<ComponentLayout>();
        }

        return _layouts[id.Value];
    }

    internal IGeneratedComponentTypeToken GetComponentTypeToken(ComponentId id)
    {
        _ = Get(id);
        return _componentTypeTokens[id.Value];
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

    private static ulong GetGroupValue<TEnum>(TEnum groupId) where TEnum : struct, Enum
    {
        object boxedValue = groupId;
        IFormatProvider invariant = CultureInfo.InvariantCulture;
        return Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum))) switch
        {
            TypeCode.SByte => (byte)Convert.ToSByte(boxedValue, invariant),
            TypeCode.Byte => Convert.ToByte(boxedValue, invariant),
            TypeCode.Int16 => (ushort)Convert.ToInt16(boxedValue, invariant),
            TypeCode.UInt16 => Convert.ToUInt16(boxedValue, invariant),
            TypeCode.Int32 => (uint)Convert.ToInt32(boxedValue, invariant),
            TypeCode.UInt32 => Convert.ToUInt32(boxedValue, invariant),
            TypeCode.Int64 => (ulong)Convert.ToInt64(boxedValue, invariant),
            TypeCode.UInt64 => Convert.ToUInt64(boxedValue, invariant),
            _ => ThrowHelper.ThrowUnsupportedEnumUnderlyingType(typeof(TEnum)),
        };
    }

    private static ComponentRegistrationTypeToken CreateComponentRegistrationTypeToken(IGeneratedComponentTypeToken typeToken)
    {
        var visitor = new GeneratedComponentTypeRegistrationTokenFactory();
        typeToken.DispatchRegistrationRoute(ref visitor);
        return visitor.RegistrationToken;
    }

    /// <summary>
    /// Closes a generic component definition with the CLR types selected by
    /// <paramref name="componentArguments"/> and registers the resulting type.
    /// </summary>
    private ComponentId RegisterGeneric(
        Type genericTypeDefinition,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments)
    {
        ThrowHelper.ThrowIfNull(genericTypeDefinition, nameof(genericTypeDefinition));
        if (!genericTypeDefinition.IsGenericTypeDefinition)
        {
            ThrowHelper.ThrowNotGenericTypeDefinition(genericTypeDefinition);
        }

        if (GeneratedGenericBindingRegistry.TryGetComponentDispatcher(
            genericTypeDefinition,
            out int dispatcherArity,
            out GeneratedGenericComponentDispatcher dispatcher))
        {
            if (dispatcherArity != componentArguments.Length)
            {
                ThrowHelper.ThrowGenericTypeArgumentCountMismatch(genericTypeDefinition, dispatcherArity, componentArguments.Length);
            }

            IGeneratedComponentTypeToken[] typeTokens = GetComponentTypeTokens(componentArguments);
            return dispatcher(this, schemaId, componentArguments, typeTokens);
        }

        return ThrowHelper.ThrowMissingGeneratedGenericComponent(genericTypeDefinition);
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

    private ComponentId Register(
        ComponentLayout layout,
        ComponentRowOperations rowOperations,
        IGeneratedComponentTypeToken typeToken,
        IComponentRegistrationToken componentRegistrationToken,
        bool isTag = false)
    {
        if (_idsBySchema.TryGetValue(layout.SchemaId, out int existingId))
        {
            var existingLayout = _layouts[existingId];
            if (!existingLayout.Equals(layout) || (_tagIndices[existingId] >= 0) != isTag)
            {
                ThrowHelper.ThrowSchemaConflict(layout.SchemaId);
            }

            return new ComponentId(existingId);
        }

        var id = new ComponentId(_layouts.Count);
        EnsureTagIndexCapacity(_layouts.Count + 1);
        _tagIndices[id.Value] = isTag ? TagCount : -1;
        if (isTag)
        {
            TagCount++;
        }

        _layouts.Add(layout);
        _rowOperations.Add(rowOperations);
        _componentTypeTokens.Add(typeToken);
        _componentVisitorTokens.Add(componentRegistrationToken);
        _idsBySchema.Add(layout.SchemaId, id.Value);
        _primaryIdsByType.TryAdd(layout.RuntimeType, id);

        return id;
    }

    private void EnsureTagIndexCapacity(int required)
    {
        if (required <= _tagIndices.Length)
        {
            return;
        }

        int capacity = Math.Max(required, _tagIndices.Length == 0 ? 4 : _tagIndices.Length * 2);
        Array.Resize(ref _tagIndices, capacity);
    }

    private sealed class ComponentGroupMembers
    {
        private readonly List<ComponentId> _componentIds = new();
        private ComponentId[]? _cached;

        internal void Add(ComponentId componentId)
        {
            if (_componentIds.Contains(componentId))
            {
                return;
            }

            _componentIds.Add(componentId);
            _cached = null;
        }

        internal ReadOnlySpan<ComponentId> GetComponents() => _cached ??= _componentIds.ToArray();
    }

    private struct GeneratedComponentTypeRegistrationTokenFactory :
        IGeneratedComponentRegistrationRouteVisitor
    {
        private ComponentRegistrationTypeToken? _registrationToken;

        internal readonly ComponentRegistrationTypeToken RegistrationToken => _registrationToken
            ?? ThrowHelper.ThrowGeneratedComponentVisitorTokenMissing();

        void IGeneratedComponentRegistrationRouteVisitor.VisitUnconstrained<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new UnconstrainedComponentTypeRegistrationRoute<T>());

        void IGeneratedComponentRegistrationRouteVisitor.VisitStruct<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new StructComponentTypeRegistrationRoute<T>());

        void IGeneratedComponentRegistrationRouteVisitor.VisitUnmanaged<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new UnmanagedComponentTypeRegistrationRoute<T>());

        void IGeneratedComponentRegistrationRouteVisitor.VisitClass<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new ClassComponentTypeRegistrationRoute<T>());

        void IGeneratedComponentRegistrationRouteVisitor.VisitConstructible<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new NewComponentTypeRegistrationRoute<T>());

        void IGeneratedComponentRegistrationRouteVisitor.VisitClassConstructible<T>()
            => _registrationToken = new ComponentRegistrationTypeToken(new ClassNewComponentTypeRegistrationRoute<T>());
    }

    private sealed class GenericRegistration(ComponentId[] arguments, ComponentId componentId)
    {
        internal readonly ComponentId[] Arguments = arguments;
        internal readonly ComponentId ComponentId = componentId;
    }
}
