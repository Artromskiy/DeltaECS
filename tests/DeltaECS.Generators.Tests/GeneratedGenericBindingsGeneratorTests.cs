using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
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

    [Test]
    public void CSharp9GeneratedRegistrationSelectsTheGeneratedConstraintRoute()
    {
        const string source = """
            using System;
            using Delta.ECS;

            public struct UnmanagedComponent { public int Value; }
            public struct ManagedStructComponent { public string Value; }
            public class ConstructibleClassComponent { }
            public class PlainClassComponent { private PlainClassComponent() { } }

            public static class RegistrationProof
            {
                public static int Run()
                {
                    var layouts = new ComponentLayoutRegistry();
                    ComponentId unmanagedId = layouts.RegisterGenerated<UnmanagedComponent>(new SchemaId(1));
                    ComponentId managedStructId = layouts.RegisterGenerated<ManagedStructComponent>(new SchemaId(2));
                    ComponentId constructibleClassId = layouts.RegisterGenerated<ConstructibleClassComponent>(new SchemaId(3));
                    ComponentId plainClassId = layouts.RegisterGenerated<PlainClassComponent>(new SchemaId(4));

                    if (!layouts.TryVisit(unmanagedId, new UnmanagedVisitor())
                        || !layouts.TryVisit(unmanagedId, new StructVisitor())
                        || !layouts.TryVisit(unmanagedId, new UnconstrainedVisitor())
                        || layouts.TryVisit(managedStructId, new UnmanagedVisitor())
                        || !layouts.TryVisit(managedStructId, new StructVisitor())
                        || !layouts.TryVisit(constructibleClassId, new ClassNewVisitor())
                        || !layouts.TryVisit(constructibleClassId, new ClassVisitor())
                        || !layouts.TryVisit(plainClassId, new ClassVisitor())
                        || layouts.TryVisit(plainClassId, new ClassNewVisitor()))
                    {
                        return 1;
                    }

                    return 0;
                }
            }

            public sealed class UnmanagedVisitor : IUnmanagedVisitor
            {
                public void Visit<T>(ComponentId id) where T : unmanaged { }
            }

            public sealed class StructVisitor : IStructVisitor
            {
                public void Visit<T>(ComponentId id) where T : struct { }
            }

            public sealed class UnconstrainedVisitor : IUnconstrainedVisitor
            {
                public void Visit<T>(ComponentId id) { }
            }

            public sealed class ClassNewVisitor : IClassNewVisitor
            {
                public void Visit<T>(ComponentId id) where T : class, new() { }
            }

            public sealed class ClassVisitor : IClassVisitor
            {
                public void Visit<T>(ComponentId id) where T : class { }
            }
            """;
        var options = new CSharpParseOptions(LanguageVersion.CSharp9);
        MetadataReference[] references = GetConsumerReferences();
        var compilation = CSharpCompilation.Create("GeneratedRegistrationConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, options) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new GeneratedGenericBindingsGenerator().AsSourceGenerator() }, parseOptions: options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);

        Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        string generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(static tree => tree.GetText().ToString()));
        Assert.That(generated, Does.Contain("RegisterGenerated<T>"));
        Assert.That(generated, Does.Contain("GeneratedGenericBindingRegistry.RegisterComponentType<T>"));
        Assert.That(generated, Does.Not.Contain("GeneratedComponentCatalog.GetRegistration"));

        using var assemblyImage = new MemoryStream();
        EmitResult emit = output.Emit(assemblyImage);
        Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));
        assemblyImage.Position = 0;
        Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyImage);
        object? result = assembly.GetType("RegistrationProof")!
            .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null);

        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void CSharp13DoesNotGenerateTheRegistrationFallbackExtension()
    {
        const string source = """
            using Delta.ECS;

            public struct Position { public int Value; }

            public static class RegistrationProof
            {
                public static ComponentId Register(ComponentLayoutRegistry layouts)
                    => layouts.Register<Position>(new SchemaId(1));
            }
            """;
        var options = new CSharpParseOptions(LanguageVersion.CSharp13);
        var compilation = CSharpCompilation.Create("CSharp13RegistrationConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, options) }, GetConsumerReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new GeneratedGenericBindingsGenerator().AsSourceGenerator() }, parseOptions: options);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);

        Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        string generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(static tree => tree.GetText().ToString()));
        Assert.That(generated, Does.Not.Contain("GeneratedComponentRegistrationExtensions"));
    }

    private static MetadataReference[] GetConsumerReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !string.Equals(
                path,
                typeof(Delta.ECS.Generators.Consumer.GeneratedApiGrammarProof).Assembly.Location,
                StringComparison.Ordinal))
            .Append(typeof(World).Assembly.Location).Distinct()
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
}
