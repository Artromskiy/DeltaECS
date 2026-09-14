using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.Fennecs_Components;
using fennecs;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithTwoComponents
    {
        [Context] private readonly FennecsBaseContext _fennecs;

        [BenchmarkCategory(Categories.Fennecs)]
        [Benchmark]
        public void Fennecs()
        {
            _fennecs.Component2Template.Spawn(EntityCount, new Component1(), new Component2());
        }
    }
}
