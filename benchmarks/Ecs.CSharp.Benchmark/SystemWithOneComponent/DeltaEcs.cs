using BenchmarkDotNet.Attributes;
using Delta.ECS;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithOneComponent
    {
        [Context]
        private readonly DeltaSystemOneContext _deltaEcs;

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECS()
        {
            _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => ++component.Value).Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => ++component.Value,
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent1Functor()).Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent1Functor(), workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            return EntityCount;
        }
    }
}
