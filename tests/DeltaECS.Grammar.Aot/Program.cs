namespace Delta.ECS.Grammar.Aot;

#if !NATIVE_AOT
using Delta.ECS.Generators.Consumer;
#endif
using Delta.ECS.Runtime.Consumer;
using Delta.ECS.Registration.CSharp9;

internal static class Program
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1303:Do not pass literals as localized parameters",
        Justification = "Fixed English proof output is intentionally not localized.")]
    private static void Main()
    {
#if NATIVE_AOT
        RegistrationGrammarProof.Run();
        RuntimeApiGrammarProof.RunAotProof();
        Console.WriteLine("NativeAOT constrained visitor proof passed.");
#else
        RegistrationGrammarProof.Run();
        RuntimeApiGrammarProof.Run();
        GeneratedApiGrammarProof.Run();
        Console.WriteLine("Runtime and generated API grammar proofs passed.");
#endif
    }
}
