namespace Delta.ECS.Grammar.Aot;

#if !NATIVE_AOT
using Delta.ECS.Generators.Consumer;
#endif
using Delta.ECS.Runtime.Consumer;

internal static class Program
{
    private static void Main()
    {
#if NATIVE_AOT
        RuntimeApiGrammarProof.RunAotProof();
        Console.WriteLine("NativeAOT constrained visitor proof passed.");
#else
        RuntimeApiGrammarProof.Run();
        GeneratedApiGrammarProof.Run();
        Console.WriteLine("Runtime and generated API grammar proofs passed.");
#endif
    }
}
