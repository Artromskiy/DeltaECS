using Ecs.CSharp.Benchmark.Contexts.StaticEcsComponents;
using FFS.Libraries.StaticEcs;

namespace Ecs.CSharp.Benchmark.Contexts
{
    internal readonly struct StaticEcsBlockOne : StaticEcsWorld.IQueryBlock.Write<Component1>
    {
        public void Invoke(uint count, StaticEcsWorld.EntityBlock entities, Block<Component1> component)
        {
            for (uint index = 0; index < count; index++)
            {
                component[index].Value++;
            }
        }
    }

    internal readonly struct StaticEcsBlockTwo : StaticEcsWorld.IQueryBlock.Write<Component1>.Read<Component2>
    {
        public void Invoke(uint count, StaticEcsWorld.EntityBlock entities, Block<Component1> first, BlockR<Component2> second)
        {
            for (uint index = 0; index < count; index++)
            {
                first[index].Value += second[index].Value;
            }
        }
    }

    internal readonly struct StaticEcsBlockThree : StaticEcsWorld.IQueryBlock.Write<Component1>.Read<Component2, Component3>
    {
        public void Invoke(
            uint count,
            StaticEcsWorld.EntityBlock entities,
            Block<Component1> first,
            BlockR<Component2> second,
            BlockR<Component3> third)
        {
            for (uint index = 0; index < count; index++)
            {
                first[index].Value += second[index].Value + third[index].Value;
            }
        }
    }
}
