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
            one.World.ForEach(in one.Query, static (ref DeltaComponent1 component) => ++component.Value);
            one.World.ForEachParallel(in one.Query, static (ref DeltaComponent1 component) => ++component.Value);
            one.World.ForEach(in one.Query, new DeltaComponent1Functor());
            one.World.ForEachParallel(in one.Query, new DeltaComponent1Functor());

            using DeltaSystemTwoContext two = new(32, 1);
            two.World.ForEach(
                in two.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value);
            two.World.ForEachParallel(
                in two.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value);
            two.World.ForEach(in two.Query, new DeltaComponent2Functor());
            two.World.ForEachParallel(in two.Query, new DeltaComponent2Functor());

            using DeltaSystemThreeContext three = new(32, 1);
            three.World.ForEach(
                in three.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value);
            three.World.ForEachParallel(
                in three.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value);
            three.World.ForEach(in three.Query, new DeltaComponent3Functor());
            three.World.ForEachParallel(in three.Query, new DeltaComponent3Functor());

            using DeltaSystemMultipleCompositionContext compositions = new(32);
            compositions.World.ForEach(
                in compositions.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value);
            compositions.World.ForEachParallel(
                in compositions.Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => first.Value += second.Value);
            compositions.World.ForEach(in compositions.Query, new DeltaComponent2Functor());
            compositions.World.ForEachParallel(in compositions.Query, new DeltaComponent2Functor());
            if (compositions.World.AliveEntityCount != 32)
            {
                throw new InvalidOperationException("Composition smoke did not retain every entity.");
            }

            Console.WriteLine("DeltaECS full-fork contract smoke passed.");
        }
    }
}
