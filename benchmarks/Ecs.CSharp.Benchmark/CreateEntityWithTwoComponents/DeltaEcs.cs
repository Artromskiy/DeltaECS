using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        [Context]
        private readonly DeltaCreateTwoContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECS()
        {
            for (int i = 0; i < EntityCount; i++)
            {
                _deltaEcs.World.Create(_deltaEcs.Components);
            }

            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECSBatch)]
        [Benchmark]
        public int DeltaECSBatch()
        {
            _deltaEcs.World.Create(_deltaEcs.Components, EntityCount);
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECSBatch)]
        [Benchmark]
        public int DeltaECSBatchGeneric()
        {
            _deltaEcs.World.Create<DeltaComponent1, DeltaComponent2>(EntityCount);
            return EntityCount;
        }
    }
}
