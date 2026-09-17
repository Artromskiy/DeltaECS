using BenchmarkDotNet.Attributes;
using DefaultEcs;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.TinyEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        [Context]
        private readonly TinyEcsBaseContext _tinyEcs;

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public void TinyEcs()
        {
            for (int i = 0; i < EntityCount; ++i)
            {
                _tinyEcs.World.Entity()
                    .Set(new Component1())
                    .Set(new Component2());
            }
        }
    }
}
