namespace Delta.ECS;

using System;
using System.Collections.Generic;

internal enum OverlayMaskResult : byte
{
    None,
    Full,
    Partial
}

internal sealed class OverlayTagManager
{
    private sealed class TagState
    {
        public readonly List<int> ChunkIds = new();
        public readonly Dictionary<int, int> ChunkIndexById = new();
        public readonly List<ulong[]> Masks = new();
    }

    private readonly Dictionary<int, TagState> _tagStates = new();
    private readonly int _wordsPerChunk;

    public OverlayTagManager(int chunkCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkCapacity);

        _wordsPerChunk = (chunkCapacity + 63) / 64;
    }

    public int WordsPerChunk => _wordsPerChunk;

    internal bool HasAnyTags { get; private set; }

    public void AddTag(int chunkId, int slotIndex, TagId tag)
    {
        HasAnyTags = true;
        var state = GetOrCreateState(tag);
        ulong[] mask = GetOrAddChunk(state, chunkId);
        SetBit(mask, slotIndex, true);
    }

    public void RemoveTag(int chunkId, int slotIndex, TagId tag)
    {
        if (!_tagStates.TryGetValue(tag.Value, out var state))
        {
            return;
        }

        if (!TryGetMask(state, chunkId, out ulong[]? mask, out int localIndex))
        {
            return;
        }

        SetBit(mask, slotIndex, false);
        if (IsEmpty(mask))
        {
            RemoveChunk(state, chunkId, localIndex);
        }
    }

    public bool HasTag(int chunkId, int slotIndex, TagId tag)
    {
        return _tagStates.TryGetValue(tag.Value, out var state)
               && TryGetMask(state, chunkId, out ulong[]? mask, out _)
               && GetBit(mask, slotIndex);
    }

    public void MoveSlotBits(int chunkId, int fromSlotIndex, int toSlotIndex)
    {
        if (fromSlotIndex == toSlotIndex)
        {
            return;
        }

        foreach (var state in _tagStates.Values)
        {
            if (!TryGetMask(state, chunkId, out ulong[]? mask, out int localIndex))
            {
                continue;
            }

            bool value = GetBit(mask, fromSlotIndex);
            SetBit(mask, toSlotIndex, value);
            SetBit(mask, fromSlotIndex, false);
            if (IsEmpty(mask))
            {
                RemoveChunk(state, chunkId, localIndex);
            }
        }
    }

    public void CopySlotTags(int sourceChunkId, int sourceSlotIndex, int targetChunkId, int targetSlotIndex)
    {
        foreach (var state in _tagStates)
        {
            var tagState = state.Value;
            bool sourceBit = TryGetMask(tagState, sourceChunkId, out ulong[]? sourceMask, out _)
                && GetBit(sourceMask, sourceSlotIndex);

            if (sourceBit)
            {
                ulong[] targetMask = GetOrAddChunk(tagState, targetChunkId);
                SetBit(targetMask, targetSlotIndex, true);
            }
            else if (TryGetMask(tagState, targetChunkId, out ulong[]? targetMaskExisting, out int local))
            {
                SetBit(targetMaskExisting, targetSlotIndex, false);
                if (IsEmpty(targetMaskExisting))
                {
                    RemoveChunk(tagState, targetChunkId, local);
                }
            }
        }
    }

    public void ClearSlot(int chunkId, int slotIndex)
    {
        foreach (var state in _tagStates.Values)
        {
            if (!TryGetMask(state, chunkId, out ulong[]? mask, out int localIndex))
            {
                continue;
            }

            SetBit(mask, slotIndex, false);
            if (IsEmpty(mask))
            {
                RemoveChunk(state, chunkId, localIndex);
            }
        }
    }

    public OverlayMaskResult BuildMask(QuerySpec query, int chunkId, int chunkSize, Span<ulong> destination)
    {
        if (_wordsPerChunk > destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }

        if (query.AllTags.Length > 0)
        {
            if (query.AllTags.Length == 1)
            {
                if (!GetOrDefaultMask(query.AllTags[0], chunkId, out ulong[]? mask))
                {
                    return OverlayMaskResult.None;
                }

                mask.AsSpan(0, _wordsPerChunk).CopyTo(destination);
            }
            else
            {
                FillFullMask(destination, chunkSize);
                for (int i = 0; i < query.AllTags.Length; i++)
                {
                    if (!GetOrDefaultMask(query.AllTags[i], chunkId, out ulong[]? mask))
                    {
                        return OverlayMaskResult.None;
                    }

                    for (int w = 0; w < _wordsPerChunk; w++)
                    {
                        destination[w] &= mask[w];
                    }
                }
            }
        }
        else
        {
            FillFullMask(destination, chunkSize);
        }

        if (query.AnyTags.Length > 0)
        {
            Span<ulong> anyMask = destination.Length >= _wordsPerChunk ? stackalloc ulong[_wordsPerChunk] : new ulong[_wordsPerChunk];
            anyMask.Clear();
            bool hadAny = false;
            for (int i = 0; i < query.AnyTags.Length; i++)
            {
                var tag = query.AnyTags[i];
                if (!GetOrDefaultMask(tag, chunkId, out ulong[]? mask))
                {
                    continue;
                }

                hadAny = true;
                for (int w = 0; w < _wordsPerChunk; w++)
                {
                    anyMask[w] |= mask[w];
                }
            }

            if (!hadAny)
            {
                return OverlayMaskResult.None;
            }

            for (int w = 0; w < _wordsPerChunk; w++)
            {
                destination[w] &= anyMask[w];
            }
        }

        if (query.NoneTags.Length > 0)
        {
            Span<ulong> noneMask = destination.Length >= _wordsPerChunk ? stackalloc ulong[_wordsPerChunk] : new ulong[_wordsPerChunk];
            noneMask.Clear();
            for (int i = 0; i < query.NoneTags.Length; i++)
            {
                var tag = query.NoneTags[i];
                if (!GetOrDefaultMask(tag, chunkId, out ulong[]? mask))
                {
                    continue;
                }

                for (int w = 0; w < _wordsPerChunk; w++)
                {
                    noneMask[w] |= mask[w];
                }
            }

            for (int w = 0; w < _wordsPerChunk; w++)
            {
                destination[w] &= ~noneMask[w];
            }
        }

        TrimToChunkSize(destination, chunkSize);

        bool hasActiveSlots = false;
        for (int w = 0; w < _wordsPerChunk; w++)
        {
            if (destination[w] != 0)
            {
                hasActiveSlots = true;
                break;
            }
        }

        if (!hasActiveSlots)
        {
            return OverlayMaskResult.None;
        }

        return IsFullMask(destination, chunkSize)
            ? OverlayMaskResult.Full
            : OverlayMaskResult.Partial;
    }

    private TagState GetOrCreateState(TagId tag)
    {
        if (!_tagStates.TryGetValue(tag.Value, out var state))
        {
            state = new TagState();
            _tagStates[tag.Value] = state;
        }

        return state;
    }

    private ulong[] GetOrAddChunk(TagState state, int chunkId)
    {
        if (state.ChunkIndexById.TryGetValue(chunkId, out int existing))
        {
            return state.Masks[existing];
        }

        int index = state.ChunkIds.Count;
        state.ChunkIndexById[chunkId] = index;
        state.ChunkIds.Add(chunkId);
        ulong[] mask = new ulong[_wordsPerChunk];
        state.Masks.Add(mask);
        return mask;
    }

    private bool TryGetMask(TagState state, int chunkId, out ulong[] mask, out int localIndex)
    {
        if (state.ChunkIndexById.TryGetValue(chunkId, out localIndex))
        {
            mask = state.Masks[localIndex];
            return true;
        }

        mask = Array.Empty<ulong>();
        localIndex = -1;
        return false;
    }

    private bool GetOrDefaultMask(TagId tag, int chunkId, out ulong[] mask)
    {
        if (_tagStates.TryGetValue(tag.Value, out var state)
            && TryGetMask(state, chunkId, out mask, out _))
        {
            return true;
        }

        mask = Array.Empty<ulong>();
        return false;
    }

    private void RemoveChunk(TagState state, int chunkId, int localIndex)
    {
        int lastIndex = state.ChunkIds.Count - 1;
        if (localIndex == lastIndex)
        {
            state.ChunkIds.RemoveAt(lastIndex);
            state.Masks.RemoveAt(lastIndex);
            state.ChunkIndexById.Remove(chunkId);
            return;
        }

        int replacementChunk = state.ChunkIds[lastIndex];
        state.ChunkIds[localIndex] = replacementChunk;
        state.Masks[localIndex] = state.Masks[lastIndex];
        state.ChunkIndexById[replacementChunk] = localIndex;

        state.ChunkIds.RemoveAt(lastIndex);
        state.Masks.RemoveAt(lastIndex);
        state.ChunkIndexById.Remove(chunkId);
    }

    private bool IsEmpty(ulong[] mask)
    {
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool GetBit(ReadOnlySpan<ulong> words, int slotIndex)
    {
        int word = slotIndex >> 6;
        ulong bit = 1UL << (slotIndex & 63);
        return (words[word] & bit) != 0;
    }

    private static bool GetBit(ulong[] words, int slotIndex) => GetBit((ReadOnlySpan<ulong>)words, slotIndex);

    private static void SetBit(ulong[] words, int slotIndex, bool value)
    {
        int word = slotIndex >> 6;
        ulong bit = 1UL << (slotIndex & 63);
        if (value)
        {
            words[word] |= bit;
        }
        else
        {
            words[word] &= ~bit;
        }
    }

    private void FillFullMask(Span<ulong> destination, int chunkSize)
    {
        for (int i = 0; i < _wordsPerChunk; i++)
        {
            destination[i] = 0;
        }

        int fullWords = chunkSize >> 6;
        for (int i = 0; i < fullWords; i++)
        {
            destination[i] = ulong.MaxValue;
        }

        int remainingBits = chunkSize & 63;
        if (remainingBits > 0)
        {
            destination[fullWords] = (1UL << remainingBits) - 1UL;
        }
    }

    private bool IsFullMask(ReadOnlySpan<ulong> words, int chunkSize)
    {
        if (chunkSize == 0)
        {
            return false;
        }

        int fullWords = chunkSize >> 6;
        for (int i = 0; i < fullWords; i++)
        {
            if (words[i] != ulong.MaxValue)
            {
                return false;
            }
        }

        int remainingBits = chunkSize & 63;
        return remainingBits == 0 || words[fullWords] == (1UL << remainingBits) - 1UL;
    }

    private void TrimToChunkSize(Span<ulong> destination, int chunkSize)
    {
        int usedWords = (chunkSize + 63) / 64;
        for (int i = usedWords; i < _wordsPerChunk; i++)
        {
            destination[i] = 0;
        }

        if (usedWords > 0 && (chunkSize & 63) != 0)
        {
            destination[usedWords - 1] &= (1UL << (chunkSize & 63)) - 1UL;
        }
    }
}
