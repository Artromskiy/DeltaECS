namespace Delta.ECS;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ArrayAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Element<T>(this T[] array, int index) =>
        ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
}
