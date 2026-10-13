using System.Diagnostics.CodeAnalysis;

// Benchmark and context types are selected or instantiated by BenchmarkDotNet through reflection.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The comparative runner discovers public benchmark types across this assembly.")]
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "BenchmarkDotNet creates the benchmark contexts through reflection.")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "The comparative suite intentionally stores operations through IOperation to exercise type-erased iteration forms.")]
