namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class ListSpanCompat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> AsSpan<T>(List<T> list) => CollectionsMarshal.AsSpan(list);
}
