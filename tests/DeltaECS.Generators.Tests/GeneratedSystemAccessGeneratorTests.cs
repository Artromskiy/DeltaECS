using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

[TestFixture]
public sealed class GeneratedSystemAccessGeneratorTests
{
    [Test]
    public void PartialSystemReceivesDeterministicAccessMetadata()
    {
        const string source = """
            using System;
            namespace Delta.ECS
            {
                public readonly struct Entity { }
                public readonly struct ComponentId { }
                public readonly struct Query { }
                public sealed class ComponentLayoutRegistry
                {
                    public ComponentId GetPrimary<T>() => default;
                }
                public sealed class World
                {
                    public ComponentLayoutRegistry Layouts { get; } = new();
                    public void ForEach<T1, T2>(in Query query, ForEachAction<T1, T2> action) { }
                    public ref T GetRef<T>(Entity entity) => throw new NotImplementedException();
                    public T Get<T>(Entity entity) => default!;
                    public bool Has<T>(Entity entity) => true;
                    public bool Add<T>(Entity entity, in T value) => true;
                    public bool Remove<T>(Entity entity) => true;
                    public bool Set<T>(Entity entity, in T value) => true;
                    public int Create<T>(int count) => count;
                    public void Destroy(Entity entity) { }
                }
                public delegate void ForEachAction<T1, T2>(ref T1 first, in T2 second);
            }
            namespace Delta.ECS.Systems
            {
                using Delta.ECS;
                public interface ISystem
                {
                    World World { get; init; }
                    SystemAccess Access { get; }
                    void Tick();
                }
                public readonly struct SystemAccess
                {
                    public SystemAccess(
                        ComponentId[]? reads = null,
                        ComponentId[]? writes = null,
                        ComponentId[]? stampReads = null,
                        ComponentId[]? adds = null,
                        ComponentId[]? removes = null,
                        bool readsTopology = false,
                        bool writesTopology = false,
                        bool createsEntities = false,
                        bool destroysEntities = false,
                        bool unknownWorldAccess = false,
                        bool usesParallelExecutor = false) { }
                    public static SystemAccess None => default;
                }
            }
            namespace Game
            {
                using Delta.ECS;
                using Delta.ECS.Systems;
                public struct Position { public int Value; }
                public struct Velocity { public int Value; }
                public partial class MovementSystem : ISystem
                {
                    private readonly Query _query;
                    public World World { get; init; } = null!;
                    public void Tick()
                    {
                        World.ForEach(in _query, static (ref Position position, in Velocity velocity) =>
                            position.Value += velocity.Value);
                        _ = World.GetRef<Position>(default);
                        _ = World.Get<Velocity>(default);
                        _ = World.Has<Velocity>(default);
                        Position value = default;
                        _ = World.Add(default, in value);
                        _ = World.Remove<Velocity>(default);
                        _ = World.Set(default, in value);
                        _ = World.Create<Position>(1);
                        World.Destroy(default);
                    }
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new GeneratedSystemAccessGenerator().AsSourceGenerator());
        GeneratorDriverRunResult run = driver.RunGenerators(compilation).GetRunResult();

        Assert.That(run.Diagnostics, Is.Empty);
        SyntaxTree generated = run.GeneratedTrees.Single(tree => tree.FilePath.Contains("GeneratedSystemAccess_", StringComparison.Ordinal));
        string text = generated.GetText().ToString();
        Assert.That(text, Does.Contain("readsTopology: true"));
        Assert.That(text, Does.Contain("GetPrimary<global::Game.Velocity>()"));
        Assert.That(text, Does.Contain("GetPrimary<global::Game.Position>()"));
        Assert.That(text, Does.Contain("writes: new global::Delta.ECS.ComponentId[]"));
        Assert.That(text, Does.Contain("adds: new global::Delta.ECS.ComponentId[]"));
        Assert.That(text, Does.Contain("removes: new global::Delta.ECS.ComponentId[]"));
        Assert.That(text, Does.Contain("createsEntities: true"));
        Assert.That(text, Does.Contain("destroysEntities: true"));
        Assert.That(text, Does.Contain("public global::Delta.ECS.Systems.SystemAccess Access"));

        CSharpCompilation output = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest)));
        Diagnostic[] errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
    }

    [Test]
    public void GlobalNamespaceSystemReceivesAccessMetadata()
    {
        const string source = """
            namespace Delta.ECS
            {
                public readonly struct ComponentId { }
                public readonly struct Query { }
                public sealed class ComponentLayoutRegistry { public ComponentId GetPrimary<T>() => default; }
                public sealed class World
                {
                    public ComponentLayoutRegistry Layouts { get; } = new();
                    public void ForEach<T>(in Query query, ForEachAction<T> action) { }
                }
                public delegate void ForEachAction<T>(ref T component);
            }
            namespace Delta.ECS.Systems
            {
                using Delta.ECS;
                public interface ISystem
                {
                    World World { get; init; }
                    SystemAccess Access { get; }
                    void Tick();
                }
                public readonly struct SystemAccess
                {
                    public SystemAccess(ComponentId[]? writes = null, bool readsTopology = false) { }
                    public static SystemAccess None => default;
                }
            }
            public struct Position { public int Value; }
            public partial class GlobalSystem : Delta.ECS.Systems.ISystem
            {
                public Delta.ECS.World World { get; init; } = null!;
                public void Tick()
                {
                    Delta.ECS.Query query = default;
                    World.ForEach(in query, static (ref Position position) => position.Value++);
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new GeneratedSystemAccessGenerator().AsSourceGenerator());
        GeneratorDriverRunResult run = driver.RunGenerators(compilation).GetRunResult();

        Assert.That(run.Diagnostics, Is.Empty);
        string generated = run.GeneratedTrees.Single().GetText().ToString();
        Assert.That(generated, Does.Not.Contain("namespace <global namespace>"));
        CSharpCompilation output = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(generated, new CSharpParseOptions(LanguageVersion.Latest)));
        Diagnostic[] errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "GeneratedSystemAccessHarness",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
