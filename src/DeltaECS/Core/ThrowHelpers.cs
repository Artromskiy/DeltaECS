namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int ThrowInvalidReadRoute(ComponentId component)
        => throw new ArgumentException(
            "A row access must target a registered component guaranteed by the query All mask.",
            nameof(component));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int ThrowMissingPrimaryRoute(Type runtimeType)
        => throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ReadAccess ThrowMissingPrimaryReadAccess(Type runtimeType)
        => throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static WriteAccess ThrowMissingPrimaryWriteAccess(Type runtimeType)
        => throw new ArgumentException(
            $"The primary component for {runtimeType} is not guaranteed by the query All mask.",
            nameof(runtimeType));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowComponentTypeMismatch(ComponentId component, Type runtimeType)
        => throw new ArgumentException(
            $"Component {component} is not registered as {runtimeType}.",
            nameof(component));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowUnregisteredQueryComponent(ComponentId component, QuerySpec spec)
        => throw new ArgumentException(
            $"Query component {component} is not registered in the query's world.",
            nameof(spec));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowPlanActivationOutOfSync()
        => throw new InvalidOperationException("Chunk plan activation order is out of sync with its archetype.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowPlanDeactivationOutOfSync()
        => throw new InvalidOperationException("Chunk plan deactivation order is out of sync with its archetype.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessMismatch()
        => throw new InvalidOperationException("The row access does not belong to this query or world.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessTypeMismatch()
        => throw new InvalidOperationException("The row access type does not match the registered component type.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowMissingWriteIntent()
        => throw new InvalidOperationException("The query did not register its write row access.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDisposedQueryExecution()
        => throw new InvalidOperationException("The query execution has ended.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowAccessModeMismatch()
        => throw new InvalidOperationException("The access mode does not match the requested row operation.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArchetypeIteratorNotPositioned()
        => throw new InvalidOperationException("The archetype iterator is not positioned on an archetype.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkIteratorNotPositioned()
        => throw new InvalidOperationException("The chunk iterator is not positioned on a chunk.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSlotIteratorNotPositioned()
        => throw new InvalidOperationException("The slot iterator is not positioned on a slot.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowIntegrationAlreadyInitialized()
        => throw new InvalidOperationException("The ECS world can be initialized exactly once.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowIntegrationComponentNotRegistered(int component, string parameterName)
        => throw new ArgumentException($"Component {component} is not registered in this world.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowIntegrationRawComponentUnsupported(int component)
        => throw new NotSupportedException($"Component {component} has a raw layout that the typed-array world cannot materialize.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowIntegrationNotInitialized()
        => throw new InvalidOperationException("The ECS integration world is not initialized or has already shut down.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T ThrowMissingComponent<T>(Entity entity, ComponentId componentId)
        => throw new InvalidOperationException(
            $"Entity {entity} does not contain a component of type {typeof(T)} for {componentId}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowComponentNotRegistered(ComponentId componentId)
        => throw new ArgumentException(
            $"Component {componentId} is not registered in this world.",
            nameof(componentId));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowGenericComponentTypeMismatch<T>(ComponentId componentId, Type registeredType)
        => throw new ArgumentException(
            $"Component {componentId} is registered as {registeredType}, not {typeof(T)}.",
            nameof(componentId));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowStructuralCreateFailed()
        => throw new InvalidOperationException("The structural operation did not produce a live entity.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowStructuralComponentMissing()
        => throw new InvalidOperationException("The structural operation did not produce the requested component row.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowStampRange(string parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowGeneratedQueryInvalid(string parameterName)
        => throw new ArgumentException("Query handle does not belong to this world.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkRowOperationsMismatch(string parameterName)
        => throw new ArgumentException("Each component row must have cached operations.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArrayRowsRequiresRuntimeType()
        => throw new InvalidOperationException("ArrayRows requires a type-backed component layout. Register the component with its runtime Type.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkFull()
        => throw new InvalidOperationException("Chunk is full.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkCountOutOfRange(string parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowChunkSlotOutOfRange(string parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidComponentList()
        => throw new InvalidOperationException("Component list is empty or invalid.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArchetypeHandleInvalid(string parameterName)
        => throw new ArgumentException("Archetype handle does not belong to this world.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowWorldDestinationOutOfRange(string parameterName)
        => throw new ArgumentOutOfRangeException(parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowMissingComponentLayout(int component)
        => throw new InvalidOperationException($"Missing component layout for {component}.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowWorldComponentNotRegistered(int component, string parameterName)
        => throw new ArgumentException(
            $"Component {component} is not registered in this world.",
            parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowStructuralChangeWhileLeased(string operation)
        => throw new InvalidOperationException($"Cannot {operation} while chunk leases are active.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidQuery(string parameterName)
        => throw new ArgumentException("Query handle does not belong to this world.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowArchetypeLayoutMismatch()
        => throw new ArgumentException("Archetype must have matching component and layout arrays.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidEntityQueryHandle()
        => throw new InvalidOperationException("Cannot bind a row from an invalid query handle.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Type ThrowNonArrayComponentRow(string parameterName)
        => throw new ArgumentException("The component row must be a CLR array.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowExactComponentValueType(Type elementType, string parameterName)
        => throw new ArgumentException($"Component value must be exactly {elementType}.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowComponentValueTypeMismatch(Type valueType, Type registeredType, string parameterName)
        => throw new ArgumentException(
            $"Component value type {valueType} does not match registered type {registeredType}.",
            parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowComponentDestinationTooSmall(string parameterName)
        => throw new ArgumentException("Destination is too small.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int ThrowComponentIdOutOfRange()
        => throw new ArgumentOutOfRangeException("componentId", "ComponentId must be a non-negative value.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidQueryScopeHandle(string parameterName)
        => throw new ArgumentException("Query handle does not belong to this world.", parameterName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDisposedQueryScope()
        => throw new InvalidOperationException("The query scope has been disposed.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static MethodInfo ThrowMissingRuntimeHelper()
        => throw new MissingMethodException(nameof(RuntimeHelpers.IsReferenceOrContainsReferences));

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSchemaConflict(SchemaId schemaId)
        => throw new InvalidOperationException($"SchemaId {schemaId} is already registered with a different component layout.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ComponentId ThrowComponentTypeNotRegistered(Type runtimeType)
        => throw new KeyNotFoundException($"The component type {runtimeType} is not registered.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T ThrowInvalidComponentLayoutId<T>()
        => throw new ArgumentOutOfRangeException("id");
}
