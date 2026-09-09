namespace Delta.ECS;

using System;
using System.Collections.Generic;

internal static class SpanSortCompat
{
    internal static void Sort<T>(Span<T> span, IComparer<T> comparer)
    {
#if NETSTANDARD2_1
        for (int start = (span.Length - 2) / 2; start >= 0; start--)
        {
            SiftDown(span, comparer, start, span.Length);
        }

        for (int end = span.Length - 1; end > 0; end--)
        {
            (span[0], span[end]) = (span[end], span[0]);
            SiftDown(span, comparer, 0, end);
        }
#else
        span.Sort(comparer);
#endif
    }

#if NETSTANDARD2_1
    private static void SiftDown<T>(Span<T> span, IComparer<T> comparer, int root, int length)
    {
        while (root <= (length - 2) / 2)
        {
            int child = root * 2 + 1;
            if (child + 1 < length && comparer.Compare(span[child], span[child + 1]) < 0)
            {
                child++;
            }

            if (comparer.Compare(span[root], span[child]) >= 0)
            {
                return;
            }

            (span[root], span[child]) = (span[child], span[root]);
            root = child;
        }
    }
#endif
}
