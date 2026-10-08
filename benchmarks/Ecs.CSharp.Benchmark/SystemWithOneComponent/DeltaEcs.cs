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
        private EcsOperation? _iteration;
        private EcsOperation? _parallelIteration;
        private EcsOperation<DeltaComponent1Functor>? _functorIteration;
        private EcsOperation<DeltaComponent1Functor>? _parallelFunctorIteration;
        private EcsOperation? _entityRefParallelIteration;
        private EcsOperation<DeltaEntityRefFunctor>? _entityRefFunctorParallelIteration;

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECS()
        {
            _iteration ??= _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => ++component.Value);
            _iteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _parallelIteration ??= _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => ++component.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            _parallelIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _functorIteration ??= _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent1Functor());
            _functorIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _parallelFunctorIteration ??= _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent1Functor(), workerCount: ParallelContext.ParallelWorkerCount);
            _parallelFunctorIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSEntityRefParallel()
        {
            _entityRefParallelIteration ??= _deltaEcs.World.ForEachEntityParallel(
                in _deltaEcs.Query,
                entity =>
                {
                    ref DeltaComponent1 component = ref entity.GetRef<DeltaComponent1>(_deltaEcs.Component);
                    component.Value++;
                },
                workerCount: ParallelContext.ParallelWorkerCount);
            _entityRefParallelIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSEntityRefFunctorParallel()
        {
            _entityRefFunctorParallelIteration ??= _deltaEcs.World.ForEachEntityParallel(
                in _deltaEcs.Query,
                new DeltaEntityRefFunctor(_deltaEcs.Component),
                workerCount: ParallelContext.ParallelWorkerCount);
            _entityRefFunctorParallelIteration.Invoke();
            return EntityCount;
        }
    }
}
