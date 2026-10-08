using System.Runtime.CompilerServices;
using Delta.ECS;
using IDisposable = System.IDisposable;
using DeltaComponentId = Delta.ECS.ComponentId;
using DeltaEntity = Delta.ECS.Entity;
using DeltaLayoutRegistry = Delta.ECS.ComponentLayoutRegistry;
using DeltaQuery = Delta.ECS.Query;
using DeltaQuerySpec = Delta.ECS.QuerySpec;
using DeltaSchemaId = Delta.ECS.SchemaId;
using DeltaWorld = Delta.ECS.World;
using Ecs.CSharp.Benchmark.Contexts.DeltaEcsComponents;

namespace Ecs.CSharp.Benchmark.Contexts
{
    namespace DeltaEcsComponents
    {
        internal struct DeltaComponent1
        {
            internal int Value;
        }

        internal struct DeltaComponent2
        {
            internal int Value;
        }

        internal struct DeltaComponent3
        {
            internal int Value;
        }

        internal struct DeltaComponentPadding
        {
        }

        internal struct DeltaCompositionPadding0
        {
        }

        internal struct DeltaCompositionPadding1
        {
        }

        internal struct DeltaCompositionPadding2
        {
        }

        internal struct DeltaCompositionPadding3
        {
        }
    }

    internal struct DeltaComponent1Functor : IForEach
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref DeltaComponent1 component) => ++component.Value;
    }

    internal struct DeltaComponent2Functor : IForEach
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
            first.Value += second.Value;
    }

    internal struct DeltaComponent3Functor : IForEach
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(
            ref DeltaComponent1 first,
            ref readonly DeltaComponent2 second,
            ref readonly DeltaComponent3 third) =>
            first.Value += second.Value + third.Value;
    }

    internal sealed class DeltaCreateOneContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaComponentId[] Components { get; }

        internal DeltaCreateOneContext()
        {
            DeltaLayoutRegistry layouts = new();
            DeltaComponentId component = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_001));
            World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
            Components = [component];
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaCreateTwoContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaComponentId[] Components { get; }

        internal DeltaCreateTwoContext()
        {
            DeltaLayoutRegistry layouts = new();
            DeltaComponentId first = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_002));
            DeltaComponentId second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(900_003));
            World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
            Components = [first, second];
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaCreateThreeContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaComponentId[] Components { get; }

        internal DeltaCreateThreeContext()
        {
            DeltaLayoutRegistry layouts = new();
            DeltaComponentId first = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_004));
            DeltaComponentId second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(900_005));
            DeltaComponentId third = layouts.Register<DeltaComponent3>(new DeltaSchemaId(900_006));
            World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
            Components = [first, second, third];
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaSystemOneContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaQuery Query;

        internal EcsOperation Iteration { get; private set; } = null!;

        internal EcsOperation ParallelIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent1Functor> FunctorIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent1Functor> ParallelFunctorIteration { get; private set; } = null!;

        private readonly DeltaComponentId _component;

        private readonly DeltaEntity[] _entities;

        internal DeltaSystemOneContext(int entityCount, int entityPadding)
        {
            DeltaLayoutRegistry layouts = new();
            _component = layouts.Register<DeltaComponent1>(new DeltaSchemaId(901_001));
            DeltaComponentId padding = layouts.Register<DeltaComponentPadding>(new DeltaSchemaId(901_000));
            World = new DeltaWorld(layouts, initialEntityCapacity: entityCount * (entityPadding + 1));

            if (entityPadding != 0)
            {
                DeltaEntity[] paddingEntities = new DeltaEntity[entityCount * entityPadding];
                World.Create(stackalloc[] { padding }, paddingEntities.Length, paddingEntities);
            }

            _entities = new DeltaEntity[entityCount];
            World.Create(stackalloc[] { _component }, entityCount, _entities);
            for (int i = 0; i < _entities.Length; i++)
            {
                DeltaEntity entity = _entities[i];
                World.GetRef<DeltaComponent1>(entity, _component) = new DeltaComponent1 { Value = 1 };
            }

            Query = World.CreateQuery(DeltaQuerySpec.WhereAll(_component));
            Iteration = World.ForEach(
                in Query,
                static (ref DeltaComponent1 component) => ++component.Value);
            ParallelIteration = World.ForEachParallel(
                in Query,
                static (ref DeltaComponent1 component) => ++component.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            FunctorIteration = World.ForEach(in Query, new DeltaComponent1Functor());
            ParallelFunctorIteration = World.ForEachParallel(
                in Query,
                new DeltaComponent1Functor(),
                workerCount: ParallelContext.ParallelWorkerCount);
            Iteration.Invoke();
            ParallelIteration.Invoke();
            FunctorIteration.Invoke();
            ParallelFunctorIteration.Invoke();
            ResetComponents();
        }

        private void ResetComponents()
        {
            for (int index = 0; index < _entities.Length; index++)
            {
                World.GetRef<DeltaComponent1>(_entities[index], _component) = new DeltaComponent1 { Value = 1 };
            }
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaSystemTwoContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaQuery Query;

        internal EcsOperation Iteration { get; private set; } = null!;

        internal EcsOperation ParallelIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent2Functor> FunctorIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent2Functor> ParallelFunctorIteration { get; private set; } = null!;

        internal DeltaComponentId First { get; }

        internal DeltaComponentId Second { get; }

        private readonly DeltaEntity[] _entities;

        internal DeltaSystemTwoContext(int entityCount, int entityPadding)
        {
            DeltaLayoutRegistry layouts = new();
            First = layouts.Register<DeltaComponent1>(new DeltaSchemaId(901_002));
            Second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(901_003));
            DeltaComponentId padding = layouts.Register<DeltaComponentPadding>(new DeltaSchemaId(901_000));
            World = new DeltaWorld(layouts, initialEntityCapacity: entityCount * (entityPadding + 1));

            if (entityPadding != 0)
            {
                DeltaEntity[] paddingEntities = new DeltaEntity[entityCount * entityPadding];
                World.Create(stackalloc[] { padding }, paddingEntities.Length, paddingEntities);
            }

            _entities = new DeltaEntity[entityCount];
            World.Create(stackalloc[] { First, Second }, entityCount, _entities);
            for (int i = 0; i < _entities.Length; i++)
            {
                DeltaEntity entity = _entities[i];
                World.GetRef<DeltaComponent1>(entity, First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(entity, Second) = new DeltaComponent2 { Value = 2 };
            }

            Query = World.CreateQuery(DeltaQuerySpec.WhereAll(stackalloc ComponentId[] { First, Second }));
            Iteration = World.ForEach(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value);
            ParallelIteration = World.ForEachParallel(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            FunctorIteration = World.ForEach(in Query, new DeltaComponent2Functor());
            ParallelFunctorIteration = World.ForEachParallel(
                in Query,
                new DeltaComponent2Functor(),
                workerCount: ParallelContext.ParallelWorkerCount);
            Iteration.Invoke();
            ParallelIteration.Invoke();
            FunctorIteration.Invoke();
            ParallelFunctorIteration.Invoke();
            ResetComponents();
        }

        private void ResetComponents()
        {
            for (int index = 0; index < _entities.Length; index++)
            {
                World.GetRef<DeltaComponent1>(_entities[index], First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(_entities[index], Second) = new DeltaComponent2 { Value = 2 };
            }
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaSystemThreeContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaQuery Query;

        internal EcsOperation Iteration { get; private set; } = null!;

        internal EcsOperation ParallelIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent3Functor> FunctorIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent3Functor> ParallelFunctorIteration { get; private set; } = null!;

        internal DeltaComponentId First { get; }

        internal DeltaComponentId Second { get; }

        internal DeltaComponentId Third { get; }

        private readonly DeltaEntity[] _entities;

        internal DeltaSystemThreeContext(int entityCount, int entityPadding)
        {
            DeltaLayoutRegistry layouts = new();
            First = layouts.Register<DeltaComponent1>(new DeltaSchemaId(901_004));
            Second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(901_005));
            Third = layouts.Register<DeltaComponent3>(new DeltaSchemaId(901_006));
            DeltaComponentId padding = layouts.Register<DeltaComponentPadding>(new DeltaSchemaId(901_000));
            World = new DeltaWorld(layouts, initialEntityCapacity: entityCount * (entityPadding + 1));

            if (entityPadding != 0)
            {
                DeltaEntity[] paddingEntities = new DeltaEntity[entityCount * entityPadding];
                World.Create(stackalloc[] { padding }, paddingEntities.Length, paddingEntities);
            }

            _entities = new DeltaEntity[entityCount];
            World.Create(stackalloc[] { First, Second, Third }, entityCount, _entities);
            for (int i = 0; i < _entities.Length; i++)
            {
                DeltaEntity entity = _entities[i];
                World.GetRef<DeltaComponent1>(entity, First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(entity, Second) = new DeltaComponent2 { Value = 2 };
                World.GetRef<DeltaComponent3>(entity, Third) = new DeltaComponent3 { Value = 3 };
            }

            Query = World.CreateQuery(DeltaQuerySpec.WhereAll(stackalloc ComponentId[] { First, Second, Third }));
            Iteration = World.ForEach(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value);
            ParallelIteration = World.ForEachParallel(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                    first.Value += second.Value + third.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            FunctorIteration = World.ForEach(in Query, new DeltaComponent3Functor());
            ParallelFunctorIteration = World.ForEachParallel(
                in Query,
                new DeltaComponent3Functor(),
                workerCount: ParallelContext.ParallelWorkerCount);
            Iteration.Invoke();
            ParallelIteration.Invoke();
            FunctorIteration.Invoke();
            ParallelFunctorIteration.Invoke();
            ResetComponents();
        }

        private void ResetComponents()
        {
            for (int index = 0; index < _entities.Length; index++)
            {
                World.GetRef<DeltaComponent1>(_entities[index], First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(_entities[index], Second) = new DeltaComponent2 { Value = 2 };
                World.GetRef<DeltaComponent3>(_entities[index], Third) = new DeltaComponent3 { Value = 3 };
            }
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    internal sealed class DeltaSystemMultipleCompositionContext : IDisposable
    {
        internal DeltaWorld World { get; }

        internal DeltaQuery Query;

        internal EcsOperation Iteration { get; private set; } = null!;

        internal EcsOperation ParallelIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent2Functor> FunctorIteration { get; private set; } = null!;

        internal EcsOperation<DeltaComponent2Functor> ParallelFunctorIteration { get; private set; } = null!;

        internal DeltaComponentId First { get; }

        internal DeltaComponentId Second { get; }

        private readonly DeltaEntity[] _entities;

        internal DeltaSystemMultipleCompositionContext(int entityCount)
        {
            DeltaLayoutRegistry layouts = new();
            First = layouts.Register<DeltaComponent1>(new DeltaSchemaId(901_007));
            Second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(901_008));
            DeltaComponentId padding0 = layouts.Register<DeltaCompositionPadding0>(new DeltaSchemaId(901_009));
            DeltaComponentId padding1 = layouts.Register<DeltaCompositionPadding1>(new DeltaSchemaId(901_010));
            DeltaComponentId padding2 = layouts.Register<DeltaCompositionPadding2>(new DeltaSchemaId(901_011));
            DeltaComponentId padding3 = layouts.Register<DeltaCompositionPadding3>(new DeltaSchemaId(901_012));
            World = new DeltaWorld(layouts, initialEntityCapacity: entityCount);
            _entities = new DeltaEntity[entityCount];

            DeltaComponentId[] composition0 = [First, Second, padding0];
            DeltaComponentId[] composition1 = [First, Second, padding1];
            DeltaComponentId[] composition2 = [First, Second, padding2];
            DeltaComponentId[] composition3 = [First, Second, padding3];

            int offset = 0;
            offset = CreateComposition(composition0, (entityCount + 3) / 4, offset);
            offset = CreateComposition(composition1, (entityCount + 2) / 4, offset);
            offset = CreateComposition(composition2, (entityCount + 1) / 4, offset);
            _ = CreateComposition(composition3, entityCount / 4, offset);

            Query = World.CreateQuery(DeltaQuerySpec.WhereAll(stackalloc ComponentId[] { First, Second }));
            Iteration = World.ForEach(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value);
            ParallelIteration = World.ForEachParallel(
                in Query,
                static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                    first.Value += second.Value,
                workerCount: ParallelContext.ParallelWorkerCount);
            FunctorIteration = World.ForEach(in Query, new DeltaComponent2Functor());
            ParallelFunctorIteration = World.ForEachParallel(
                in Query,
                new DeltaComponent2Functor(),
                workerCount: ParallelContext.ParallelWorkerCount);
            Iteration.Invoke();
            ParallelIteration.Invoke();
            FunctorIteration.Invoke();
            ParallelFunctorIteration.Invoke();
            ResetComponents();
        }

        private int CreateComposition(DeltaComponentId[] composition, int count, int offset)
        {
            World.Create(composition, count, System.MemoryExtensions.AsSpan(_entities, offset, count));
            for (int index = 0; index < count; index++)
            {
                DeltaEntity entity = _entities[offset + index];
                World.GetRef<DeltaComponent1>(entity, First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(entity, Second) = new DeltaComponent2 { Value = 2 };
            }

            return offset + count;
        }

        private void ResetComponents()
        {
            for (int index = 0; index < _entities.Length; index++)
            {
                World.GetRef<DeltaComponent1>(_entities[index], First) = new DeltaComponent1 { Value = 1 };
                World.GetRef<DeltaComponent2>(_entities[index], Second) = new DeltaComponent2 { Value = 2 };
            }
        }

        void IDisposable.Dispose() => World.Dispose();
    }

    public static class ParallelContext
    {
        public static int ParallelWorkerCount = System.Environment.ProcessorCount;
    }
}
