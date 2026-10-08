using System;
using Delta.ECS;
using DeltaEntity = Delta.ECS.Entity;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark
{
    internal static class DeltaEcsSmoke
    {
        internal static void Run()
        {
            BenchmarkConfiguration.EntityCount = 32;
            using DeltaCreateOneContext createOne = new();
            createOne.World.Create(createOne.Components, 32, new DeltaEntity[32]);
            if (createOne.World.AliveEntityCount != 32)
            {
                throw new InvalidOperationException("Create-one smoke did not retain every entity.");
            }

            using DeltaCreateTwoContext createTwo = new();
            createTwo.World.Create(createTwo.Components, 32, new DeltaEntity[32]);

            using DeltaCreateThreeContext createThree = new();
            createThree.World.Create(createThree.Components, 32, new DeltaEntity[32]);

            using DeltaSystemOneContext one = new(32, 1);
            one.World.ForEach(in one.Query, static (ref DeltaComponent1 component) => ++component.Value).Invoke();
            one.World.ForEachParallel(in one.Query, static (ref DeltaComponent1 component) => ++component.Value).Invoke();
            one.World.ForEach(in one.Query, new DeltaComponent1Functor()).Invoke();
            one.World.ForEachParallel(in one.Query, new DeltaComponent1Functor()).Invoke();
            one.World.ForEachEntity(
                in one.Query,
                entity =>
                {
                    ref DeltaComponent1 component = ref entity.GetRef<DeltaComponent1>(one.Component);
                    component.Value++;
                }).Invoke();
            one.World.ForEachEntity(in one.Query, new DeltaEntityRefFunctor(one.Component)).Invoke();
            one.World.ForEachEntityParallel(
                in one.Query,
                entity =>
                {
                    ref DeltaComponent1 component = ref entity.GetRef<DeltaComponent1>(one.Component);
                    component.Value++;
                },
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            one.World.ForEachEntityParallel(
                in one.Query,
                new DeltaEntityRefFunctor(one.Component),
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();

            using DeltaSystemTwoContext two = new(32, 1);
            two.World.ForEach(
                in two.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value).Invoke();
            two.World.ForEachParallel(
                in two.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value).Invoke();
            two.World.ForEach(in two.Query, new DeltaComponent2Functor()).Invoke();
            two.World.ForEachParallel(in two.Query, new DeltaComponent2Functor()).Invoke();
            two.World.ForEachEntity(
                in two.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(two.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(two.Second);
                    first.Value += second.Value;
                }).Invoke();
            two.World.ForEachEntity(in two.Query, new DeltaEntityRefTwoComponentFunctor(two.First, two.Second)).Invoke();
            two.World.ForEachEntityParallel(
                in two.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(two.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(two.Second);
                    first.Value += second.Value;
                },
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            two.World.ForEachEntityParallel(
                in two.Query,
                new DeltaEntityRefTwoComponentFunctor(two.First, two.Second),
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();

            using DeltaSystemThreeContext three = new(32, 1);
            three.World.ForEach(
                in three.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value).Invoke();
            three.World.ForEachParallel(
                in three.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value).Invoke();
            three.World.ForEach(in three.Query, new DeltaComponent3Functor()).Invoke();
            three.World.ForEachParallel(in three.Query, new DeltaComponent3Functor()).Invoke();
            three.World.ForEachEntity(
                in three.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(three.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(three.Second);
                    ref DeltaComponent3 third = ref entity.GetRef<DeltaComponent3>(three.Third);
                    first.Value += second.Value + third.Value;
                }).Invoke();
            three.World.ForEachEntity(
                in three.Query,
                new DeltaEntityRefThreeComponentFunctor(three.First, three.Second, three.Third)).Invoke();
            three.World.ForEachEntityParallel(
                in three.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(three.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(three.Second);
                    ref DeltaComponent3 third = ref entity.GetRef<DeltaComponent3>(three.Third);
                    first.Value += second.Value + third.Value;
                },
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            three.World.ForEachEntityParallel(
                in three.Query,
                new DeltaEntityRefThreeComponentFunctor(three.First, three.Second, three.Third),
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();

            using DeltaSystemMultipleCompositionContext compositions = new(32);
            compositions.World.ForEach(
                in compositions.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value).Invoke();
            compositions.World.ForEachParallel(
                in compositions.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value).Invoke();
            compositions.World.ForEach(in compositions.Query, new DeltaComponent2Functor()).Invoke();
            compositions.World.ForEachParallel(in compositions.Query, new DeltaComponent2Functor()).Invoke();
            compositions.World.ForEachEntity(
                in compositions.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(compositions.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(compositions.Second);
                    first.Value += second.Value;
                }).Invoke();
            compositions.World.ForEachEntity(
                in compositions.Query,
                new DeltaEntityRefTwoComponentFunctor(compositions.First, compositions.Second)).Invoke();
            compositions.World.ForEachEntityParallel(
                in compositions.Query,
                entity =>
                {
                    ref DeltaComponent1 first = ref entity.GetRef<DeltaComponent1>(compositions.First);
                    ref DeltaComponent2 second = ref entity.GetRef<DeltaComponent2>(compositions.Second);
                    first.Value += second.Value;
                },
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            compositions.World.ForEachEntityParallel(
                in compositions.Query,
                new DeltaEntityRefTwoComponentFunctor(compositions.First, compositions.Second),
                workerCount: ParallelContext.ParallelWorkerCount).Invoke();
            if (compositions.World.AliveEntityCount != 32)
            {
                throw new InvalidOperationException("Composition smoke did not retain every entity.");
            }

            Console.WriteLine("DeltaECS full-fork contract smoke passed.");
        }
    }
}
