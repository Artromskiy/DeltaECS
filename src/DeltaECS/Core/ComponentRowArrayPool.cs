namespace Delta.ECS;

using System;
using System.Collections.Generic;

internal sealed class ComponentRowArrayPool
{
    private readonly Dictionary<Type, Stack<Array>> _arrays = new();

    internal Array Rent(Type runtimeType, ComponentRowOperations rowOperations, int capacity)
    {
        if (_arrays.TryGetValue(runtimeType, out Stack<Array>? arrays)
            && arrays.Count != 0)
        {
            return arrays.Pop();
        }

        return rowOperations.CreateArray(runtimeType, capacity);
    }

    internal void Return(Array array)
    {
        Array.Clear(array, 0, array.Length);
        Type? runtimeType = array.GetType().GetElementType();
        if (runtimeType is null)
        {
            ThrowHelper.ThrowArrayRowsRequiresRuntimeType();
            return;
        }

        if (!_arrays.TryGetValue(runtimeType, out Stack<Array>? arrays))
        {
            arrays = new Stack<Array>();
            _arrays.Add(runtimeType, arrays);
        }

        arrays.Push(array);
    }

    internal void Clear() => _arrays.Clear();
}
