using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;

using RelEcs;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithOneComponent
    {

        [Context]
        private readonly RelEcsContext _relEcs;

        [BenchmarkCategory(Categories.RelEcs, Categories.SingleThread)]
        [Benchmark]
        public int RelEcs()
        {
            _relEcs.MonoThreadSystem.Run(_relEcs.World);
            return EntityCount;
        }
        private sealed class RelEcsContext : RelEcsBaseContext
        {

            public ISystem MonoThreadSystem { get; } = new MonoThreadRunSystem();

            public RelEcsContext(int entityCount, int entityPadding)
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    for (int j = 0; j < entityPadding; ++j)
                    {
                        World.Spawn();
                    }

                    World
                        .Spawn()
                        .Add(new Component1());
                }
            }
            private sealed class MonoThreadRunSystem : ISystem
            {
                public void Run(World world)
                {
                    foreach (Component1 c in world.Query<Component1>().Build())
                    {
                        c.Value++;
                    }
                }
            }
        }
    }
}
