namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>Creates one closed generic component registration using generated typed code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate ComponentId GeneratedGenericComponentRegistrationFactory(ComponentLayoutRegistry layouts, SchemaId schemaId);

/// <summary>Creates one closed generated executor for a runtime-selected generic functor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate IGeneratedGenericFunctor GeneratedGenericFunctorFactory();

/// <summary>Receives closed generic bindings emitted into the consuming assembly.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedGenericBindingRegistry
{
    private static readonly object Gate = new();
    private static readonly List<ComponentFactoryEntry> ComponentFactories = new();
    private static readonly List<FunctorFactoryEntry> FunctorFactories = new();

    private sealed class ComponentFactoryEntry(Type genericDefinition, Type[] arguments, Type componentType, GeneratedGenericComponentRegistrationFactory factory)
    {
        internal readonly Type GenericDefinition = genericDefinition;
        internal readonly Type[] Arguments = arguments;
        internal readonly Type ComponentType = componentType;
        internal readonly GeneratedGenericComponentRegistrationFactory Factory = factory;
    }

    private sealed class FunctorFactoryEntry(Type genericDefinition, Type[] arguments, GeneratedGenericFunctorFactory factory)
    {
        internal readonly Type GenericDefinition = genericDefinition;
        internal readonly Type[] Arguments = arguments;
        internal readonly GeneratedGenericFunctorFactory Factory = factory;
    }

    /// <summary>Registers a direct typed factory for one closed generic component.</summary>
    public static void RegisterComponentFactory(
        Type genericDefinition,
        Type[] genericArguments,
        Type componentType,
        GeneratedGenericComponentRegistrationFactory factory)
    {
        ThrowHelper.ThrowIfNull(genericDefinition, nameof(genericDefinition));
        ThrowHelper.ThrowIfNull(genericArguments, nameof(genericArguments));
        ThrowHelper.ThrowIfNull(componentType, nameof(componentType));
        ThrowHelper.ThrowIfNull(factory, nameof(factory));
        Type[] arguments = (Type[])genericArguments.Clone();

        lock (Gate)
        {
            foreach (ComponentFactoryEntry entry in ComponentFactories)
            {
                if (entry.GenericDefinition == genericDefinition && TypesEqual(entry.Arguments, arguments))
                {
                    if (entry.ComponentType != componentType)
                    {
                        throw new InvalidOperationException($"Conflicting generated factories were registered for {genericDefinition}.");
                    }

                    return;
                }
            }

            ComponentFactories.Add(new ComponentFactoryEntry(genericDefinition, arguments, componentType, factory));
        }
    }

    /// <summary>Registers a direct typed factory for one closed generic functor executor.</summary>
    public static void RegisterFunctorFactory(
        Type genericDefinition,
        Type[] genericArguments,
        GeneratedGenericFunctorFactory factory)
    {
        ThrowHelper.ThrowIfNull(genericDefinition, nameof(genericDefinition));
        ThrowHelper.ThrowIfNull(genericArguments, nameof(genericArguments));
        ThrowHelper.ThrowIfNull(factory, nameof(factory));
        Type[] arguments = (Type[])genericArguments.Clone();

        lock (Gate)
        {
            foreach (FunctorFactoryEntry entry in FunctorFactories)
            {
                if (entry.GenericDefinition == genericDefinition && TypesEqual(entry.Arguments, arguments))
                {
                    return;
                }
            }

            FunctorFactories.Add(new FunctorFactoryEntry(genericDefinition, arguments, factory));
        }
    }

    internal static bool TryGetComponentFactory(
        Type genericDefinition,
        ReadOnlySpan<Type> genericArguments,
        out Type componentType,
        out GeneratedGenericComponentRegistrationFactory factory)
    {
        lock (Gate)
        {
            foreach (ComponentFactoryEntry entry in ComponentFactories)
            {
                if (entry.GenericDefinition == genericDefinition && TypesEqual(entry.Arguments, genericArguments))
                {
                    componentType = entry.ComponentType;
                    factory = entry.Factory;
                    return true;
                }
            }
        }

        componentType = null!;
        factory = null!;
        return false;
    }

    internal static bool TryGetFunctorFactory(
        Type genericDefinition,
        ReadOnlySpan<Type> genericArguments,
        out GeneratedGenericFunctorFactory factory)
    {
        lock (Gate)
        {
            foreach (FunctorFactoryEntry entry in FunctorFactories)
            {
                if (entry.GenericDefinition == genericDefinition && TypesEqual(entry.Arguments, genericArguments))
                {
                    factory = entry.Factory;
                    return true;
                }
            }
        }

        factory = null!;
        return false;
    }

    private static bool TypesEqual(Type[] registered, ReadOnlySpan<Type> requested)
    {
        if (registered.Length != requested.Length)
        {
            return false;
        }

        for (int index = 0; index < registered.Length; index++)
        {
            if (registered[index] != requested[index])
            {
                return false;
            }
        }

        return true;
    }
}
