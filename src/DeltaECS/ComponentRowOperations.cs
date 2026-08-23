namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal delegate void CopyOneComponent(Array source, int sourceIndex, Array target, int targetIndex);

internal delegate void ClearOneComponent(Array row, int index);

internal readonly struct ComponentRowOperations
{
    private static readonly CopyOneComponent _copyFallback = CopyFallback;
    private static readonly ClearOneComponent _clearFallback = ClearFallback;

    private ComponentRowOperations(
        CopyOneComponent copyOne,
        ClearOneComponent clearOne,
        bool containsReferences)
    {
        CopyOne = copyOne;
        ClearOne = clearOne;
        ContainsReferences = containsReferences;
    }

    public CopyOneComponent CopyOne { get; }

    public ClearOneComponent ClearOne { get; }

    public bool ContainsReferences { get; }

    public static ComponentRowOperations Fallback { get; } = new(
        _copyFallback,
        _clearFallback,
        containsReferences: true);

    public static ComponentRowOperations For<T>() => Cache<T>.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyTyped<T>(Array source, int sourceIndex, Array target, int targetIndex) => ((T[])target)[targetIndex] = ((T[])source)[sourceIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearTyped<T>(Array row, int index) => ((T[])row)[index] = default!;

    private static void CopyFallback(Array source, int sourceIndex, Array target, int targetIndex) => Array.Copy(source, sourceIndex, target, targetIndex, 1);

    private static void ClearFallback(Array row, int index) => Array.Clear(row, index, 1);

    private static class Cache<T>
    {
        internal static readonly ComponentRowOperations Value = new(
            CopyTyped<T>,
            ClearTyped<T>,
            RuntimeHelpers.IsReferenceOrContainsReferences<T>());
    }
}
