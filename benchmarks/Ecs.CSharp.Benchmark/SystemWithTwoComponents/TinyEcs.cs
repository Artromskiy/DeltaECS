using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.TinyEcsComponents;
using TinyEcs;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithTwoComponents
    {
        [Context] private readonly TinyEcsContext _tinyEcs;

        private sealed class TinyEcsContext : TinyEcsBaseContext
        {
            public Query Query { get; }

            public TinyEcsContext(int entityCount, int entityPadding) : base()
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    for (int j = 0; j < entityPadding; ++j)
                    {
                        var padding = World.Entity();
                        switch (j % 2)
                        {
                            case 0:
                                padding.Set(new Component1());
                                break;

                            case 1:
                                padding.Set(new Component2());
                                break;
                        }
                    }

                    World.Entity()
                        .Set(new Component1())
                        .Set(new Component2 { Value = 1 });
                    Query = World.QueryBuilder().Data<Component1>().Data<Component2>().Build();
                }
            }
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public int TinyEcsEach()
        {
            var data = Data<Component1, Component2>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1, out Ptr<Component2> c2);
                c1.Ref.Value += c2.Ref.Value;
            }
            return EntityCount;
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public int TinyEcsEachJob()
        {
            var data = Data<Component1, Component2>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1, out Ptr<Component2> c2);
                c1.Ref.Value += c2.Ref.Value;
            }
            return EntityCount;
        }
    }
}
