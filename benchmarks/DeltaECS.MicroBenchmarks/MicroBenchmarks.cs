using BenchmarkDotNet.Attributes;

namespace Delta.ECS.MicroBenchmarks;

/// <summary>
/// The only file normally edited when selecting or exposing a microbenchmark.
/// The fixture, setup details and kernels live in MicroBenchmarkImplementations.cs.
/// </summary>
internal static class MicroBenchmarkCatalog
{
    public static readonly Type[] Types =
    [
        typeof(EntityRecordResolveMicroBenchmarks),
        typeof(CreateKnownArchetypeMicroBenchmarks),
        typeof(CachedBindingIterationMicroBenchmarks),
        typeof(AtomicStructuralMicroBenchmarks),
        typeof(ListBatchMicroBenchmarks),
        typeof(QueryBatchMicroBenchmarks),
        typeof(StorageAndOverlayMicroBenchmarks)
    ];
}

// Keep these wrappers intentionally empty. Add a wrapper and one catalog entry
// for a new operation; put the actual fixture and kernel in the implementation file.
[MemoryDiagnoser]
public class EntityRecordResolveMicroBenchmarks : EntityRecordResolveMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class CreateKnownArchetypeMicroBenchmarks : CreateKnownArchetypeMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class CachedBindingIterationMicroBenchmarks : CachedBindingIterationMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class AtomicStructuralMicroBenchmarks : AtomicStructuralMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class ListBatchMicroBenchmarks : ListBatchMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class QueryBatchMicroBenchmarks : QueryBatchMicroBenchmarkImplementation
{
}

[MemoryDiagnoser]
public class StorageAndOverlayMicroBenchmarks : StorageAndOverlayMicroBenchmarkImplementation
{
}
