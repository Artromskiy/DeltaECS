using BenchmarkDotNet.Attributes;
using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS.ArrayRefBenchmarks;

internal struct ArrayReferenceValue
{
    internal int Value;
}

/// <summary>Measures generated component iteration over an array-backed world.</summary>
[InProcess]
[MemoryDiagnoser]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "BenchmarkDotNet discovers benchmark types only when they are public.")]
public class ForEachArrayReferenceBenchmarks : IDisposable
{
    private World _world = null!; // GlobalSetup initializes the world before benchmark cleanup.
    private Entity[] _entities = null!; // GlobalSetup initializes the output before benchmark execution.
    private Query _query;

    /// <summary>Creates the world and verifies the benchmark checksum.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId valueId = layouts.Register<ArrayReferenceValue>(new SchemaId(96_001));
        _world = new World(layouts, initialEntityCapacity: Program.Amount);
        _entities = new Entity[Program.Amount];
        _world.Create(stackalloc[] { valueId }, Program.Amount, _entities);

        for (int index = 0; index < _entities.Length; index++)
        {
            _world.Set(_entities[index], valueId, new ArrayReferenceValue { Value = index });
        }

        QuerySpec description = QuerySpec.WhereAll(valueId);
        _query = _world.CreateQuery(in description);
        long expectedChecksum = (long)_entities.Length * (_entities.Length - 1) / 2;

        if (ForEachT() != expectedChecksum)
        {
            throw new InvalidOperationException("Generated ForEach<T> did not visit every benchmark entity.");
        }
    }

    /// <summary>Disposes the benchmark world after measurement.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases resources owned by the benchmark.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases resources owned by the benchmark.</summary>
    /// <param name="disposing">Whether managed resources should be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _world.Dispose();
        }
    }

    /// <summary>Iterates all values and returns their checksum.</summary>
    [Benchmark]
    public long ForEachT()
    {
        long checksum = 0;
        _world.ForEach(
            in _query,
            ref checksum,
            static (ref long sum, in ArrayReferenceValue value) => sum += value.Value);
        return checksum;
    }
}
