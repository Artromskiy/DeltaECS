using Arch.Core;

using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.ArchComponents;

namespace Ecs.CSharp.Benchmark;

public partial class CreateEntityWithOneComponent
{
    private static readonly ComponentType[] _archetype = [typeof(Component1)];

    [Context]
    private readonly ArchBaseContext _arch;

    [BenchmarkCategory(Categories.Arch)]
    [Benchmark]
    public int Arch()
    {
        World world = _arch.World;
        _ = world.EnsureCapacity(new Signature(_archetype), EntityCount);

        for (int i = 0; i < EntityCount; ++i)
        {
            _ = world.Create(_archetype);
        }
        return EntityCount;
    }
}
