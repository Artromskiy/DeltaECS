using System.Diagnostics.CodeAnalysis;

// NUnit discovers public tests through reflection.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public test fixtures are discovered and invoked by NUnit.")]
