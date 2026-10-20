namespace Delta.ECS.Tests;

using NUnit.Framework;

[TestFixture]
internal sealed class EntityIdentityTests
{
    [Test]
    public void EntityUsesIndexAndGenerationIdentity()
    {
        var first = new Entity(7, 3);
        var same = new Entity(7, 3);
        var nextGeneration = new Entity(7, 4);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(nextGeneration));
            Assert.That(first.Index, Is.EqualTo(7));
            Assert.That(first.Generation, Is.EqualTo(3));
        });
    }

}
