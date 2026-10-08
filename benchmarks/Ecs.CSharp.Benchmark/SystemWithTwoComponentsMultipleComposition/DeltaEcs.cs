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
        private EcsOperation? _iteration;
        private EcsOperation? _parallelIteration;
        private EcsOperation<DeltaComponent2Functor>? _functorIteration;
        private EcsOperation<DeltaComponent2Functor>? _parallelFunctorIteration;
        private EcsOperation? _entityRefParallelIteration;
        private EcsOperation<DeltaEntityRefTwoComponentFunctor>? _entityRefFunctorParallelIteration;

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECS()
        {
            _iteration ??= _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value);
            _iteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _parallelIteration ??= _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            _parallelIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _functorIteration ??= _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent2Functor());
            _functorIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _parallelFunctorIteration ??= _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent2Functor(), workerCount: ParallelContext.ParallelWorkerCount);
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
                    if (!entity.TryGet(_deltaEcs.Second, out DeltaComponent2 second))
                    {
                        return;
                    }

                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(_deltaEcs.First);
                    first.Value += second.Value;
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
                new DeltaEntityRefTwoComponentFunctor(_deltaEcs.First, _deltaEcs.Second),
                workerCount: ParallelContext.ParallelWorkerCount);
            _entityRefFunctorParallelIteration.Invoke();
            return EntityCount;
        }
    }
}
