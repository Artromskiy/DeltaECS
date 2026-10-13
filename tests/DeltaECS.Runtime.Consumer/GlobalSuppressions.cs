using System.Diagnostics.CodeAnalysis;

// The runtime proof types are referenced by the separate AOT grammar-proof project.
[assembly: SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "The public proof entry point is called by another project in the solution.")]
