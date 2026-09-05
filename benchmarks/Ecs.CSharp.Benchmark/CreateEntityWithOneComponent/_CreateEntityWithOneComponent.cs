using BenchmarkDotNet.Attributes;

namespace Ecs.CSharp.Benchmark
{
    [BenchmarkCategory(Categories.CreateEntity)]
    [MemoryDiagnoser]
#if CHECK_CACHE_MISSES
    [HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
    public partial class CreateEntityWithOneComponent
    {
        public int EntityCount { get; set; } = BenchmarkConfiguration.EntityCount;

        [IterationSetup]
        public void Setup() => BenchmarkOperations.SetupContexts(this);

        [IterationCleanup]
        public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
    }
}
