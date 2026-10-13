using System.Diagnostics.CodeAnalysis;

// BenchmarkDotNet discovers benchmark types through reflection.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Benchmark entry points are discovered by BenchmarkDotNet from outside this assembly.")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "The probe deliberately compares reusable operations through the public IOperation abstraction.")]
