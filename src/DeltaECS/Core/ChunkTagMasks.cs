namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>External per-chunk storage for data-less tag membership.</summary>
internal sealed class ChunkTagMasks
{
    private const int WordShift = 6;
    private const int WordMask = (1 << WordShift) - 1;
    private const int WordsPerTag = (Chunk.Capacity + WordMask) >> WordShift;

    private ulong[] _summaries = [];
    private ulong[] _words = [];
    private readonly List<int> _activeTags = new();
    private bool[] _activeTagFlags = [];

    internal int TagCapacity => _summaries.Length;

    internal void EnsureTagCapacity(int tagCount)
    {
        if (_summaries.Length >= tagCount)
        {
            return;
        }

        Array.Resize(ref _summaries, tagCount);
        Array.Resize(ref _words, checked(tagCount * WordsPerTag));
        Array.Resize(ref _activeTagFlags, tagCount);
    }

    // Structural mutations keep the index ordered; concurrent readers never rebuild it.
    internal ReadOnlySpan<int> GetActiveTagIndices() => ArrayAccess.AsSpan(_activeTags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(int tagIndex, int slotIndex)
    {
        if ((uint)tagIndex >= (uint)_summaries.Length)
        {
            return false;
        }

        int wordIndex = slotIndex >> WordShift;
        ulong bit = 1UL << (slotIndex & WordMask);
        return (_words[tagIndex * WordsPerTag + wordIndex] & bit) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Set(int tagIndex, int slotIndex)
    {
        int wordIndex = slotIndex >> WordShift;
        int index = tagIndex * WordsPerTag + wordIndex;
        ulong bit = 1UL << (slotIndex & WordMask);
        ulong previous = _words[index];
        if ((previous & bit) != 0)
        {
            return false;
        }

        _words[index] = previous | bit;
        if (previous == 0)
        {
            _summaries[tagIndex] |= 1UL << wordIndex;
            RefreshActiveTag(tagIndex);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Clear(int tagIndex, int slotIndex)
    {
        if ((uint)tagIndex >= (uint)_summaries.Length)
        {
            return false;
        }

        int wordIndex = slotIndex >> WordShift;
        int index = tagIndex * WordsPerTag + wordIndex;
        ulong bit = 1UL << (slotIndex & WordMask);
        ulong previous = _words[index];
        if ((previous & bit) == 0)
        {
            return false;
        }

        ulong next = previous & ~bit;
        _words[index] = next;
        if (next == 0)
        {
            _summaries[tagIndex] &= ~(1UL << wordIndex);
            RefreshActiveTag(tagIndex);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ulong Summary(int tagIndex)
        => (uint)tagIndex < (uint)_summaries.Length ? _summaries[tagIndex] : 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ulong Word(int tagIndex, int wordIndex)
        => (uint)tagIndex < (uint)_summaries.Length ? _words[tagIndex * WordsPerTag + wordIndex] : 0;

    internal void CopySlotTo(ChunkTagMasks target, int sourceSlot, int targetSlot)
    {
        if (ReferenceEquals(this, target) && sourceSlot == targetSlot)
        {
            return;
        }

        if (target.TagCapacity < TagCapacity)
        {
            ThrowHelper.ThrowTagMaskTargetCapacityTooSmall(nameof(target));
        }

        int sourceWord = sourceSlot >> WordShift;
        int targetWord = targetSlot >> WordShift;
        ulong sourceBit = 1UL << (sourceSlot & WordMask);
        ulong targetBit = 1UL << (targetSlot & WordMask);
        ulong targetWordBit = 1UL << targetWord;

        int tag = 0;
        for (; tag < _summaries.Length; tag++)
        {
            int sourceIndex = tag * WordsPerTag + sourceWord;
            int destinationIndex = tag * WordsPerTag + targetWord;
            bool present = (_words[sourceIndex] & sourceBit) != 0;
            ulong old = target._words[destinationIndex];
            ulong next = present ? old | targetBit : old & ~targetBit;
            if (old == next)
            {
                continue;
            }

            target._words[destinationIndex] = next;
            if (old == 0)
            {
                target._summaries[tag] |= targetWordBit;
            }
            else if (next == 0)
            {
                target._summaries[tag] &= ~targetWordBit;
            }
            target.RefreshActiveTag(tag);
        }

        // A wider target may contain registrations absent from the source.
        for (; tag < target._summaries.Length; tag++)
        {
            int index = tag * WordsPerTag + targetWord;
            ulong next = target._words[index] & ~targetBit;
            target._words[index] = next;
            if (next == 0)
            {
                target._summaries[tag] &= ~targetWordBit;
                target.RefreshActiveTag(tag);
            }
        }
    }

    internal void CopyRangeTo(ChunkTagMasks target, int sourceSlot, int targetSlot, int count)
    {
        if (count <= 0 || (ReferenceEquals(this, target) && sourceSlot == targetSlot))
        {
            return;
        }

        if (count == 1)
        {
            CopySlotTo(target, sourceSlot, targetSlot);
            return;
        }

        if (target.TagCapacity < TagCapacity)
        {
            ThrowHelper.ThrowTagMaskTargetCapacityTooSmall(nameof(target));
        }

        int firstWord = targetSlot >> WordShift;
        int lastWord = (targetSlot + count - 1) >> WordShift;

        for (int tag = 0; tag < _summaries.Length; tag++)
        {
            if ((_summaries[tag] | target._summaries[tag]) == 0)
            {
                continue;
            }

            int start = tag * WordsPerTag;
            ReadOnlySpan<ulong> sourceWords = _words.AsSpan(start, WordsPerTag);
            Span<ulong> targetWords = target._words.AsSpan(start, WordsPerTag);

            ComponentChangeMasks.CopyBits(sourceWords, sourceSlot, targetWords, targetSlot, count);
            target.RefreshSummaryWords(tag, firstWord, lastWord);
        }

        // Extra target registrations correspond to absent source tags.
        for (int tag = _summaries.Length; tag < target._summaries.Length; tag++)
        {
            int start = tag * WordsPerTag;
            // Only the destination range is cleared, not the whole tag.
            ComponentChangeMasks.SetRange(target._words.AsSpan(), start * 64 + targetSlot, count, value: false);
            target.RefreshSummaryWords(tag, firstWord, lastWord);
        }
    }

    internal void ClearRange(int slotIndex, int count)
    {
        if (count <= 0)
        {
            return;
        }

        int firstWord = slotIndex >> WordShift;
        int lastWord = (slotIndex + count - 1) >> WordShift;
        int lastBits = ((slotIndex + count - 1) & WordMask) + 1;
        ulong firstMask = ulong.MaxValue << (slotIndex & WordMask);
        ulong lastMask = lastBits == 64 ? ulong.MaxValue : (1UL << lastBits) - 1;
        int touchedWords = lastWord - firstWord + 1;
        ulong touchedMask = (touchedWords == 64
            ? ulong.MaxValue
            : (1UL << touchedWords) - 1) << firstWord;

        for (int tag = 0; tag < _summaries.Length; tag++)
        {
            ulong summary = _summaries[tag];
            if ((summary & touchedMask) == 0)
            {
                continue;
            }

            int baseIndex = tag * WordsPerTag;
            for (int word = firstWord; word <= lastWord; word++)
            {
                ulong summaryBit = 1UL << word;
                if ((summary & summaryBit) == 0)
                {
                    continue;
                }

                ulong clearMask = ulong.MaxValue;
                if (word == firstWord)
                {
                    clearMask &= firstMask;
                }

                if (word == lastWord)
                {
                    clearMask &= lastMask;
                }

                int index = baseIndex + word;
                ulong next = _words[index] & ~clearMask;
                _words[index] = next;
                if (next == 0)
                {
                    summary &= ~summaryBit;
                }
            }

            _summaries[tag] = summary;
            RefreshActiveTag(tag);
        }
    }

    internal void ClearAll()
    {
        Array.Clear(_summaries, 0, _summaries.Length);
        Array.Clear(_words, 0, _words.Length);
        foreach (int tag in _activeTags)
        {
            _activeTagFlags[tag] = false;
        }
        _activeTags.Clear();
    }

    private void RefreshSummaryWords(int tagIndex, int firstWord, int lastWord)
    {
        int baseIndex = tagIndex * WordsPerTag;
        ulong summary = _summaries[tagIndex];
        for (int word = firstWord; word <= lastWord; word++)
        {
            ulong bit = 1UL << word;
            if (_words[baseIndex + word] == 0)
            {
                summary &= ~bit;
            }
            else
            {
                summary |= bit;
            }
        }

        _summaries[tagIndex] = summary;
        RefreshActiveTag(tagIndex);
    }

    private void RefreshActiveTag(int tagIndex)
    {
        bool active = _summaries[tagIndex] != 0;
        if (_activeTagFlags[tagIndex] == active)
        {
            return;
        }
        _activeTagFlags[tagIndex] = active;
        int position = _activeTags.BinarySearch(tagIndex);
        if (active)
        {
            _activeTags.Insert(~position, tagIndex);
        }
        else
        {
            _activeTags.RemoveAt(position);
        }
    }
}
