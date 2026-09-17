namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if DELTAECS_ARRAY_REFERENCE_UNSAFE_OFFSET && !NET10_0
#error The UnsafeOffset benchmark variant depends on the .NET 10 CoreCLR intrinsic.
#endif

internal static class ArrayAccess
{
#if DELTAECS_ARRAY_REFERENCE_UNSAFE_OFFSET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this T[] array)
    {
        // This experiment relies on CoreCLR's SZArray layout and Unsafe.As object-reference intrinsic.
        ref int length = ref Unsafe.As<ArrayHeader>(array).Length;
        ref byte firstElement = ref Unsafe.AddByteOffset(
            ref Unsafe.As<int, byte>(ref length),
            (IntPtr)IntPtr.Size);
        return ref Unsafe.As<byte, T>(ref firstElement);
    }
#elif DELTAECS_ARRAY_REFERENCE_SPAN
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this T[] array) =>
        ref MemoryMarshal.GetReference(array.AsSpan());
#elif DELTAECS_ARRAY_REFERENCE_INDEX
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this T[] array) => ref array[0];
#elif DELTAECS_ARRAY_REFERENCE_FIXED || NETSTANDARD2_1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ref T GetRefAtZero<T>(this T[] array)
    {
        // The array is pinned here; only a GC-tracked managed byref escapes this scope.
#pragma warning disable CS8500
        fixed (void* pointer = array)
        {
            return ref Unsafe.As<byte, T>(ref Unsafe.AsRef<byte>(pointer));
        }
#pragma warning restore CS8500
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this T[] array) =>
        ref MemoryMarshal.GetArrayDataReference(array);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(this T[] array, int index)
#if NETSTANDARD2_1
        // Call sites validate or derive the index from a bounded loop.
        => ref Unsafe.Add(ref array[0], index);
#else
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this Span<T> span) =>
        ref MemoryMarshal.GetReference(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly T GetRefAtZero<T>(this ReadOnlySpan<T> span) =>
        ref MemoryMarshal.GetReference(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(this Span<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly T RefAt<T>(this ReadOnlySpan<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

#if DELTAECS_ARRAY_REFERENCE_UNSAFE_OFFSET
    private sealed class ArrayHeader
    {
        internal int Length;
    }
#endif
}
