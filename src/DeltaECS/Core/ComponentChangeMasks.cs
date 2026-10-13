namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Word and range operations for component change and presence masks.</summary>
/// <remarks>Bit positions and lengths are trusted, in-range chunk-local values.</remarks>
internal static class ComponentChangeMasks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordCount(int count) => (count + 63) >> 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordStart(int wordIndex) => wordIndex << 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordLength(int count, int wordIndex) => Math.Min(64, count - (wordIndex << 6));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Intersect(ulong left, ulong right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsFullWord(ulong word, int count, int wordIndex) => word == GetValidWord(count, wordIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int TakeFirstSlot(ref ulong word, int wordIndex)
    {
        int slot = (wordIndex << 6) + BitOperationsCompat.TrailingZeroCount(word);
        word &= word - 1;
        return slot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMarked(ReadOnlySpan<ulong> mask, int index) => GetSlot(mask, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MarkEntity(Span<ulong> mask, int index) => SetSlot(mask, index, true);

    /// <summary>OR the original source bit range into the target, including overlaps.</summary>
    internal static void MergeRange(
        ReadOnlySpan<ulong> source, int sourceSlot,
        Span<ulong> target, int targetSlot, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if (count <= 64)
        {
            ulong bits = Read64(source, sourceSlot) & LowMask(count);
            WriteSmall(target, targetSlot, bits, count, merge: true);
            return;
        }

        bool overlaps = source.Overlaps(target, out int targetOffset);
        if (overlaps)
        {
            long targetStart = (long)targetOffset * 64 + targetSlot;
            if (targetStart == sourceSlot)
            {
                return;
            }

            overlaps = sourceSlot < targetStart + count
                       && targetStart < (long)sourceSlot + count;
        }

        if (!overlaps)
        {
            WriteRange(source, sourceSlot, target, targetSlot, count, merge: true);
            return;
        }

        int words = WordCount(count);
        Span<ulong> scratch = stackalloc ulong[words];
        for (int index = 0; index < words; index++)
        {
            int remaining = count - (index << 6);
            scratch[index] = Read64(source, sourceSlot + (index << 6))
                             & LowMask(Math.Min(64, remaining));
        }

        WriteRange(scratch, 0, target, targetSlot, count, merge: true);
    }

    internal static DirtyKind Classify(ReadOnlySpan<ulong> mask, int count)
    {
        bool any = false;
        bool all = count != 0;
        int words = WordCount(count);
        for (int index = 0; index < words; index++)
        {
            ulong valid = GetValidWord(count, index);
            ulong word = mask[index] & valid;
            any |= word != 0;
            all &= word == valid;
            if (any && !all)
            {
                return DirtyKind.Mask;
            }
        }

        return all ? DirtyKind.All : any ? DirtyKind.Mask : DirtyKind.None;
    }

    internal static DirtyKind SelectTagChanges(
        Chunk chunk, int tagIndex, DirtyKind kind,
        ReadOnlySpan<ulong> mask, Span<ulong> selected)
    {
        int words = WordCount(chunk.Count);
        if (kind == DirtyKind.None)
        {
            selected.Slice(0, words).Clear();
            return DirtyKind.None;
        }

        bool any = false;
        bool all = chunk.Count != 0;
        if (kind == DirtyKind.All)
        {
            for (int index = 0; index < words; index++)
            {
                ulong valid = GetValidWord(chunk.Count, index);
                ulong value = chunk.GetTagWord(tagIndex, index) & valid;
                selected[index] = value;
                any |= value != 0;
                all &= value == valid;
            }
        }
        else
        {
            for (int index = 0; index < words; index++)
            {
                ulong valid = GetValidWord(chunk.Count, index);
                ulong value = mask[index] & chunk.GetTagWord(tagIndex, index) & valid;
                selected[index] = value;
                any |= value != 0;
                all &= value == valid;
            }
        }

        return all ? DirtyKind.All : any ? DirtyKind.Mask : DirtyKind.None;
    }

    internal static void Union(Span<ulong> target, ReadOnlySpan<ulong> source, int count)
    {
        for (int index = 0, end = WordCount(count); index < end; index++)
        {
            Union(ref target[index], source[index]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetValidWord(int count, int wordIndex)
    {
        int remaining = count - (wordIndex << 6);
        return remaining >= 64 ? ulong.MaxValue : LowMask(remaining);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GetSlot(ReadOnlySpan<ulong> mask, int slotIndex) =>
        (mask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetSlot(Span<ulong> mask, int slotIndex, bool value)
    {
        ulong bit = 1UL << (slotIndex & 63);
        ref ulong word = ref mask[slotIndex >> 6];
        word = value ? word | bit : word & ~bit;
    }

    internal static void SetRange(Span<ulong> mask, int start, int count, bool value)
    {
        if (count <= 0)
        {
            return;
        }

        int wordIndex = start >> 6;
        int offset = start & 63;
        if (offset != 0)
        {
            int first = Math.Min(count, 64 - offset);
            ulong bits = LowMask(first) << offset;
            mask[wordIndex] = value ? mask[wordIndex] | bits : mask[wordIndex] & ~bits;
            count -= first;
            wordIndex++;
        }

        int fullWords = count >> 6;
        if (fullWords != 0)
        {
            Span<ulong> whole = mask.Slice(wordIndex, fullWords);
            if (value)
            {
                whole.Fill(ulong.MaxValue);
            }
            else
            {
                whole.Clear();
            }

            count -= fullWords << 6;
            wordIndex += fullWords;
        }

        if (count == 0)
        {
            return;
        }

        {
            ulong bits = LowMask(count);
            mask[wordIndex] = value ? mask[wordIndex] | bits : mask[wordIndex] & ~bits;
        }
    }

    /// <summary>Replace destination bits by source bits; overlapping spans are supported.</summary>
    internal static void CopyBits(
        ReadOnlySpan<ulong> source, int sourceSlot,
        Span<ulong> target, int targetSlot, int count)
    {
        if (count <= 0)
        {
            return;
        }

        // CopyTo has memmove semantics for overlapping spans.
        if (((sourceSlot | targetSlot | count) & 63) == 0)
        {
            source.Slice(sourceSlot >> 6, count >> 6)
                .CopyTo(target.Slice(targetSlot >> 6, count >> 6));
            return;
        }

        if (count <= 64)
        {
            ulong bits = Read64(source, sourceSlot) & LowMask(count);
            WriteSmall(target, targetSlot, bits, count, merge: false);
            return;
        }

        bool overlap = source.Overlaps(target, out int targetOffset);
        if (overlap)
        {
            // targetOffset is measured in ulong elements relative to source.
            long targetStart = (long)targetOffset * 64 + targetSlot;
            overlap = sourceSlot < targetStart + count
                      && targetStart < (long)sourceSlot + count;
        }

        if (!overlap)
        {
            WriteRange(source, sourceSlot, target, targetSlot, count, merge: false);
            return;
        }

        int words = WordCount(count);
        Span<ulong> scratch = stackalloc ulong[words];
        for (int index = 0; index < words; index++)
        {
            int remaining = count - (index << 6);
            scratch[index] = Read64(source, sourceSlot + (index << 6))
                             & LowMask(Math.Min(64, remaining));
        }

        WriteRange(scratch, 0, target, targetSlot, count, merge: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Union(ref ulong target, ulong source) => target |= source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong LowMask(int count) => count == 64 ? ulong.MaxValue : (1UL << count) - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Read64(ReadOnlySpan<ulong> words, int bitIndex)
    {
        int wordIndex = bitIndex >> 6;
        int shift = bitIndex & 63;
        ulong result = words[wordIndex] >> shift;
        if (shift != 0 && wordIndex + 1 < words.Length)
        {
            result |= words[wordIndex + 1] << (64 - shift);
        }

        return result;
    }

    // Copy/OR at most 64 bits. Capture the complete source before touching the target;
    // this also handles overlapping short moves.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSmall(Span<ulong> target, int targetSlot, ulong value, int count, bool merge)
    {
        int wordIndex = targetSlot >> 6;
        int shift = targetSlot & 63;
        int first = Math.Min(count, 64 - shift);
        ulong firstMask = LowMask(first);
        ulong destinationMask = firstMask << shift;
        ulong placed = (value & firstMask) << shift;
        target[wordIndex] = merge
            ? target[wordIndex] | placed
            : (target[wordIndex] & ~destinationMask) | placed;

        int remaining = count - first;
        if (remaining == 0)
        {
            return;
        }

        ulong mask = LowMask(remaining);
        ulong upper = (value >> first) & mask;
        target[wordIndex + 1] = merge
            ? target[wordIndex + 1] | upper
            : (target[wordIndex + 1] & ~mask) | upper;
    }

    // Only use directly if source and destination bit ranges do not overlap.
    private static void WriteRange(
        ReadOnlySpan<ulong> source, int sourceSlot,
        Span<ulong> target, int targetSlot, int count, bool merge)
    {
        int copied = 0;
        while (copied < count)
        {
            int destination = targetSlot + copied;
            int offset = destination & 63;
            int length = Math.Min(count - copied, 64 - offset);
            ulong lowMask = LowMask(length);
            ulong word = Read64(source, sourceSlot + copied);
            ulong placed = (word & lowMask) << offset;
            ref ulong targetWord = ref target[destination >> 6];
            if (merge)
            {
                targetWord |= placed;
            }
            else
            {
                ulong destinationMask = lowMask << offset;
                targetWord = (targetWord & ~destinationMask) | placed;
            }

            copied += length;
        }
    }

}
