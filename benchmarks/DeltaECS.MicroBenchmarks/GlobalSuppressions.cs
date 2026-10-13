using System.Diagnostics.CodeAnalysis;

// BenchmarkDotNet discovers benchmark types through reflection and owns benchmark cleanup.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Benchmark entry points are discovered by BenchmarkDotNet from outside this assembly.")]
[assembly: SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "BenchmarkDotNet invokes GlobalCleanup, which disposes the benchmark-owned fixtures.")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "These benchmarks intentionally store operations through IOperation to exercise the type-erased API.")]
