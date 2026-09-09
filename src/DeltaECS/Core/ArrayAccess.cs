namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ArrayAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref byte DataReference(Array array) =>
        ref Unsafe.As<byte[]>(array).GetRefAtZero();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this T[] array) =>
#if NETSTANDARD2_1
        ref MemoryMarshal.GetReference(array.AsSpan());
#else
        ref MemoryMarshal.GetArrayDataReference(array);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T GetRefAtZero<T>(this Span<T> span) =>
        ref MemoryMarshal.GetReference(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly T GetRefAtZero<T>(this ReadOnlySpan<T> span) =>
        ref MemoryMarshal.GetReference(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(this T[] array, int index) =>
#if NETSTANDARD2_1
        ref Unsafe.Add(ref MemoryMarshal.GetReference(array.AsSpan()), index);
#else
        ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(Array array, int index) =>
        ref Unsafe.As<T[]>(array).RefAt(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(this Span<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly T RefAt<T>(this ReadOnlySpan<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
}
