namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentChangeMasksSpanTests
{
    [Test]
    public void CopyBitsPreservesBitsOutsideUnalignedRange()
    {
        ulong[] source = CreateMask(5);
        ulong[] actual = CreateMask(5);
        ulong[] expected = (ulong[])actual.Clone();

        CopyExpected(source, 59, expected, 63, 130, merge: false);
        ComponentChangeMasks.CopyBits(source.AsSpan(), 59, actual.AsSpan(), 63, 130);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(17, 73)]
    [TestCase(73, 17)]
    public void CopyBitsSupportsOverlappingSpans(int sourceSlot, int targetSlot)
    {
        ulong[] actual = CreateMask(5);
        ulong[] expected = (ulong[])actual.Clone();

        CopyExpected(expected, sourceSlot, expected, targetSlot, 150, merge: false);
        ComponentChangeMasks.CopyBits(actual.AsSpan(), sourceSlot, actual.AsSpan(), targetSlot, 150);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void MergeRangeSupportsAliasedSlices()
    {
        ulong[] actual = CreateMask(6);
        ulong[] expected = (ulong[])actual.Clone();
        ReadOnlySpan<ulong> source = actual.AsSpan(0, 5);
        Span<ulong> target = actual.AsSpan(1, 5);

        MergeExpected(expected, 7, 71, 180);
        ComponentChangeMasks.MergeRange(source, 7, target, 7, 180);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void SetRangeOnlyChangesTheSuppliedSpan()
    {
        ulong[] actual = CreateMask(5);
        ulong[] expected = (ulong[])actual.Clone();
        Span<ulong> mask = actual.AsSpan(1, 3);

        SetExpected(expected.AsSpan(1, 3), 58, 70, true);
        ComponentChangeMasks.SetRange(mask, 58, 70, value: true);

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static ulong[] CreateMask(int words)
    {
        ulong[] mask = new ulong[words];
        for (int index = 0; index < words; index++)
        {
            mask[index] = 0x9E37_79B9_7F4A_7C15UL * (uint)(index + 1);
        }

        return mask;
    }

    private static void CopyExpected(ulong[] source, int sourceSlot, ulong[] target, int targetSlot, int count, bool merge)
    {
        bool[] copied = new bool[count];
        for (int index = 0; index < count; index++)
        {
            copied[index] = GetBit(source, sourceSlot + index);
        }

        for (int index = 0; index < count; index++)
        {
            if (!merge || copied[index])
            {
                SetBit(target, targetSlot + index, copied[index]);
            }
        }
    }

    private static void MergeExpected(ulong[] mask, int sourceSlot, int targetSlot, int count)
        => CopyExpected(mask, sourceSlot, mask, targetSlot, count, merge: true);

    private static void SetExpected(Span<ulong> mask, int start, int count, bool value)
    {
        for (int index = 0; index < count; index++)
        {
            SetBit(mask, start + index, value);
        }
    }

    private static bool GetBit(ReadOnlySpan<ulong> mask, int index)
        => (mask[index >> 6] & (1UL << (index & 63))) != 0;

    private static void SetBit(Span<ulong> mask, int index, bool value)
    {
        ulong bit = 1UL << (index & 63);
        ref ulong word = ref mask[index >> 6];
        word = value ? word | bit : word & ~bit;
    }
}
