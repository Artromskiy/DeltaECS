namespace Delta.ECS.Tests;

using System.Collections.Generic;

using NUnit.Framework;

[TestFixture]
internal sealed class ArrayAccessTests
{
    [Test]
    public void AsSpanForListUsesCountAndBackingArray()
    {
        var values = new List<int>(8) { 3, 5, 7 };

        Span<int> span = ArrayAccess.AsSpan(values);
        span[1] = 11;

        Assert.That(span.Length, Is.EqualTo(3));
        Assert.That(values[1], Is.EqualTo(11));
    }
}
