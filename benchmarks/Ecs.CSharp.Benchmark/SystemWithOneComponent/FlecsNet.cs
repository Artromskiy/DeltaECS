using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.ArchComponents;
using Flecs.NET.Core;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithOneComponent
    {
        [Context]
        private readonly FlecsContext _flecs;

        private sealed class FlecsContext : FlecsNetBaseContext
        {
            public Query query;

            public FlecsContext(int entityCount, int entityPadding)
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    for (int j = 0; j < entityPadding; ++j)
                    {
                        World.Entity();
                    }

                    World.Entity().Add<Component1>();

                }
                query = World.QueryBuilder().With<Component1>().Build();
            }
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public void FlecsNetEach()
        {
            _flecs.query.Each((Iter it, int index) =>
            {
                ref Component1 c1 = ref it.FieldAt<Component1>(0, index);
                c1.Value += 1;
            });
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public void FlecsNetIter()
        {
            _flecs.query.Iter(it =>
            {
                Field<Component1> c1 = it.Field<Component1>(0);
                foreach (int i in it)
                {
                    c1[i].Value += 1;
                }
            });
        }
    }
}
