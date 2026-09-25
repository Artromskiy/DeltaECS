namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal readonly partial struct ComponentRowOperations
{
    private readonly Func<int, Array>? _createArray;

    private ComponentRowOperations(bool containsReferences, Func<int, Array>? createArray)
    {
        ContainsReferences = containsReferences;
        _createArray = createArray;
    }

    internal bool ContainsReferences { get; }

    internal static ComponentRowOperations ForType<T>()
        => new(
            RuntimeHelpers.IsReferenceOrContainsReferences<T>(),
            static capacity => new T[capacity]);

    internal Array CreateArray(int capacity) => _createArray!(capacity);
}
