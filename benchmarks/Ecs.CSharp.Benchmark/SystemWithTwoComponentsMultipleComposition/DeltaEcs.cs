using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithTwoComponentsMultipleComposition
    {
        [Context]
        private readonly DeltaSystemMultipleCompositionContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECS()
        {
            _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value);
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent2Functor());
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent2Functor(), workerCount: ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }
    }
}
