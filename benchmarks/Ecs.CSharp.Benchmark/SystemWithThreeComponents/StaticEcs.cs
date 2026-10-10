using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.StaticEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithThreeComponents
    {
        [Context]
        private readonly StaticEcsSystemThreeContext _staticEcs;

        [BenchmarkCategory(Categories.StaticEcs, Categories.SingleThread)]
        [Benchmark]
        public int StaticEcsBlock()
        {
            StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2, Component3>().For(default(StaticEcsBlockThree));
            return EntityCount;
        }

        [BenchmarkCategory(Categories.StaticEcs, Categories.MultiThread)]
        [Benchmark]
        public int StaticEcsBlockParallel()
        {
            StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2, Component3>().ForParallel(
                default(StaticEcsBlockThree),
                workersLimit: (uint)ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }
    }
}
