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
        private EcsOperation? _iteration;
        private EcsOperation? _parallelIteration;
        private EcsOperation<DeltaComponent3Functor>? _functorIteration;
        private EcsOperation<DeltaComponent3Functor>? _parallelFunctorIteration;
        private EcsOperation? _entityRefParallelIteration;
        private EcsOperation<DeltaEntityRefThreeComponentFunctor>? _entityRefFunctorParallelIteration;

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECS()
        {
            _iteration ??= _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value);
            _iteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSParallel()
        {
            _parallelIteration ??= _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            _parallelIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.SingleThread)]
        [Benchmark]
        public int DeltaECSFunctor()
        {
            _functorIteration ??= _deltaEcs.World.ForEach(in _deltaEcs.Query, new DeltaComponent3Functor());
            _functorIteration.Invoke();
            return EntityCount;
        }

        [BenchmarkCategory(Categories.DeltaECS, Categories.MultiThread)]
        [Benchmark]
        public int DeltaECSFunctorParallel()
        {
            _parallelFunctorIteration ??= _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, new DeltaComponent3Functor(), workerCount: ParallelContext.ParallelWorkerCount);
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
                    if (!entity.TryGet(_deltaEcs.Second, out DeltaComponent2 second)
                        || !entity.TryGet(_deltaEcs.Third, out DeltaComponent3 third))
                    {
                        return;
                    }

                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(_deltaEcs.First);
                    first.Value += second.Value + third.Value;
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
                new DeltaEntityRefThreeComponentFunctor(_deltaEcs.First, _deltaEcs.Second, _deltaEcs.Third),
                workerCount: ParallelContext.ParallelWorkerCount);
            _entityRefFunctorParallelIteration.Invoke();
            return EntityCount;
        }
    }
}
