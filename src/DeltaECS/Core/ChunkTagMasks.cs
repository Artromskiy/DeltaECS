namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

/// <summary>External per-chunk storage for data-less tag membership.</summary>
internal sealed class ChunkTagMasks
{
    private const int WordShift = 6;
    private const int WordMask = (1 << WordShift) - 1;
    private int _wordsPerTag;

    private ulong[] _summaries = [];
    private ulong[] _words = [];
    private readonly List<int> _activeTags = new();
    private bool[] _activeTagFlags = [];

    internal int TagCapacity => _summaries.Length;

    internal ChunkTagMasks(int capacity = Chunk.MaxCapacity)
        => _wordsPerTag = (capacity + WordMask) >> WordShift;

    internal void EnsureSlotCapacity(int capacity)
    {
        int wordsPerTag = (capacity + WordMask) >> WordShift;
        if (wordsPerTag <= _wordsPerTag)
        {
            return;
        }
        var words = new ulong[checked(_summaries.Length * wordsPerTag)];
        for (int tag = 0; tag < _summaries.Length; tag++)
        {
            _words.AsSpan(tag * _wordsPerTag, _wordsPerTag)
                .CopyTo(words.AsSpan(tag * wordsPerTag, _wordsPerTag));
        }
        _words = words;
        _wordsPerTag = wordsPerTag;
    }

    internal void EnsureTagCapacity(int tagCount)
    {
        if (_summaries.Length >= tagCount)
        {
            return;
        }

        Array.Resize(ref _summaries, tagCount);
        Array.Resize(ref _words, checked(tagCount * _wordsPerTag));
        Array.Resize(ref _activeTagFlags, tagCount);
    }

    // Structural mutations keep the index ordered; concurrent readers never rebuild it.
    internal ReadOnlySpan<int> GetActiveTagIndices() => ArrayAccess.AsSpan(_activeTags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(int tagIndex, int slotIndex)
    {
        if ((uint)tagIndex >= (uint)_summaries.Length || (uint)(slotIndex >> WordShift) >= (uint)_wordsPerTag)
        {
            return false;
        }

        int wordIndex = slotIndex >> WordShift;
        ulong bit = 1UL << (slotIndex & WordMask);
        return (_words[tagIndex * _wordsPerTag + wordIndex] & bit) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Set(int tagIndex, int slotIndex)
    {
        int wordIndex = slotIndex >> WordShift;
        int index = tagIndex * _wordsPerTag + wordIndex;
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
        if ((uint)tagIndex >= (uint)_summaries.Length || (uint)(slotIndex >> WordShift) >= (uint)_wordsPerTag)
        {
            return false;
        }

        int wordIndex = slotIndex >> WordShift;
        int index = tagIndex * _wordsPerTag + wordIndex;
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
        => (uint)tagIndex < (uint)_summaries.Length && (uint)wordIndex < (uint)_wordsPerTag
            ? _words[tagIndex * _wordsPerTag + wordIndex] : 0;

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
            int sourceIndex = tag * _wordsPerTag + sourceWord;
            int destinationIndex = tag * target._wordsPerTag + targetWord;
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
            int index = tag * target._wordsPerTag + targetWord;
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

            int start = tag * _wordsPerTag;
            ReadOnlySpan<ulong> sourceWords = _words.AsSpan(start, _wordsPerTag);
            Span<ulong> targetWords = target._words.AsSpan(tag * target._wordsPerTag, target._wordsPerTag);

            ComponentChangeMasks.CopyBits(sourceWords, sourceSlot, targetWords, targetSlot, count);
            target.RefreshSummaryWords(tag, firstWord, lastWord);
        }

        // Extra target registrations correspond to absent source tags.
        for (int tag = _summaries.Length; tag < target._summaries.Length; tag++)
        {
            int start = tag * target._wordsPerTag;
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

            int baseIndex = tag * _wordsPerTag;
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
        int baseIndex = tagIndex * _wordsPerTag;
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
