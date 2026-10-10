using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.StaticEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithTwoComponentsMultipleComposition
    {
        [Context]
        private readonly StaticEcsSystemMultipleCompositionContext _staticEcs;

        [BenchmarkCategory(Categories.StaticEcs, Categories.SingleThread)]
        [Benchmark]
        public int StaticEcsBlock()
        {
            StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().For(default(StaticEcsBlockTwo));
            return EntityCount;
        }

        [BenchmarkCategory(Categories.StaticEcs, Categories.MultiThread)]
        [Benchmark]
        public int StaticEcsBlockParallel()
        {
            StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().ForParallel(
                default(StaticEcsBlockTwo),
                workersLimit: (uint)ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }
    }
}
