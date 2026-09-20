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

            using DeltaCreateTwoContext createTwo = new();
            createTwo.World.Create(createTwo.Components, 32, new DeltaEntity[32]);

            using DeltaCreateThreeContext createThree = new();
            createThree.World.Create(createThree.Components, 32, new DeltaEntity[32]);

            using DeltaSystemOneContext one = new(32, 1);
            one.World.ForEach(in one.Query, static (ref DeltaComponent1 component) => DeltaOperations.Update(ref component));
            one.World.ForEachParallel(in one.Query, static (ref DeltaComponent1 component) => DeltaOperations.Update(ref component));
            var oneFunctor = new DeltaComponent1Functor();
            one.World.ForEach(in one.Query, ref oneFunctor);
            if (oneFunctor.Count != 32)
            {
                throw new InvalidOperationException("The sequential one-component functor did not visit every entity.");
            }
            one.World.ForEachParallel(in one.Query, ref oneFunctor);

            using DeltaSystemTwoContext two = new(32, 1);
            two.World.ForEach(in two.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
            two.World.ForEachParallel(in two.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
            var twoFunctor = new DeltaComponent2Functor();
            two.World.ForEach(in two.Query, ref twoFunctor);
            if (twoFunctor.Count != 32)
            {
                throw new InvalidOperationException("The sequential two-component functor did not visit every entity.");
            }
            two.World.ForEachParallel(in two.Query, ref twoFunctor);

            using DeltaSystemThreeContext three = new(32, 1);
            three.World.ForEach(in three.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) => DeltaOperations.Update(ref first, in second, in third));
            three.World.ForEachParallel(
                in three.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    DeltaOperations.Update(ref first, in second, in third));
            var threeFunctor = new DeltaComponent3Functor();
            three.World.ForEach(in three.Query, ref threeFunctor);
            if (threeFunctor.Count != 32)
            {
                throw new InvalidOperationException("The sequential three-component functor did not visit every entity.");
            }
            three.World.ForEachParallel(in three.Query, ref threeFunctor);

            using DeltaSystemMultipleCompositionContext compositions = new(32);
            compositions.World.ForEach(in compositions.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
            compositions.World.ForEachParallel(in compositions.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
            var compositionFunctor = new DeltaComponent2Functor();
            compositions.World.ForEach(in compositions.Query, ref compositionFunctor);
            if (compositionFunctor.Count != 32)
            {
                throw new InvalidOperationException("The sequential composition functor did not visit every entity.");
            }
            compositions.World.ForEachParallel(in compositions.Query, ref compositionFunctor);
            Console.WriteLine("DeltaECS full-fork contract smoke passed.");
        }
    }
}
