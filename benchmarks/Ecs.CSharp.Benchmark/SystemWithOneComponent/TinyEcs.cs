using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.TinyEcsComponents;
using TinyEcs;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithOneComponent
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
                        World.Entity();
                    }

                    World.Entity().Set(new Component1());
                }

                Query = World.QueryBuilder().Data<Component1>().Build();
            }
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public int TinyEcsEach()
        {
            var data = Data<Component1>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1);
                c1.Ref.Value++;
            }
            return EntityCount;
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public int TinyEcsEachJob()
        {
            var data = Data<Component1>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1);
                c1.Ref.Value++;
            }
            return EntityCount;
        }
    }
}
