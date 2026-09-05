using BenchmarkDotNet.Attributes;

namespace Ecs.CSharp.Benchmark
{
    [BenchmarkCategory(Categories.System)]
    [MemoryDiagnoser]
#if CHECK_CACHE_MISSES
    [HardwareCounters(BenchmarkDotNet.Diagnosers.HardwareCounter.CacheMisses)]
#endif
    public partial class SystemWithTwoComponents
    {
        public int EntityCount { get; set; } = BenchmarkConfiguration.EntityCount;

        public int EntityPadding { get; set; } = BenchmarkConfiguration.EntityPadding;

        [GlobalSetup]
        public void Setup() => BenchmarkOperations.SetupContexts(this, EntityCount, EntityPadding);

        [GlobalCleanup]
        public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
    }
}
