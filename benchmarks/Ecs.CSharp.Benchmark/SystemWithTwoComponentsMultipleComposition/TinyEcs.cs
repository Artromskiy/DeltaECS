using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.TinyEcsComponents;
using TinyEcs;

namespace Ecs.CSharp.Benchmark
{
    public partial class SystemWithTwoComponentsMultipleComposition
    {
        [Context] private readonly TinyEcsContext _tinyEcs;

        private sealed class TinyEcsContext : TinyEcsBaseContext
        {

            private record struct Padding1(int Value);
            private record struct Padding2(int Value);
            private record struct Padding3(int Value);
            private record struct Padding4(int Value);
            public Query Query { get; }


            public TinyEcsContext(int entityCount) : base()
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    var entity = World.Entity();
                    entity.Set(new Component1());
                    entity.Set(new Component2 { Value = 1 });

                    switch (i % 4)
                    {
                        case 0:
                            entity.Set(new Padding1());
                            break;

                        case 1:
                            entity.Set(new Padding2());
                            break;

                        case 2:
                            entity.Set(new Padding3());
                            break;

                        case 3:
                            entity.Set(new Padding4());
                            break;
                    }
                }

                Query = World.QueryBuilder().Data<Component1>().Data<Component2>().Build();
            }
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public void TinyEcsEach()
        {
            var data = Data<Component1, Component2>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1, out Ptr<Component2> c2);
                c1.Ref.Value += c2.Ref.Value;
            }
        }

        [BenchmarkCategory(Categories.TinyEcs)]
        [Benchmark]
        public void TinyEcsEachJob()
        {
            var data = Data<Component1, Component2>.CreateIterator(_tinyEcs.Query.Iter());
            while (data.MoveNext())
            {
                data.Deconstruct(out Ptr<Component1> c1, out Ptr<Component2> c2);
                c1.Ref.Value += c2.Ref.Value;
            }
        }
    }
}
