namespace Delta.ECS;

using System;
using System.Collections.Generic;

internal sealed class ComponentRowArrayPool
{
    private readonly Dictionary<Type, Stack<Array>> _arrays = new();
    private readonly Dictionary<int, Stack<Array[]>> _rowReferences = new();

    internal Array Rent(Type runtimeType, ComponentRowOperations rowOperations, int capacity)
    {
        if (_arrays.TryGetValue(runtimeType, out Stack<Array>? arrays)
            && arrays.Count != 0)
        {
            return arrays.Pop();
        }

        return rowOperations.CreateArray(capacity);
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

    internal Array[] RentRowReferences(int count)
        => count == 0 ? []
            : _rowReferences.TryGetValue(count, out Stack<Array[]>? arrays) && arrays.Count != 0
                ? arrays.Pop()
                : new Array[count];

    internal void ReturnRowReferences(Array[] rows)
    {
        if (rows.Length == 0)
        {
            return;
        }

        Array.Clear(rows, 0, rows.Length);
        if (!_rowReferences.TryGetValue(rows.Length, out Stack<Array[]>? arrays))
        {
            arrays = new Stack<Array[]>();
            _rowReferences.Add(rows.Length, arrays);
        }

        arrays.Push(rows);
    }

    internal void Clear()
    {
        _arrays.Clear();
        _rowReferences.Clear();
    }
}
