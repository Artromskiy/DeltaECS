using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.FennecsComponents;
using fennecs;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        [Context] private readonly FennecsBaseContext _fennecs;

        [BenchmarkCategory(Categories.Fennecs)]
        [Benchmark]
        public int Fennecs()
        {
            _fennecs.Component2Template.Spawn(EntityCount, new Component1(), new Component2());
            return EntityCount;
        }
    }
}
