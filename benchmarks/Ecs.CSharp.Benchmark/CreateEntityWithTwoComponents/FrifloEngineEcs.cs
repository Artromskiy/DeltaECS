using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts.FrifloEngineComponents;

using Friflo.Engine.ECS;

namespace Ecs.CSharp.Benchmark;

public partial class CreateEntityWithTwoComponents
{
    [BenchmarkCategory(Categories.FrifloEngineEcs)]
    [Benchmark]
    public int FrifloEngineEcs()
    {
        EntityStore store = new EntityStore(PidType.UsePidAsId);
        _ = store.EnsureCapacity(EntityCount);

        Archetype archetype = store.GetArchetype(ComponentTypes.Get<Component1, Component2>());
        _ = archetype.EnsureCapacity(EntityCount);

        for (int i = 0; i < EntityCount; ++i)
        {
            _ = archetype.CreateEntity();
        }
        return EntityCount;
    }
}
