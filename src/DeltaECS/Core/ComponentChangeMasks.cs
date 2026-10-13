namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Word and range operations for component change and presence masks.</summary>
internal static class ComponentChangeMasks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordCount(int count) => (count + 63) >> 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordStart(int wordIndex) => wordIndex << 6;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WordLength(int count, int wordIndex) => Math.Min(64, count - WordStart(wordIndex));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Intersect(ulong left, ulong right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Union(ref ulong target, ulong source) => target |= source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsFullWord(ulong word, int count, int wordIndex) => word == GetValidWord(count, wordIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int TakeFirstSlot(ref ulong word, int wordIndex)
    {
        int slot = WordStart(wordIndex) + BitOperationsCompat.TrailingZeroCount(word);
        word &= word - 1;
        return slot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMarked(ulong[] mask, int index) => GetSlot(mask, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MarkEntity(ulong[] mask, int index) => SetSlot(mask, index, true);

    internal static void MergeRange(ulong[] source, int sourceSlot, ulong[] target, int targetSlot, int count)
    {
        // Scratch preserves the source when ranges share storage.
        Span<ulong> moved = stackalloc ulong[Chunk.WordCount];
        moved.Clear();
        int copied = 0;
        while (copied < count)
        {
            int sourcePosition = sourceSlot + copied;
            int targetPosition = targetSlot + copied;
            int length = Math.Min(count - copied, Math.Min(64 - (sourcePosition & 63), 64 - (targetPosition & 63)));
            ulong bits = (source[sourcePosition >> 6] >> (sourcePosition & 63)) & (ulong.MaxValue >> (64 - length));
            moved[targetPosition >> 6] |= bits << (targetPosition & 63);
            copied += length;
        }
        for (int index = 0; index < Chunk.WordCount; index++)
        {
            Union(ref target[index], moved[index]);
        }
    }

    internal static DirtyKind Classify(ReadOnlySpan<ulong> mask, int count)
    {
        bool any = false;
        bool all = count != 0;
        for (int index = 0; index < WordCount(count); index++)
        {
            ulong valid = GetValidWord(count, index);
            ulong word = Intersect(mask[index], valid);
            any |= word != 0;
            all &= word == valid;
        }
        return all ? DirtyKind.All : any ? DirtyKind.Mask : DirtyKind.None;
    }

    internal static DirtyKind SelectTagChanges(Chunk chunk, int tagIndex, DirtyKind kind, ReadOnlySpan<ulong> mask, Span<ulong> selected)
    {
        bool any = false;
        bool all = chunk.Count != 0;
        for (int index = 0; index < WordCount(chunk.Count); index++)
        {
            ulong valid = GetValidWord(chunk.Count, index);
            ulong writes = kind == DirtyKind.All ? valid : Intersect(mask[index], valid);
            ulong word = Intersect(writes, chunk.GetTagWord(tagIndex, index));
            selected[index] = word;
            any |= word != 0;
            all &= word == valid;
        }
        return all ? DirtyKind.All : any ? DirtyKind.Mask : DirtyKind.None;
    }

    internal static void Union(Span<ulong> target, ReadOnlySpan<ulong> source, int count)
    {
        for (int index = 0; index < WordCount(count); index++)
        {
            Union(ref target[index], source[index]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetValidWord(int count, int wordIndex)
    {
        int remaining = count - (wordIndex << 6);
        return remaining >= 64 ? ulong.MaxValue : (1UL << remaining) - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GetSlot(ulong[] mask, int slotIndex)
        => (mask[slotIndex >> 6] & (1UL << (slotIndex & 63))) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetSlot(ulong[] mask, int slotIndex, bool value)
    {
        ulong bit = 1UL << (slotIndex & 63);
        ref ulong word = ref mask[slotIndex >> 6];
        word = value ? word | bit : word & ~bit;
    }

    internal static void SetRange(ulong[] mask, int start, int count, bool value)
    {
        while (count > 0)
        {
            int offset = start & 63;
            int length = Math.Min(count, 64 - offset);
            ulong bits = (ulong.MaxValue >> (64 - length)) << offset;
            ref ulong word = ref mask[start >> 6];
            word = value ? word | bits : word & ~bits;
            start += length;
            count -= length;
        }
    }

    internal static void CopyBits(ulong[] source, int sourceSlot, ulong[] target, int targetSlot, int count)
    {
        // A chunk-sized scratch also makes overlapping moves safe.
        Span<ulong> bits = stackalloc ulong[Chunk.WordCount];
        int words = (count + 63) >> 6;
        for (int index = 0; index < words; index++)
        {
            int position = sourceSlot + (index << 6);
            int word = position >> 6;
            int shift = position & 63;
            ulong value = source[word] >> shift;
            if (shift != 0 && word + 1 < source.Length)
            {
                value |= source[word + 1] << (64 - shift);
            }

            bits[index] = value;
        }

        int copied = 0;
        while (copied < count)
        {
            int offset = (targetSlot + copied) & 63;
            int length = Math.Min(count - copied, 64 - offset);
            int sourceWord = copied >> 6;
            int shift = copied & 63;
            ulong value = bits[sourceWord] >> shift;
            if (shift != 0 && sourceWord + 1 < words)
            {
                value |= bits[sourceWord + 1] << (64 - shift);
            }

            ulong valid = ulong.MaxValue >> (64 - length);
            ref ulong targetWord = ref target[(targetSlot + copied) >> 6];
            targetWord = (targetWord & ~(valid << offset)) | ((value & valid) << offset);
            copied += length;
        }
    }

}
