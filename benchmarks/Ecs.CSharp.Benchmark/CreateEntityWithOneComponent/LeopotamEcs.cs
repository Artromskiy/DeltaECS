using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Leopotam.Ecs;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithOneComponent
    {
        [Context]
        private readonly LeopotamEcsBaseContext _leopotamEcs;

        [BenchmarkCategory(Categories.LeopotamEcs)]
        [Benchmark]
        public int LeopotamEcs()
        {
            for (int i = 0; i < EntityCount; ++i)
            {
                _leopotamEcs.World.NewEntity()
                    .Replace(new LeopotamEcsBaseContext.Component1());
            }
            return EntityCount;
        }
    }
}
