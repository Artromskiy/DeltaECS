using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithThreeComponents
    {
        [Context]
        private readonly DeltaSystemThreeContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECS()
        {
            _deltaEcs.Iteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _deltaEcs.ParallelIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _deltaEcs.FunctorIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _deltaEcs.ParallelFunctorIteration.Invoke();
            return EntityCount;
        }
    }
}
