using Delta.ECS.Runtime.Consumer;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Globalization",
    "CA1303:Do not pass literals as localized parameters",
    Justification = "Fixed English proof output is intentionally not localized.")]

RuntimeApiGrammarProof.Run();
Console.WriteLine("Runtime API grammar proof passed.");
