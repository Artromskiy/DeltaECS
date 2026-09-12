using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Delta.ECS;
using DeltaEntity = Delta.ECS.Entity;
using DeltaWorld = Delta.ECS.World;
using DeltaComponentId = Delta.ECS.ComponentId;
using DeltaLayoutRegistry = Delta.ECS.ComponentLayoutRegistry;
using DeltaQuery = Delta.ECS.Query;
using DeltaQuerySpec = Delta.ECS.QuerySpec;
using DeltaSchemaId = Delta.ECS.SchemaId;

namespace Ecs.CSharp.Benchmark;

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

internal static class DeltaOperations
{
    internal const int ParallelWorkerCount = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(ref DeltaComponent1 component)
    {
        ++component.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(ref DeltaComponent1 first, ref readonly DeltaComponent2 second)
    {
        first.Value += second.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(
        ref DeltaComponent1 first,
        ref readonly DeltaComponent2 second,
        ref readonly DeltaComponent3 third)
    {
        first.Value += second.Value + third.Value;
    }
}

internal sealed class DeltaCreateOneContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaComponentId[] Components { get; }

    internal ArchetypeHandle Archetype { get; }

    internal DeltaCreateOneContext()
    {
        DeltaLayoutRegistry layouts = new();
        DeltaComponentId component = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_001));
        World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
        Components = [component];
        Archetype = World.GetOrCreateArchetype(Components);
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaCreateTwoContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaComponentId[] Components { get; }

    internal ArchetypeHandle Archetype { get; }

    internal DeltaCreateTwoContext()
    {
        DeltaLayoutRegistry layouts = new();
        DeltaComponentId first = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_002));
        DeltaComponentId second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(900_003));
        World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
        Components = [first, second];
        Archetype = World.GetOrCreateArchetype(Components);
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaCreateThreeContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaComponentId[] Components { get; }

    internal ArchetypeHandle Archetype { get; }

    internal DeltaCreateThreeContext()
    {
        DeltaLayoutRegistry layouts = new();
        DeltaComponentId first = layouts.Register<DeltaComponent1>(new DeltaSchemaId(900_004));
        DeltaComponentId second = layouts.Register<DeltaComponent2>(new DeltaSchemaId(900_005));
        DeltaComponentId third = layouts.Register<DeltaComponent3>(new DeltaSchemaId(900_006));
        World = new DeltaWorld(layouts, initialEntityCapacity: BenchmarkConfiguration.EntityCount);
        Components = [first, second, third];
        Archetype = World.GetOrCreateArchetype(Components);
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaSystemOneContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaQuery Query;

    internal DeltaSystemOneContext(int entityCount, int entityPadding)
    {
        DeltaLayoutRegistry layouts = new();
        DeltaComponentId component = layouts.Register<DeltaComponent1>(new DeltaSchemaId(901_001));
        DeltaComponentId padding = layouts.Register<DeltaComponentPadding>(new DeltaSchemaId(901_000));
        World = new DeltaWorld(layouts, initialEntityCapacity: entityCount * (entityPadding + 1));

        if (entityPadding != 0)
        {
            DeltaEntity[] paddingEntities = new DeltaEntity[entityCount * entityPadding];
            World.Create(stackalloc[] { padding }, paddingEntities.Length, paddingEntities);
        }

        DeltaEntity[] entities = new DeltaEntity[entityCount];
        World.Create(stackalloc[] { component }, entityCount, entities);
        for (int i = 0; i < entities.Length; i++)
        {
            DeltaEntity entity = entities[i];
            World.Set(entity, component, new DeltaComponent1 { Value = 1 });
        }

        Query = World.CreateQuery(DeltaQuerySpec.WhereAll(component));
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaSystemTwoContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaQuery Query;

    internal DeltaComponentId First { get; }

    internal DeltaComponentId Second { get; }

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

        DeltaEntity[] entities = new DeltaEntity[entityCount];
        World.Create(stackalloc[] { First, Second }, entityCount, entities);
        for (int i = 0; i < entities.Length; i++)
        {
            DeltaEntity entity = entities[i];
            World.Set(entity, First, new DeltaComponent1 { Value = 1 });
            World.Set(entity, Second, new DeltaComponent2 { Value = 2 });
        }

        Query = World.CreateQuery(DeltaQuerySpec.WhereAll(First, Second));
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaSystemThreeContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaQuery Query;

    internal DeltaComponentId First { get; }

    internal DeltaComponentId Second { get; }

    internal DeltaComponentId Third { get; }

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

        DeltaEntity[] entities = new DeltaEntity[entityCount];
        World.Create(stackalloc[] { First, Second, Third }, entityCount, entities);
        for (int i = 0; i < entities.Length; i++)
        {
            DeltaEntity entity = entities[i];
            World.Set(entity, First, new DeltaComponent1 { Value = 1 });
            World.Set(entity, Second, new DeltaComponent2 { Value = 2 });
            World.Set(entity, Third, new DeltaComponent3 { Value = 3 });
        }

        Query = World.CreateQuery(DeltaQuerySpec.WhereAll(First, Second, Third));
    }

    void IDisposable.Dispose() => World.Dispose();
}

internal sealed class DeltaSystemMultipleCompositionContext : IDisposable
{
    internal DeltaWorld World { get; }

    internal DeltaQuery Query;

    internal DeltaComponentId First { get; }

    internal DeltaComponentId Second { get; }

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

        DeltaComponentId[] composition0 = [First, Second, padding0];
        DeltaComponentId[] composition1 = [First, Second, padding1];
        DeltaComponentId[] composition2 = [First, Second, padding2];
        DeltaComponentId[] composition3 = [First, Second, padding3];

        CreateComposition(composition0, (entityCount + 3) / 4);
        CreateComposition(composition1, (entityCount + 2) / 4);
        CreateComposition(composition2, (entityCount + 1) / 4);
        CreateComposition(composition3, entityCount / 4);

        Query = World.CreateQuery(DeltaQuerySpec.WhereAll(First, Second));
    }

    private void CreateComposition(DeltaComponentId[] composition, int count)
    {
        DeltaEntity[] entities = new DeltaEntity[count];
        World.Create(composition, count, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            DeltaEntity entity = entities[index];
            World.Set(entity, First, new DeltaComponent1 { Value = 1 });
            World.Set(entity, Second, new DeltaComponent2 { Value = 2 });
        }
    }

    void IDisposable.Dispose() => World.Dispose();
}

public partial class CreateEntityWithOneComponent
{
    [Context]
    private readonly DeltaCreateOneContext _deltaEcs;

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeltaECS()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _deltaEcs.World.Create(_deltaEcs.Components);
        }
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch()
    {
        _deltaEcs.World.Create(_deltaEcs.Archetype, EntityCount);
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch_Generic()
    {
        _deltaEcs.World.Create<DeltaComponent1>(EntityCount);
    }
}

public partial class CreateEntityWithTwoComponents
{
    [Context]
    private readonly DeltaCreateTwoContext _deltaEcs;

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeltaECS()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _deltaEcs.World.Create(_deltaEcs.Components);
        }
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch()
    {
        _deltaEcs.World.Create(_deltaEcs.Archetype, EntityCount);
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch_Generic()
    {
        _deltaEcs.World.Create<DeltaComponent1, DeltaComponent2>(EntityCount);
    }
}

public partial class CreateEntityWithThreeComponents
{
    [Context]
    private readonly DeltaCreateThreeContext _deltaEcs;

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeltaECS()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _deltaEcs.World.Create(_deltaEcs.Components);
        }
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch()
    {
        _deltaEcs.World.Create(_deltaEcs.Archetype, EntityCount);
    }

    [BenchmarkCategory(Categories.DeltaECSBatch)]
    [Benchmark]
    public void DeltaECS_Batch_Generic()
    {
        _deltaEcs.World.Create<DeltaComponent1, DeltaComponent2, DeltaComponent3>(EntityCount);
    }
}

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
    public void DeltaECS_Parallel()
    {
        _deltaEcs.World.ForEachParallel(
            in _deltaEcs.Query,
            static (ref DeltaComponent1 component) => DeltaOperations.Update(ref component),
            workerCount: DeltaOperations.ParallelWorkerCount);
    }
}

public partial class SystemWithTwoComponents
{
    [Context]
    private readonly DeltaSystemTwoContext _deltaEcs;

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
    public void DeltaECS_Parallel()
    {
        _deltaEcs.World.ForEachParallel(
            in _deltaEcs.Query,
            static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                DeltaOperations.Update(ref first, in second),
            workerCount: DeltaOperations.ParallelWorkerCount);
    }
}

public partial class SystemWithThreeComponents
{
    [Context]
    private readonly DeltaSystemThreeContext _deltaEcs;

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeltaECS()
    {
        _deltaEcs.World.ForEach(
            in _deltaEcs.Query,
            static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                DeltaOperations.Update(ref first, in second, in third));
    }

    [BenchmarkCategory(Categories.DeltaECS)]
    [Benchmark]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeltaECS_Parallel()
    {
        _deltaEcs.World.ForEachParallel(
            in _deltaEcs.Query,
            static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                DeltaOperations.Update(ref first, in second, in third),
            workerCount: DeltaOperations.ParallelWorkerCount);
    }
}

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
    public void DeltaECS_Parallel()
    {
        _deltaEcs.World.ForEachParallel(
            in _deltaEcs.Query,
            static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) =>
                DeltaOperations.Update(ref first, in second),
            workerCount: DeltaOperations.ParallelWorkerCount);
    }
}

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

        using DeltaSystemTwoContext two = new(32, 1);
        two.World.ForEach(in two.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
        two.World.ForEachParallel(in two.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));

        using DeltaSystemThreeContext three = new(32, 1);
        three.World.ForEach(in three.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) => DeltaOperations.Update(ref first, in second, in third));
        three.World.ForEachParallel(
            in three.Query,
            static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second, ref readonly DeltaComponent3 third) =>
                DeltaOperations.Update(ref first, in second, in third));

        using DeltaSystemMultipleCompositionContext compositions = new(32);
        compositions.World.ForEach(in compositions.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
        compositions.World.ForEachParallel(in compositions.Query, static (ref DeltaComponent1 first, ref readonly DeltaComponent2 second) => DeltaOperations.Update(ref first, in second));
        Console.WriteLine("DeltaECS full-fork contract smoke passed.");
    }
}
