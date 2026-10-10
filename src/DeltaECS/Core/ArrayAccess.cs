namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ArrayAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe Span<T> AsSpan<T>(nint address, int length) where T : unmanaged =>
        length == 0 ? Span<T>.Empty : new((void*)address, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ReadOnlySpan<T> AsReadOnlySpan<T>(nint address, int length) where T : unmanaged =>
        AsSpan<T>(address, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ref T GetRefAtZero<T>(nint address) where T : unmanaged =>
        ref Unsafe.AsRef<T>((void*)address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T RefAt<T>(nint address, int index) where T : unmanaged =>
        ref Unsafe.Add(ref GetRefAtZero<T>(address), index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static nint AddBytes(nint address, nuint byteCount) =>
        checked(address + checked((nint)byteCount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe nint GetAddress(void* pointer) => (nint)pointer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void* GetPointer(nint address) => (void*)address;

#if NETSTANDARD2_1
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

}
