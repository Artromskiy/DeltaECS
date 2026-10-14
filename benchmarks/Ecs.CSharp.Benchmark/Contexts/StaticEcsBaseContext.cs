using System;

using FFS.Libraries.StaticEcs;

namespace Ecs.CSharp.Benchmark.Contexts
{
    namespace StaticEcsComponents
    {
        internal struct Component1 : IComponent
        {
            public int Value;
        }

        internal struct Component2 : IComponent
        {
            public int Value;
        }

        internal struct Component3 : IComponent
        {
            public int Value;
        }

        internal struct Padding : IComponent { }
        internal struct Composition1 : IComponent { }
        internal struct Composition2 : IComponent { }
        internal struct Composition3 : IComponent { }
        internal struct Composition4 : IComponent { }
    }

    internal abstract class StaticEcsBaseContext : IDisposable
    {
        protected StaticEcsBaseContext()
            : this(1, 0)
        { }

        protected StaticEcsBaseContext(int capacity, uint threadCount)
        {
            StaticEcsWorld.Create(new WorldConfig { ThreadCount = threadCount });
            _ = StaticEcsWorld.Types()
                .EntityType<Default>()
                .Component<StaticEcsComponents.Component1>()
                .Component<StaticEcsComponents.Component2>()
                .Component<StaticEcsComponents.Component3>()
                .Component<StaticEcsComponents.Padding>()
                .Component<StaticEcsComponents.Composition1>()
                .Component<StaticEcsComponents.Composition2>()
                .Component<StaticEcsComponents.Composition3>()
                .Component<StaticEcsComponents.Composition4>();
            StaticEcsWorld.Initialize((uint)Math.Max(1, capacity));
        }

        public void Dispose() => StaticEcsWorld.Destroy(withHooks: false);
    }

    internal struct StaticEcsWorldType : IWorldType { }

    internal abstract class StaticEcsWorld : World<StaticEcsWorldType> { }

    internal sealed class StaticEcsCreateContext : StaticEcsBaseContext
    {
        public StaticEcsCreateContext()
            : base(global::Ecs.CSharp.Benchmark.BenchmarkConfiguration.EntityCount, 0)
        { }
    }

    internal sealed class StaticEcsSystemOneContext : StaticEcsBaseContext
    {
        public StaticEcsSystemOneContext(int entityCount, int padding)
            : base(entityCount * (padding + 1), (uint)ParallelContext.ParallelWorkerCount)
        {
            for (int index = 0; index < entityCount; index++)
            {
                for (int pad = 0; pad < padding; pad++)
                {
                    _ = StaticEcsWorld.NewEntity<Default>();
                }

                _ = StaticEcsWorld.NewEntity<Default>().Set(new StaticEcsComponents.Component1());
            }
        }
    }

    internal sealed class StaticEcsSystemTwoContext : StaticEcsBaseContext
    {
        public StaticEcsSystemTwoContext(int entityCount, int padding)
            : base(entityCount * (padding + 1), (uint)ParallelContext.ParallelWorkerCount)
        {
            for (int index = 0; index < entityCount; index++)
            {
                for (int pad = 0; pad < padding; pad++)
                {
                    _ = StaticEcsWorld.NewEntity<Default>();
                }

                _ = StaticEcsWorld.NewEntity<Default>().Set(
                    new StaticEcsComponents.Component1(),
                    new StaticEcsComponents.Component2 { Value = 1 });
            }
        }
    }

    internal sealed class StaticEcsSystemThreeContext : StaticEcsBaseContext
    {
        public StaticEcsSystemThreeContext(int entityCount, int padding)
            : base(entityCount * (padding + 1), (uint)ParallelContext.ParallelWorkerCount)
        {
            for (int index = 0; index < entityCount; index++)
            {
                for (int pad = 0; pad < padding; pad++)
                {
                    _ = StaticEcsWorld.NewEntity<Default>();
                }

                _ = StaticEcsWorld.NewEntity<Default>().Set(
                    new StaticEcsComponents.Component1(),
                    new StaticEcsComponents.Component2 { Value = 1 },
                    new StaticEcsComponents.Component3 { Value = 1 });
            }
        }
    }

    internal sealed class StaticEcsSystemMultipleCompositionContext : StaticEcsBaseContext
    {
        public StaticEcsSystemMultipleCompositionContext(int entityCount)
            : base(entityCount, (uint)ParallelContext.ParallelWorkerCount)
        {
            for (int index = 0; index < entityCount; index++)
            {
                _ = (index % 4) switch
                {
                    0 => StaticEcsWorld.NewEntity<Default>().Set(
                                                new StaticEcsComponents.Component1(),
                                                new StaticEcsComponents.Component2 { Value = 1 },
                                                new StaticEcsComponents.Composition1()),
                    1 => StaticEcsWorld.NewEntity<Default>().Set(
                                                new StaticEcsComponents.Component1(),
                                                new StaticEcsComponents.Component2 { Value = 1 },
                                                new StaticEcsComponents.Composition2()),
                    2 => StaticEcsWorld.NewEntity<Default>().Set(
                                                new StaticEcsComponents.Component1(),
                                                new StaticEcsComponents.Component2 { Value = 1 },
                                                new StaticEcsComponents.Composition3()),
                    _ => StaticEcsWorld.NewEntity<Default>().Set(
                                                new StaticEcsComponents.Component1(),
                                                new StaticEcsComponents.Component2 { Value = 1 },
                                                new StaticEcsComponents.Composition4()),
                };
            }
        }
    }
}
