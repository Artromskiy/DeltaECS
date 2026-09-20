using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.ArchComponents;
using Flecs.NET.Core;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithTwoComponentsMultipleComposition
    {
        [Context]
        private readonly FlecsContext _flecs;


        private sealed class FlecsContext : FlecsNetBaseContext
        {

            private record struct Padding1();

            private record struct Padding2();

            private record struct Padding3();

            private record struct Padding4();

            public Query query;

            public FlecsContext(int entityCount)
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    Entity entity = World.Entity().Add<Component1>().Set(new Component2 { Value = 1 });
                    switch (i % 4)
                    {
                        case 0:
                            entity.Add<Padding1>();
                            break;
                        case 1:
                            entity.Add<Padding2>();
                            break;
                        case 2:
                            entity.Add<Padding3>();
                            break;
                        case 3:
                            entity.Add<Padding4>();
                            break;
                    }
                }
                query = World.QueryBuilder().With<Component1>().With<Component2>().Build();
            }
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public int FlecsNetEach()
        {
            _flecs.query.Each((Iter it, int index) =>
            {
                ref Component1 c1 = ref it.FieldAt<Component1>(0, index);
                ref Component2 c2 = ref it.FieldAt<Component2>(1, index);
                c1.Value += c2.Value;
            });
            return EntityCount;
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public int FlecsNetIter()
        {
            _flecs.query.Iter(it =>
            {
                Field<Component1> c1 = it.Field<Component1>(0);
                Field<Component2> c2 = it.Field<Component2>(1);
                foreach (int i in it)
                {
                    c1[i].Value += c2[i].Value;
                }
            });
            return EntityCount;
        }
    }
}
