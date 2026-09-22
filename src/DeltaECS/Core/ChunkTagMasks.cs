namespace Delta.ECS;

using System;

/// <summary>External per-chunk storage for data-less tag membership.</summary>
internal sealed class ChunkTagMasks
{
    private const int WordShift = 6;
    private const int WordMask = (1 << WordShift) - 1;
    private const int WordsPerTag = Chunk.Capacity >> WordShift;

    private ulong[] _summaries = Array.Empty<ulong>();
    private ulong[] _words = Array.Empty<ulong>();

    internal int TagCapacity => _summaries.Length;

    internal void EnsureTagCapacity(int tagCount)
    {
        if (_summaries.Length >= tagCount)
        {
            return;
        }

        Array.Resize(ref _summaries, tagCount);
        Array.Resize(ref _words, checked(tagCount * WordsPerTag));
    }

    internal bool Contains(int tagIndex, int slotIndex)
    {
        if ((uint)tagIndex >= (uint)_summaries.Length)
        {
            return false;
        }

        int wordIndex = slotIndex >> WordShift;
        return (_words[tagIndex * WordsPerTag + wordIndex] & (1UL << (slotIndex & WordMask))) != 0;
    }

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
        _summaries[tagIndex] |= 1UL << wordIndex;
        return true;
    }

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
        }

        return true;
    }

    internal ulong Summary(int tagIndex) => (uint)tagIndex < (uint)_summaries.Length ? _summaries[tagIndex] : 0;

    internal ulong Word(int tagIndex, int wordIndex)
        => (uint)tagIndex < (uint)_summaries.Length ? _words[tagIndex * WordsPerTag + wordIndex] : 0;

    internal void CopySlotTo(ChunkTagMasks target, int sourceSlot, int targetSlot)
    {
        bool sameStorage = ReferenceEquals(this, target);
        if (!sameStorage)
        {
            target.ClearRange(targetSlot, 1);
        }

        if (sameStorage && sourceSlot == targetSlot)
        {
            return;
        }

        int sourceWord = sourceSlot >> WordShift;
        int targetWord = targetSlot >> WordShift;
        int sourceBit = sourceSlot & WordMask;
        int targetBit = targetSlot & WordMask;
        int tagCount = _summaries.Length;
        for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)
        {
            if ((_words[tagIndex * WordsPerTag + sourceWord] & (1UL << sourceBit)) != 0)
            {
                target.Set(tagIndex, targetSlot);
            }
            else
            {
                target.Clear(tagIndex, targetSlot);
            }
        }
    }

    internal void CopyRangeTo(ChunkTagMasks target, int sourceSlot, int targetSlot, int count)
    {
        if (!ReferenceEquals(this, target))
        {
            target.ClearRange(targetSlot, count);
            for (int index = 0; index < count; index++)
            {
                CopySlotTo(target, sourceSlot + index, targetSlot + index);
            }
            return;
        }

        int start = targetSlot > sourceSlot ? count - 1 : 0;
        int end = targetSlot > sourceSlot ? -1 : count;
        int step = targetSlot > sourceSlot ? -1 : 1;
        for (int index = start; index != end; index += step)
        {
            CopySlotTo(target, sourceSlot + index, targetSlot + index);
        }
    }

    internal void ClearRange(int slotIndex, int count)
    {
        int end = slotIndex + count;
        for (int slot = slotIndex; slot < end; slot++)
        {
            int wordIndex = slot >> WordShift;
            ulong bit = 1UL << (slot & WordMask);
            for (int tagIndex = 0; tagIndex < _summaries.Length; tagIndex++)
            {
                int index = tagIndex * WordsPerTag + wordIndex;
                ulong next = _words[index] & ~bit;
                _words[index] = next;
                if (next == 0)
                {
                    _summaries[tagIndex] &= ~(1UL << wordIndex);
                }
            }
        }
    }

    internal void ClearAll()
    {
        Array.Clear(_summaries, 0, _summaries.Length);
        Array.Clear(_words, 0, _words.Length);
    }
}
