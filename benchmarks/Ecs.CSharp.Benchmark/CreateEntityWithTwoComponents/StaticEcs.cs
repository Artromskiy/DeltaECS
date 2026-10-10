using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.StaticEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        [Context]
        private readonly StaticEcsCreateContext _staticEcs;

        [BenchmarkCategory(Categories.StaticEcs)]
        [Benchmark]
        public int StaticEcs()
        {
            for (int index = 0; index < EntityCount; index++)
            {
                StaticEcsWorld.NewEntity<FFS.Libraries.StaticEcs.Default>().Set(new Component1(), new Component2());
            }

            return EntityCount;
        }

        [BenchmarkCategory(Categories.StaticEcsBatch)]
        [Benchmark]
        public int StaticEcsBatch()
        {
            StaticEcsWorld.NewEntities<FFS.Libraries.StaticEcs.Default, Component1, Component2>((uint)EntityCount);
            return EntityCount;
        }
    }
}
