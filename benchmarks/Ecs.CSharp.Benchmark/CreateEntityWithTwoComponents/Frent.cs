using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Frent;
using Frent.Core;
using static Ecs.CSharp.Benchmark.Contexts.FrentBaseContext;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        private static readonly EntityType _entityType = EntityType.EntityTypeOf([Component<Component1>.ID, Component<Component2>.ID], []);

        [Context]
        private readonly FrentBaseContext _frent;

        [BenchmarkCategory(Categories.Frent)]
        [Benchmark]
        public void Frent()
        {
            World world = _frent.World;
            world.EnsureCapacity(_entityType, EntityCount);

            for (int i = 0; i < EntityCount; i++)
                world.Create<Component1, Component2>(default, default);
        }

        [BenchmarkCategory(Categories.Frent)]
        [Benchmark]
        public void Frent_Bulk()
        {
            World world = _frent.World;
            for (int i = 0; i < EntityCount; i++)
                world.Create<Component1, Component2>(default, default);
        }
    }
}
