using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

public class GenericFunctorGeneratorTests
{
    [Test]
    public void ConsumerProofSupportsOrderedAndRepeatedVariadicArguments()
        => Assert.That(Consumer.ConsumerProof.RunRuntimeGenericFunctor(), Is.EqualTo(34));

    [Test]
    public void ConsumerProofExecutesGenericEntityListAndParallelForms()
        => Assert.That(Consumer.ConsumerProof.RunRuntimeGenericFunctorEntityForms(), Is.EqualTo(1));

    [Test]
    public void ConsumerProofExecutesGenericComponentQueryEntityListAndParallelForms()
        => Assert.That(Consumer.ConsumerProof.RunRuntimeGenericFunctorComponentForms(), Is.EqualTo(1));

    [Test]
    public void ConsumerProofExecutesGenericFunctorFormsWithContext()
        => Assert.That(Consumer.ConsumerProof.RunRuntimeGenericFunctorContextForms(), Is.EqualTo(1));

    [Test]
    public void GenericAdapterCompilesWithCSharp9AndConstraints()
    {
        const string source = """
            using Delta.ECS;
            public struct History<T> { public T Value; }
            public struct Context { public int Count; }
            public struct SaveWithContext<T> : IForEachContext<Context>
            {
                public void Invoke(ref Context context, in T value) { context.Count++; }
            }
            public struct SaveThree<T0, T1, T2> : IForEach
            {
                public void Invoke(in T0 first, in T1 second, in T2 third) { }
            }
            public static class Caller
            {
                public static void Run(World world, in Query query, ComponentId id)
                    => world.ForEach(in query, id, id, id, typeof(SaveThree<,,>));
                public static void RunWithContext(World world, in Query query, ComponentId id, ref Context context)
                    => world.ForEach(in query, ref context, id, typeof(SaveWithContext<>));
            }
            public struct Save<T> : IForEach where T : struct
            {
                public void Invoke(ref History<T> history, in T value) { history.Value = value; }
            }
            """;
        var options = new CSharpParseOptions(LanguageVersion.CSharp9);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(static path => !Path.GetFileNameWithoutExtension(path).Equals("DeltaECS.Generators.Consumer", StringComparison.Ordinal))
            .Append(typeof(World).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GenericAdapterConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, options) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new GenericFunctorGenerator().AsSourceGenerator() }, parseOptions: options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.That(diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
    }
}
