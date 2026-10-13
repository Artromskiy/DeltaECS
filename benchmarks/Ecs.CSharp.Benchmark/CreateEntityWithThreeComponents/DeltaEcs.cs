using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark;

public partial class CreateEntityWithThreeComponents
{
    [Context]
    private readonly DeltaCreateThreeContext _deltaEcs;

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    public int DeltaECS()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _ = _deltaEcs.World.Create(_deltaEcs.Components);
        }

        return EntityCount;
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public int DeltaECSBatch()
    {
        _ = _deltaEcs.World.Create(_deltaEcs.Components, EntityCount);
        return EntityCount;
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public int DeltaECSBatchGeneric()
    {
        _ = _deltaEcs.World.Create<DeltaComponent1, DeltaComponent2, DeltaComponent3>(EntityCount);
        return EntityCount;
    }
}
