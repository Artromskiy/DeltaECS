using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

public class GeneratedGenericBindingsGeneratorTests
{
    [Test]
    public void GenericComponentRegistrationCompilesWithCSharp9()
    {
        const string source = """
            using System;
            using Delta.ECS;

            public struct Position { public float X; }
            public struct Velocity { public float X; }
            public struct Acceleration { public float X; }
            public struct History<T> { public T Value; }
            public struct Pair<TFirst, TSecond> { }
            public struct Triple<TFirst, TSecond, TThird> { }

            public static class Caller
            {
                public static ComponentId Register(ComponentLayoutRegistry layouts)
                {
                    ComponentId position = layouts.Register<Position>(new SchemaId(1));
                    ComponentId velocity = layouts.Register<Velocity>(new SchemaId(3));
                    ComponentId acceleration = layouts.Register<Acceleration>(new SchemaId(4));
                    _ = layouts.Register(typeof(History<>), position, new SchemaId(2));
                    _ = layouts.Register(typeof(Pair<,>), position, velocity, new SchemaId(6));
                    ReadOnlySpan<ComponentId> pairArguments = stackalloc ComponentId[] { position, velocity };
                    _ = layouts.Register(typeof(Pair<,>), pairArguments, new SchemaId(7));
                    return layouts.Register(typeof(Triple<,,>), position, velocity, acceleration, new SchemaId(5));
                }
            }
            """;
        var options = new CSharpParseOptions(LanguageVersion.CSharp9);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !string.Equals(
                path,
                typeof(Delta.ECS.Generators.Consumer.GeneratedApiGrammarProof).Assembly.Location,
                StringComparison.Ordinal))
            .Append(typeof(World).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GenericComponentConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, options) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new GeneratedGenericBindingsGenerator().AsSourceGenerator() }, parseOptions: options);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.That(diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
    }
}
