namespace Delta.ECS.Tests;

using System;
using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class ChunkActiveTagTests
{
    [Test]
    public void LastRemovalCapacityGrowthAndClearRefreshTheOrderedIndex()
    {
        var tags = new ChunkTagMasks();
        tags.EnsureTagCapacity(130);
        tags.Set(129, 1023);
        tags.Set(65, 64);
        tags.Set(1, 0);
        tags.Set(65, 63);
        AssertActive(tags, 1, 65, 129);
        tags.Clear(65, 64);
        AssertActive(tags, 1, 65, 129);
        tags.ClearRange(63, 1);
        AssertActive(tags, 1, 129);
        tags.EnsureTagCapacity(200);
        AssertActive(tags, 1, 129);
        tags.ClearAll();
        Assert.That(tags.GetActiveTagIndices().Length, Is.Zero);
        tags.Set(180, 11);
        AssertActive(tags, 180);
    }

    [Test]
    public void SlotAndRangeCopiesClearTagsAbsentFromNarrowerSource()
    {
        var source = new ChunkTagMasks();
        var target = new ChunkTagMasks();
        source.EnsureTagCapacity(2);
        target.EnsureTagCapacity(130);
        source.Set(1, 0);
        target.Set(129, 63);
        source.CopySlotTo(target, 0, 63);
        AssertActive(target, 1);
        target.Set(128, 64);
        source.CopyRangeTo(target, 0, 64, 65);
        AssertActive(target, 1);
        target.ClearRange(63, 2);
        Assert.That(target.GetActiveTagIndices().Length, Is.Zero);
    }

    [Test]
    public void RandomizedOperationsMatchMembershipModelIncludingOverlappingCopies()
    {
        const int tagCount = 70;
        var masks = new[] { new ChunkTagMasks(), new ChunkTagMasks() };
        var model = new[] { new bool[tagCount, Chunk.Capacity], new bool[tagCount, Chunk.Capacity] };
        foreach (ChunkTagMasks mask in masks)
        {
            mask.EnsureTagCapacity(tagCount);
        }
        var random = new Random(1791);
        for (int step = 0; step < 400; step++)
        {
            int source = random.Next(2);
            int target = random.Next(2);
            int tag = random.Next(tagCount);
            int slot = random.Next(Chunk.Capacity);
            switch (random.Next(6))
            {
                case 0:
                    masks[source].Set(tag, slot);
                    model[source][tag, slot] = true;
                    break;
                case 1:
                    masks[source].Clear(tag, slot);
                    model[source][tag, slot] = false;
                    break;
                case 2:
                    int destination = random.Next(Chunk.Capacity);
                    masks[source].CopySlotTo(masks[target], slot, destination);
                    for (int i = 0; i < tagCount; i++)
                    {
                        model[target][i, destination] = model[source][i, slot];
                    }
                    break;
                case 3:
                    int count = random.Next(1, 98);
                    int from = random.Next(Chunk.Capacity - count + 1);
                    int to = random.Next(Chunk.Capacity - count + 1);
                    var original = (bool[,])model[source].Clone();
                    masks[source].CopyRangeTo(masks[target], from, to, count);
                    for (int i = 0; i < tagCount; i++)
                    {
                        for (int j = 0; j < count; j++)
                        {
                            model[target][i, to + j] = original[i, from + j];
                        }
                    }
                    break;
                case 4:
                    int length = random.Next(1, Chunk.Capacity - slot + 1);
                    masks[source].ClearRange(slot, length);
                    for (int i = 0; i < tagCount; i++)
                    {
                        for (int j = slot; j < slot + length; j++)
                        {
                            model[source][i, j] = false;
                        }
                    }
                    break;
                default:
                    masks[source].ClearAll();
                    Array.Clear(model[source], 0, model[source].Length);
                    break;
            }
            for (int buffer = 0; buffer < masks.Length; buffer++)
            {
                var expected = new List<int>();
                for (int i = 0; i < tagCount; i++)
                {
                    for (int j = 0; j < Chunk.Capacity; j++)
                    {
                        if (model[buffer][i, j])
                        {
                            expected.Add(i);
                            break;
                        }
                    }
                }
                Assert.That(masks[buffer].GetActiveTagIndices().ToArray(), Is.EqualTo(expected), $"step {step}, buffer {buffer}");
            }
        }
    }
    private static void AssertActive(ChunkTagMasks tags, params int[] expected)
        => Assert.That(tags.GetActiveTagIndices().ToArray(), Is.EqualTo(expected));

}
