using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Short BDN surface; fixture and kernel code lives in the implementation file.</summary>
internal static class MicroBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(DenseIterationMicroBenchmarks),
        typeof(Movement4OrderMicroBenchmarks),
        typeof(GeneratedFunctorMovement4MicroBenchmarks),
        typeof(AddMicroBenchmarks),
        typeof(RemoveMicroBenchmarks),
        typeof(CreateMicroBenchmarks),
        typeof(DestroyMicroBenchmarks),
        typeof(ListStructuralBatchMicroBenchmarks),
        typeof(QueryStructuralBatchMicroBenchmarks)
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
public class AddMicroBenchmarks : AddMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class RemoveMicroBenchmarks : RemoveMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class CreateMicroBenchmarks : CreateMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class DestroyMicroBenchmarks : DestroyMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class ListStructuralBatchMicroBenchmarks : ListStructuralBatchMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class QueryStructuralBatchMicroBenchmarks : QueryStructuralBatchMicroBenchmarkImplementation
{
}
