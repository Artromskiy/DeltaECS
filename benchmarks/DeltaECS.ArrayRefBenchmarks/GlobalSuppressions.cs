using System.Diagnostics.CodeAnalysis;

// BenchmarkDotNet discovers public benchmark types through reflection.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Benchmark entry points are discovered by BenchmarkDotNet from outside this assembly.")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "The benchmark intentionally stores its reusable operation through IOperation to exercise the type-erased API.")]
