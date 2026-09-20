using System.Runtime.CompilerServices;
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

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECS()
        {
            _deltaEcs.World.ForEach(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => DeltaOperations.Update(ref component));
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSParallel()
        {
            _deltaEcs.World.ForEachParallel(
                in _deltaEcs.Query,
                static (ref DeltaComponent1 component) => DeltaOperations.Update(ref component),
                workerCount: DeltaOperations.ParallelWorkerCount);
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSFunctor()
        {
            var functor = new DeltaComponent1Functor();
            _deltaEcs.World.ForEach(in _deltaEcs.Query, ref functor);
            DeltaOperations.LastFunctorCount = functor.Count;
        }

        [BenchmarkCategory(Categories.DeltaECS)]
        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeltaECSFunctorParallel()
        {
            var functor = new DeltaComponent1Functor();
            _deltaEcs.World.ForEachParallel(in _deltaEcs.Query, ref functor, workerCount: DeltaOperations.ParallelWorkerCount);
            DeltaOperations.LastFunctorCount = functor.Count;
        }
    }
}
