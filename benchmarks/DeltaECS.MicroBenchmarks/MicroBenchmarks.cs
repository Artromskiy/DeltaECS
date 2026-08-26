using BenchmarkDotNet.Attributes;

namespace DeltaECS.MicroBenchmarks;

/// <summary>Short BDN surface; fixture and kernel code lives in the implementation file.</summary>
internal static class MicroBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(DenseIterationMicroBenchmarks),
        typeof(Movement4OrderMicroBenchmarks),
        typeof(GeneratedFunctorMovement4MicroBenchmarks),
        typeof(Movement4ApiComparisonMicroBenchmarks)
    ];
}

[MemoryDiagnoser]
public class DenseIterationMicroBenchmarks : DenseIterationMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class Movement4OrderMicroBenchmarks : Movement4OrderMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class GeneratedFunctorMovement4MicroBenchmarks : GeneratedFunctorMovement4MicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class Movement4ApiComparisonMicroBenchmarks : Movement4ApiComparisonMicroBenchmarkImplementation
{
}
