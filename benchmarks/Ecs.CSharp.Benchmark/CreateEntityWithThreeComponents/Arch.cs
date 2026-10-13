using Arch.Core;

using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.ArchComponents;

namespace Ecs.CSharp.Benchmark;

public partial class CreateEntityWithThreeComponents
{
    private static readonly ComponentType[] _archetype = [typeof(Component1), typeof(Component2), typeof(Component3)];

    [Context]
    private readonly ArchBaseContext _arch;

    [BenchmarkCategory(Categories.Arch)]
    [Benchmark]
    public int Arch()
    {
        Arch.Core.World world = _arch.World;
        _ = world.EnsureCapacity(new Signature(_archetype), EntityCount);

        for (int i = 0; i < EntityCount; ++i)
        {
            _ = world.Create(_archetype);
        }
        return EntityCount;
    }
}
