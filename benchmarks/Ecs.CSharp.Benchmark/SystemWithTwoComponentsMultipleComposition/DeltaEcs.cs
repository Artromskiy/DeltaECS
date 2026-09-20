using System.Runtime.CompilerServices;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECS()
        {
            _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    DeltaOperations.Update(ref first, in second));
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSParallel()
        {
            _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    DeltaOperations.Update(ref first, in second),
                workerCount: DeltaOperations.ParallelWorkerCount);
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSFunctor()
        {
            var functor = new DeltaComponent2Functor();
            _deltaEcs.World.ForEach(in _deltaEcs.Query, ref functor);
            DeltaOperations.LastFunctorCount = functor.Count;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSFunctorParallel()
        {
            var functor = new DeltaComponent2Functor();
            _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, ref functor, workerCount: DeltaOperations.ParallelWorkerCount);
            DeltaOperations.LastFunctorCount = functor.Count;
        }
    }
}
