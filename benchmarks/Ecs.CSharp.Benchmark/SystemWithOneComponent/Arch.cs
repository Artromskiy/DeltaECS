using System.Runtime.CompilerServices;

using Arch.Core;
using Arch.System;

using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.ArchComponents;

namespace Ecs.CSharp.Benchmark;

public partial class SystemWithOneComponent
{

    private static readonly ComponentType[] _filter = [typeof(Component1)];
    private static readonly QueryDescription _queryDescription = new(new Signature(_filter), null, null, null);

    [Context]
    private readonly ArchContext _arch;
    private ForEach1 _forEach;

    [BenchmarkCategory(Categories.Arch, Categories.SingleThread)]
    [Benchmark]
    public int ArchMonoThread()
    {
        World world = _arch.World;
        world.InlineQuery<ForEach1, Component1>(_queryDescription, ref _forEach);
        return EntityCount;
    }

    [BenchmarkCategory(Categories.Arch, Categories.SingleThread)]
    [Benchmark]
    public int ArchMonoThreadSourceGenerated()
    {
        ForEachQuery(_arch.World);
        return EntityCount;
    }

    [BenchmarkCategory(Categories.Arch, Categories.MultiThread)]
    [Benchmark]
    public int ArchMultiThread()
    {
        World world = _arch.World;
        world.InlineParallelQuery<ForEach1, Component1>(_queryDescription, ref _forEach);
        return EntityCount;
    }

    [Query]
    private static void ForEach(ref Component1 t0) => ++t0.Value;
    private struct ForEach1 : IForEach<Component1>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Update(ref Component1 t0) => ++t0.Value;
    }

    private sealed class ArchContext : ArchBaseContext
    {
        internal ArchContext(int entityCount, int _) : base(_filter, entityCount) { }
    }
}
