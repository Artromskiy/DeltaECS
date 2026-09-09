namespace Delta.ECS;

using System;
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
        return (nint)System.Runtime.InteropServices.NativeMemory.Alloc(byteCount);
#endif
    }

    internal static void Clear(void* address, nuint byteCount)
    {
#if NETSTANDARD2_1
        new Span<byte>(address, checked((int)byteCount)).Clear();
#else
        System.Runtime.InteropServices.NativeMemory.Clear(address, byteCount);
#endif
    }

    internal static void Free(void* address)
    {
#if NETSTANDARD2_1
        Marshal.FreeHGlobal((nint)address);
#else
        System.Runtime.InteropServices.NativeMemory.Free(address);
#endif
    }
}
