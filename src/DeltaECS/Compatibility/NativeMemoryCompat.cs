namespace Delta.ECS;

#if NETSTANDARD2_1
using System.Runtime.InteropServices;
#endif

internal static unsafe class NativeMemoryCompat
{
    internal static nint Alloc(nuint byteCount)
    {
#if NETSTANDARD2_1
        return (nint)Marshal.AllocHGlobal(checked((nint)byteCount));
#else
        return ArrayAccess.GetAddress(System.Runtime.InteropServices.NativeMemory.Alloc(byteCount));
#endif
    }

    internal static void Clear(nint address, nuint byteCount)
    {
#if NETSTANDARD2_1
        ArrayAccess.AsSpan<byte>(address, checked((int)byteCount)).Clear();
#else
        System.Runtime.InteropServices.NativeMemory.Clear(ArrayAccess.GetPointer(address), byteCount);
#endif
    }

    internal static void Free(nint address)
    {
#if NETSTANDARD2_1
        Marshal.FreeHGlobal(address);
#else
        System.Runtime.InteropServices.NativeMemory.Free(ArrayAccess.GetPointer(address));
#endif
    }
}
