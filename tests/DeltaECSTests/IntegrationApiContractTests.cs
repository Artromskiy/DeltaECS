namespace Delta.ECS.Tests;

using System;
using System.Linq;
using Delta.ECS.Integration;
using NUnit.Framework;

[TestFixture]
public sealed class IntegrationApiContractTests
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

    [Test]
    public void ComponentCapabilities_ComposeReadAndWrite()
    {
        ComponentCapabilities capabilities = ComponentCapabilities.Read | ComponentCapabilities.Write;

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.HasFlag(ComponentCapabilities.Read), Is.True);
            Assert.That(capabilities.HasFlag(ComponentCapabilities.Write), Is.True);
        });
    }

    [Test]
    public void ReadAndWriteErrors_ExposeDetailedSymmetricFailures()
    {
        string[] commonReadErrors =
        [
            nameof(EcsReadErrorCode.None),
            nameof(EcsReadErrorCode.EntityNotAlive),
            nameof(EcsReadErrorCode.ComponentUnknown),
            nameof(EcsReadErrorCode.ComponentMissing),
            nameof(EcsReadErrorCode.Unsupported)
        ];

        string[] writeErrors = Enum.GetNames<EcsWriteErrorCode>();

        Assert.That(commonReadErrors, Is.SubsetOf(writeErrors));
        Assert.That(writeErrors, Does.Contain(nameof(EcsWriteErrorCode.StaleStamp)));
        Assert.That(writeErrors, Does.Contain(nameof(EcsWriteErrorCode.InvalidValue)));
    }

    [Test]
    public void Snapshot_MayPreserveMutableReferenceIdentity()
    {
        var value = new MutableReference();
        var snapshot = new ComponentSnapshot(value, default);

        Assert.That(snapshot.Value, Is.SameAs(value));
    }

    [Test]
    public void WorldContract_UsesOneUnifiedInterface()
    {
        Type contract = typeof(IEcsWorld);
        string[] methodNames = contract.GetMethods().Select(static method => method.Name).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(contract.GetProperty(nameof(IEcsWorld.Stamp)), Is.Not.Null);
            Assert.That(contract.GetProperty(nameof(IEcsWorld.Catalog)), Is.Not.Null);
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Initialize)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Update)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Shutdown)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.IsAlive)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Create)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Destroy)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Add)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.Remove)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.TryGetComponents)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.TryRead)));
            Assert.That(methodNames, Does.Contain(nameof(IEcsWorld.TryWrite)));
        });
    }

    private sealed class MutableReference;
}
