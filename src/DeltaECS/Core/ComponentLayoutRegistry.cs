namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>Registers component CLR types and their stable schema identities.</summary>
public sealed partial class ComponentLayoutRegistry
{
    private readonly Dictionary<SchemaId, int> _idsBySchema = new();
    private readonly Dictionary<Type, ComponentId> _primaryIdsByType = new();
    private readonly List<ComponentLayout> _layouts = new();
    private readonly List<ComponentRowOperations> _rowOperations = new();
    private readonly List<IGeneratedComponentTypeToken> _componentTypeTokens = new();
    private readonly List<IComponentRegistrationToken> _componentVisitorTokens = new();
    private readonly Dictionary<(RuntimeTypeHandle ComponentType, RuntimeTypeHandle InterfaceType), IComponentInterfaceVisitorRoute> _interfaceVisitorRoutes = new();
    private readonly List<bool> _isTag = new();
    private readonly List<int> _tagIndices = new();
    private readonly Dictionary<Type, List<GenericRegistration>> _genericRegistrations = new();
    private int _tagCount;

    /// <summary>Visits a registered component type using the visitor's registered constraint route.</summary>
    public void Visit(ComponentId componentId, IComponentTypeVisitor visitor)
    {
        ThrowHelper.ThrowIfNull(visitor, nameof(visitor));
        IComponentRegistrationToken registrationToken = GetComponentVisitorToken(componentId);
        if (visitor is IComponentTypeVisitorConstraint constrainedVisitor)
        {
            RuntimeTypeHandle componentType = GetComponentTypeToken(componentId).ComponentType.TypeHandle;
            var key = (componentType, constrainedVisitor.ConstraintType);
            if (_interfaceVisitorRoutes.TryGetValue(key, out IComponentInterfaceVisitorRoute? route))
            {
                route.Visit(componentId, visitor);
                return;
            }
        }

        registrationToken.Visit(componentId, visitor);
    }

    /// <summary>Registers one component from the generated component catalog.</summary>
    public ComponentId Register(IGeneratedComponentRegistration registration)
    {
        ThrowHelper.ThrowIfNull(registration, nameof(registration));
        if (registration.SchemaId.Value == 0)
        {
            ThrowHelper.ThrowGeneratedComponentSchemaIdZero(registration.ComponentType);
        }

        IGeneratedComponentTypeToken typeToken = GeneratedComponentTypeTokenRegistry.Get(registration.ComponentType);
        ComponentRegistrationTypeToken registrationToken = CreateComponentRegistrationTypeToken(typeToken);
        var visitor = new GeneratedComponentRegistrationVisitor(
            this,
            registration.SchemaId,
            registration.IsTag,
            typeToken,
            registrationToken);
        typeToken.Dispatch(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        return visitor.ComponentId;
    }

    private static ComponentRegistrationTypeToken CreateComponentRegistrationTypeToken(IGeneratedComponentTypeToken typeToken)
    {
        var visitor = new GeneratedComponentTypeRegistrationTokenFactory();

        if (typeToken is IGeneratedClassConstructibleComponentTypeToken classNewToken)
        {
            classNewToken.DispatchClassConstructible(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }
        else if (typeToken is IGeneratedUnmanagedComponentTypeToken unmanagedToken)
        {
            unmanagedToken.DispatchUnmanaged(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }
        else if (typeToken is IGeneratedStructComponentTypeToken structToken)
        {
            structToken.DispatchStruct(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }
        else if (typeToken is IGeneratedConstructibleComponentTypeToken newToken)
        {
            newToken.DispatchConstructible(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }
        else if (typeToken is IGeneratedClassComponentTypeToken classToken)
        {
            classToken.DispatchClass(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }
        else
        {
            typeToken.Dispatch(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref visitor);
        }

        return visitor.RegistrationToken;
    }

    private struct GeneratedComponentTypeRegistrationTokenFactory :
        IGeneratedComponentTypeVisitor,
        IGeneratedClassConstructibleComponentTypeVisitor,
        IGeneratedUnmanagedComponentTypeVisitor,
        IGeneratedStructComponentTypeVisitor,
        IGeneratedConstructibleComponentTypeVisitor,
        IGeneratedClassComponentTypeVisitor
    {
        private ComponentRegistrationTypeToken? _registrationToken;

        internal readonly ComponentRegistrationTypeToken RegistrationToken => _registrationToken
            ?? ThrowHelper.ThrowGeneratedComponentVisitorTokenMissing();

        void IGeneratedComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new UnconstrainedComponentTypeRegistrationRoute<T>());

        void IGeneratedClassConstructibleComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new ClassNewComponentTypeRegistrationRoute<T>());

        void IGeneratedUnmanagedComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new UnmanagedComponentTypeRegistrationRoute<T>());

        void IGeneratedStructComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new StructComponentTypeRegistrationRoute<T>());

        void IGeneratedConstructibleComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new NewComponentTypeRegistrationRoute<T>());

        void IGeneratedClassComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _registrationToken = new ComponentRegistrationTypeToken(new ClassComponentTypeRegistrationRoute<T>());
    }

    private struct GeneratedComponentRegistrationVisitor(
        ComponentLayoutRegistry layouts,
        SchemaId schemaId,
        bool isTag,
        IGeneratedComponentTypeToken typeToken,
        ComponentRegistrationTypeToken registrationToken) : IGeneratedComponentTypeVisitor
    {
        private ComponentId _componentId;

        internal readonly ComponentId ComponentId => _componentId;

        void IGeneratedComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => _componentId = layouts.RegisterGeneratedComponent<T>(schemaId, typeToken, isTag, registrationToken);
    }

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
        Type genericTypeDefinition,
        ComponentId componentArgument,
        SchemaId schemaId)
    {
        Span<ComponentId> arguments = stackalloc ComponentId[1] { componentArgument };
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

    internal ComponentId RegisterGeneratedGenericDefinition(
        Type genericTypeDefinition,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments)
        => RegisterGeneric(genericTypeDefinition, schemaId, componentArguments);

    /// <summary>Gets the CLR type of a registered component.</summary>
    public Type GetComponentType(ComponentId componentId) => Get(componentId).RuntimeType;

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

    internal static bool IsTagType(Type runtimeType)
        => runtimeType.IsValueType
            && !runtimeType.IsPrimitive
            && !runtimeType.IsEnum
            && runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0
            && (runtimeType.StructLayoutAttribute?.Size ?? 0) <= 1;

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
            if (!existingLayout.Equals(layout) || _isTag[existingId] != isTag)
            {
                ThrowHelper.ThrowSchemaConflict(layout.SchemaId);
            }

            return new ComponentId(existingId);
        }

        var id = new ComponentId(_layouts.Count);
        _layouts.Add(layout);
        _rowOperations.Add(rowOperations);
        _componentTypeTokens.Add(typeToken);
        _componentVisitorTokens.Add(componentRegistrationToken);
        _isTag.Add(isTag);
        _tagIndices.Add(isTag ? _tagCount++ : -1);
        _idsBySchema.Add(layout.SchemaId, id.Value);
        _primaryIdsByType.TryAdd(layout.RuntimeType, id);

        return id;
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

    internal IGeneratedComponentTypeToken GetComponentTypeToken(ComponentId id)
    {
        _ = Get(id);
        return _componentTypeTokens[id.Value];
    }

    internal IComponentRegistrationToken GetComponentVisitorToken(ComponentId id)
    {
        _ = Get(id);
        return _componentVisitorTokens[id.Value];
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
