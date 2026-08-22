using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>Short BDN surface; fixture and kernel code lives in the implementation file.</summary>
internal static class MicroBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(QueryIteratorIterationMicroBenchmarks),
        typeof(AddMicroBenchmarks),
        typeof(RemoveMicroBenchmarks),
        typeof(CreateMicroBenchmarks),
        typeof(DestroyMicroBenchmarks)
    ];
}

[MemoryDiagnoser]
public class QueryIteratorIterationMicroBenchmarks : QueryIteratorIterationMicroBenchmarkImplementation
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
