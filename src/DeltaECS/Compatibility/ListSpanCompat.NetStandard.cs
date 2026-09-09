namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ListSpanCompat
{
    // List<T> stores its backing array as the first instance field on the
    // supported netstandard2.1 runtimes. Keep this view private to the
    // compatibility layer; callers only see the validated active span.
    [StructLayout(LayoutKind.Sequential)]
    private sealed class ListLayout<T>
    {
        internal T[] Items = null!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> AsSpan<T>(List<T> list)
    {
        ref ListLayout<T> layout = ref Unsafe.As<List<T>, ListLayout<T>>(ref list);
        return layout.Items.AsSpan(0, list.Count);
    }
}
