using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithThreeComponents
    {
        [Context]
        private readonly DeltaSystemThreeContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECS()
        {
            _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value);
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent3Functor());
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent3Functor(), workerCount: ParallelContext.ParallelWorkerCount);
            return EntityCount;
        }
    }
}
