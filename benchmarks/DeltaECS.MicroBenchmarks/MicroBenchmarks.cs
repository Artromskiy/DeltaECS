using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Short BDN surface; fixture and kernel code lives in the implementation file.</summary>
internal static class MicroBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(DenseIterationMicroBenchmarks),
        typeof(GeneratedFunctorMovement4MicroBenchmarks),
        typeof(Movement4ApiComparisonMicroBenchmarks),
        typeof(WhereIterationMicroBenchmarks),
        typeof(WhereApiMicroBenchmarks),
        typeof(StructuralOperationsMicroBenchmarks),
        typeof(QueryBatchStructuralOperationsMicroBenchmarks)
    ];
}

[MemoryDiagnoser]
public class DenseIterationMicroBenchmarks : DenseIterationMicroBenchmarkImplementation
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

[MemoryDiagnoser]
public class WhereIterationMicroBenchmarks : WhereIterationMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class WhereApiMicroBenchmarks : WhereApiMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class StructuralOperationsMicroBenchmarks : StructuralOperationsMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class QueryBatchStructuralOperationsMicroBenchmarks : QueryBatchStructuralOperationsMicroBenchmarkImplementation
{
}
