using BenchmarkDotNet.Attributes;
using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.Fennecs_Components;
using fennecs;

namespace Ecs.CSharp.Benchmark
{
    public partial class CreateEntityWithThreeComponents
    {
        [Context]
        private readonly FennecsBaseContext _fennecs;

        [BenchmarkCategory(Categories.Fennecs)]
        [Benchmark]
        public void Fennecs()
        {
            _fennecs.Component3Template.Spawn(EntityCount, new Component1(), new Component2(), new Component3());
        }
    }
}
