namespace Delta.ECS.Tests;

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using NUnit.Framework;

[TestFixture]
internal sealed class ComponentRowArrayPoolTests
{
    [Test]
    public void WorldDisposePoolCanReuseClearedRowsInAnotherWorldPool()
    {
        var firstPool = new ComponentRowArrayPool();
        var source = new PooledRow[Chunk.Capacity];
        source[0] = new PooledRow { Reference = new object(), Value = 42 };
        firstPool.Return(source);
        firstPool.Clear();

        var nextPool = new ComponentRowArrayPool();
        Array rented = nextPool.Rent(typeof(PooledRow), ComponentRowOperations.ForType<PooledRow>(), Chunk.Capacity);

        Assert.That(rented, Is.SameAs(source));
        Assert.That(((PooledRow[])rented)[0], Is.EqualTo(default(PooledRow)));
    }

    [Test]
    public void SharedPoolDoesNotRetainCollectibleComponentTypes()
    {
        WeakReference assembly = ReturnCollectibleRowsToPool();

        for (int attempt = 0; attempt < 10 && assembly.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.That(assembly.IsAlive, Is.False);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReturnCollectibleRowsToPool()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"CollectibleRows_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
        Type rowType = assembly.DefineDynamicModule("main")
            .DefineType("Row", TypeAttributes.Public | TypeAttributes.Sealed)
            .CreateType()!;
        var pool = new ComponentRowArrayPool();
        pool.Return(Array.CreateInstance(rowType, Chunk.Capacity));
        pool.Clear();

        return new WeakReference(assembly);
    }

    private struct PooledRow
    {
        internal object? Reference;

        internal int Value;
    }
}
