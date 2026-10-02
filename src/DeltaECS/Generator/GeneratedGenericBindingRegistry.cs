namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>Creates a closed generic component using generated type-token dispatch.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ComponentId GeneratedGenericComponentDispatcher(
    ComponentLayoutRegistry layouts,
    SchemaId schemaId,
    ReadOnlySpan<ComponentId> componentArguments,
    ReadOnlySpan<IGeneratedComponentTypeToken> typeArguments);

/// <summary>Creates a closed generic functor using generated type-token dispatch.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate IGeneratedGenericFunctor GeneratedGenericFunctorDispatcher(
    ReadOnlySpan<IGeneratedComponentTypeToken> typeArguments);

/// <summary>Receives generated generic dispatchers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedGenericBindingRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, ComponentDispatcherEntry> ComponentDispatchers = new();
    private static readonly Dictionary<Type, FunctorDispatcherEntry> FunctorDispatchers = new();

    private sealed class ComponentDispatcherEntry(int arity, GeneratedGenericComponentDispatcher dispatcher)
    {
        internal readonly int Arity = arity;
        internal readonly GeneratedGenericComponentDispatcher Dispatcher = dispatcher;
    }

    private sealed class FunctorDispatcherEntry(int arity, GeneratedGenericFunctorDispatcher dispatcher)
    {
        internal readonly int Arity = arity;
        internal readonly GeneratedGenericFunctorDispatcher Dispatcher = dispatcher;
    }

    /// <summary>Registers a generated dispatcher for a generic component definition.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterComponentDispatcher(
        Type genericDefinition,
        int arity,
        GeneratedGenericComponentDispatcher dispatcher)
    {
        ThrowHelper.ThrowIfNull(genericDefinition, nameof(genericDefinition));
        ThrowHelper.ThrowIfNull(dispatcher, nameof(dispatcher));
        lock (Gate)
        {
            if (ComponentDispatchers.TryGetValue(genericDefinition, out ComponentDispatcherEntry? existing))
            {
                if (existing.Arity != arity)
                {
                    ThrowHelper.ThrowGeneratedDispatcherConflict(genericDefinition);
                }

                return;
            }

            ComponentDispatchers.Add(genericDefinition, new ComponentDispatcherEntry(arity, dispatcher));
        }
    }

    /// <summary>Registers a generated dispatcher for a generic functor definition.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterFunctorDispatcher(
        Type genericDefinition,
        int arity,
        GeneratedGenericFunctorDispatcher dispatcher)
    {
        ThrowHelper.ThrowIfNull(genericDefinition, nameof(genericDefinition));
        ThrowHelper.ThrowIfNull(dispatcher, nameof(dispatcher));
        lock (Gate)
        {
            if (FunctorDispatchers.TryGetValue(genericDefinition, out FunctorDispatcherEntry? existing))
            {
                if (existing.Arity != arity)
                {
                    ThrowHelper.ThrowGeneratedDispatcherConflict(genericDefinition);
                }

                return;
            }

            FunctorDispatchers.Add(genericDefinition, new FunctorDispatcherEntry(arity, dispatcher));
        }
    }

    /// <summary>Finds a generated component dispatcher for an open generic definition.</summary>
    internal static bool TryGetComponentDispatcher(
        Type genericDefinition,
        out int arity,
        out GeneratedGenericComponentDispatcher dispatcher)
    {
        lock (Gate)
        {
            if (ComponentDispatchers.TryGetValue(genericDefinition, out ComponentDispatcherEntry? entry))
            {
                arity = entry.Arity;
                dispatcher = entry.Dispatcher;
                return true;
            }
        }

        arity = 0;
        dispatcher = null!;
        return false;
    }

    /// <summary>Finds a generated functor dispatcher for an open generic definition.</summary>
    internal static bool TryGetFunctorDispatcher(
        Type genericDefinition,
        out int arity,
        out GeneratedGenericFunctorDispatcher dispatcher)
    {
        lock (Gate)
        {
            if (FunctorDispatchers.TryGetValue(genericDefinition, out FunctorDispatcherEntry? entry))
            {
                arity = entry.Arity;
                dispatcher = entry.Dispatcher;
                return true;
            }
        }

        arity = 0;
        dispatcher = null!;
        return false;
    }

    /// <summary>Registers a closed generic component from generated code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentId RegisterComponent<T>(ComponentLayoutRegistry layouts, SchemaId schemaId, ReadOnlySpan<ComponentId> componentArguments)
    {
        ThrowHelper.ThrowIfNull(layouts, nameof(layouts));
        return layouts.RegisterGeneratedGenericComponent<T>(schemaId, componentArguments, GeneratedComponentTypeTokenRegistry.Get<T>());
    }

    /// <summary>Registers a closed generic component with its generated constraint-capability token.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentId RegisterComponent<T>(
        ComponentLayoutRegistry layouts,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments,
        IGeneratedComponentTypeToken typeToken)
    {
        ThrowHelper.ThrowIfNull(layouts, nameof(layouts));
        ThrowHelper.ThrowIfNull(typeToken, nameof(typeToken));
        return layouts.RegisterGeneratedGenericComponent<T>(schemaId, componentArguments, typeToken);
    }

    /// <summary>Registers an open generic component from generated code using positional component IDs.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ComponentId RegisterComponentDefinition(
        ComponentLayoutRegistry layouts,
        Type genericDefinition,
        SchemaId schemaId,
        ReadOnlySpan<ComponentId> componentArguments)
    {
        ThrowHelper.ThrowIfNull(layouts, nameof(layouts));
        ThrowHelper.ThrowIfNull(genericDefinition, nameof(genericDefinition));
        return layouts.RegisterGeneratedGenericDefinition(genericDefinition, schemaId, componentArguments);
    }

}
