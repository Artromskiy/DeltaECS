namespace Delta.ECS;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ArrayAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte DataReference(Array array) =>
        ref MemoryMarshal.GetArrayDataReference(Unsafe.As<byte[]>(array));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Ref<T>(this T[] array, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Ref<T>(this Span<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T Ref<T>(this ReadOnlySpan<T> span, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
}
