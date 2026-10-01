namespace Delta.ECS;

using System;
using System.Reflection;
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

    internal static ComponentRowOperations ForType(Type componentType)
        => new(
            TypeContainsReferences(componentType),
            capacity => Array.CreateInstance(componentType, capacity));

    private static bool TypeContainsReferences(Type componentType)
    {
        if (componentType.IsPrimitive || componentType.IsEnum || componentType.IsPointer)
        {
            return false;
        }

        if (!componentType.IsValueType)
        {
            return true;
        }

        foreach (FieldInfo field in componentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (TypeContainsReferences(field.FieldType))
            {
                return true;
            }
        }

        return false;
    }

    internal Array CreateArray(int capacity) => _createArray!(capacity);
}
