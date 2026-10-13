using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.FennecsComponents;

namespace Ecs.CSharp.Benchmark;

public partial class CreateEntityWithOneComponent
{
    [Context] private readonly FennecsBaseContext _fennecs;

    [BenchmarkCategory(Categories.Fennecs)]
    [Benchmark]
    public int Fennecs()
    {
        _ = _fennecs.Component1Template.Spawn(EntityCount, new Component1());
        return EntityCount;
    }
}
