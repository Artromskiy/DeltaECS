using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.Arch_Components;
using Flecs.NET.Core;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithThreeComponents
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
                        Entity padding = World.Entity();
                        switch (j % 3)
                        {
                            case 0:
                                padding.Add<Component1>();
                                break;

                            case 1:
                                padding.Add<Component2>();
                                break;

                            case 2:
                                padding.Add<Component3>();
                                break;
                        }
                    }

                    World.Entity().Add<Component1>()
                        .Set(new Component2
                        {
                            Value = 1
                        })
                        .Set(new Component3
                        {
                            Value = 1
                        });
                }
                query = World.QueryBuilder().With<Component1>().With<Component2>().With<Component3>().Build();
            }
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public void FlecsNet_Each()
        {
            _flecs.query.Each((Iter it, int index) =>
            {
                ref Component1 c1 = ref it.FieldAt<Component1>(0, index);
                ref Component2 c2 = ref it.FieldAt<Component2>(1, index);
                ref Component3 c3 = ref it.FieldAt<Component3>(2, index);
                c1.Value += c2.Value + c3.Value;
            });
        }

        [BenchmarkCategory(Categories.FlecsNet)]
        [Benchmark]
        public void FlecsNet_Iter()
        {
            _flecs.query.Iter(it =>
            {
                Field<Component1> c1 = it.Field<Component1>(0);
                Field<Component2> c2 = it.Field<Component2>(1);
                Field<Component3> c3 = it.Field<Component3>(2);
                foreach (int i in it)
                {
                    c1[i].Value += c2[i].Value + c3[i].Value;
                }
            });
        }
    }
}
