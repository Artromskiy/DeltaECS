using System;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.StaticEcsComponents;
using FFS.Libraries.StaticEcs;

namespace Ecs.CSharp.Benchmark
{
    internal static class StaticEcsSmoke
    {
        private struct Component1Sum : StaticEcsWorld.IQuery.Read<Component1>
        {
            internal int Value;

            public void Invoke(StaticEcsWorld.Entity entity, in Component1 component) => Value += component.Value;
        }

        internal static void Run()
        {
            int previousWorkerCount = ParallelContext.ParallelWorkerCount;
            ParallelContext.ParallelWorkerCount = 1;
            try
            {
                using (new StaticEcsCreateContext())
                {
                    StaticEcsWorld.NewEntities<Default, Component1>(8);
                    StaticEcsWorld.NewEntities<Default, Component1, Component2>(8);
                    StaticEcsWorld.NewEntities<Default, Component1, Component2, Component3>(8);
                    if (StaticEcsWorld.CalculateEntitiesCount() != 24)
                    {
                        throw new InvalidOperationException("StaticEcs batch creation smoke did not retain every entity.");
                    }
                }

                using (new StaticEcsSystemOneContext(8, 1))
                {
                    StaticEcsWorld.Query().WriteBlock<Component1>().For(default(StaticEcsBlockOne));
                    StaticEcsWorld.Query().WriteBlock<Component1>().ForParallel(
                        default(StaticEcsBlockOne),
                        workersLimit: 1);
                    AssertComponent1Sum(16);
                }

                using (new StaticEcsSystemTwoContext(8, 1))
                {
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().For(default(StaticEcsBlockTwo));
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().ForParallel(
                        default(StaticEcsBlockTwo),
                        workersLimit: 1);
                    AssertComponent1Sum(16);
                }

                using (new StaticEcsSystemThreeContext(8, 1))
                {
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2, Component3>().For(default(StaticEcsBlockThree));
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2, Component3>().ForParallel(
                        default(StaticEcsBlockThree),
                        workersLimit: 1);
                    AssertComponent1Sum(32);
                }

                using (new StaticEcsSystemMultipleCompositionContext(8))
                {
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().For(default(StaticEcsBlockTwo));
                    StaticEcsWorld.Query().WriteBlock<Component1>().Read<Component2>().ForParallel(
                        default(StaticEcsBlockTwo),
                        workersLimit: 1);
                    AssertComponent1Sum(16);
                }
            }
            finally
            {
                ParallelContext.ParallelWorkerCount = previousWorkerCount;
            }

            Console.WriteLine("StaticEcs block and batch-creation contract smoke passed.");
        }

        private static void AssertComponent1Sum(int expected)
        {
            Component1Sum sum = default;
            StaticEcsWorld.Query().Read<Component1>().For(ref sum);
            if (sum.Value != expected)
            {
                throw new InvalidOperationException($"StaticEcs query smoke expected component sum {expected}, got {sum.Value}.");
            }
        }
    }
}
