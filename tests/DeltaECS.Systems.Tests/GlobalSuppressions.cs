using System.Diagnostics.CodeAnalysis;

// NUnit discovers fixtures and creates generated operation probes through reflection.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "NUnit discovers and invokes public test fixtures through reflection.")]
[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "NUnit and the generated scheduler tests create fixtures through reflection.")]
[assembly: SuppressMessage("Performance", "CA1859:Use concrete types when possible", Justification = "Tests intentionally store operations through IOperation to exercise the type-erased API.")]
