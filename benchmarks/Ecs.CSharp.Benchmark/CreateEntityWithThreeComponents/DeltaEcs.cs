using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithThreeComponents
    {
        [Context]
        private readonly DeltaCreateThreeContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECS()
        {
            for (int i = 0; i < EntityCount; i++)
            {
                _deltaEcs.World.Create(_deltaEcs.Components);
            }
        }

        [BenchmarkCategory(Categories.DeltaECSBatch)]
        [Benchmark]
        public void DeltaECSBatch() => _deltaEcs.World.Create(_deltaEcs.Components, EntityCount);

        [BenchmarkCategory(Categories.DeltaECSBatch)]
        [Benchmark]
        public void DeltaECSBatchGeneric() => _deltaEcs.World.Create<DeltaComponent1, DeltaComponent2, DeltaComponent3>(EntityCount);
    }
}
