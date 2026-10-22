using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

public sealed class ReadOnlyGetRefAnalyzerTests
{
    [Test]
    public async Task ReportsAndFixesReadOnlyRefLocal()
    {
        const string body = "ref Position position = ref world.GetRef<Position>(entity); _ = position.Value;";
        (Diagnostic diagnostic, string fixedSource) = await AnalyzeAndFix(body).ConfigureAwait(false);

        Assert.That(diagnostic.Id, Is.EqualTo(ReadOnlyGetRefAnalyzer.DiagnosticId));
        Assert.That(fixedSource, Does.Contain("ref readonly Position position = ref world.GetReadRef<Position>(entity)"));
        AssertCompiles(fixedSource);
    }

    [Test]
    public async Task ReportsAndFixesReadOnlyValueLocal()
    {
        const string body = "Position position = world.GetRef<Position>(entity); _ = position.Value;";
        (Diagnostic _, string fixedSource) = await AnalyzeAndFix(body).ConfigureAwait(false);

        Assert.That(fixedSource, Does.Contain("Position position = world.GetReadRef<Position>(entity)"));
        AssertCompiles(fixedSource);
    }

    [Test]
    public async Task ReportsAndFixesEntityRefAccess()
    {
        const string body = "ref readonly Position position = ref entityRef.GetRef<Position>(); _ = position.Value;";
        (Diagnostic _, string fixedSource) = await AnalyzeAndFix(body).ConfigureAwait(false);

        Assert.That(fixedSource, Does.Contain("entityRef.GetReadRef<Position>()"));
        AssertCompiles(fixedSource);
    }

    [Test]
    public async Task ReportsAndFixesExplicitRegistrationAccess()
    {
        const string body = "ref Position position = ref world.GetRef<Position>(entity, default); _ = position.Value;";
        (Diagnostic _, string fixedSource) = await AnalyzeAndFix(body).ConfigureAwait(false);

        Assert.That(fixedSource, Does.Contain("world.GetReadRef<Position>(entity, default)"));
        AssertCompiles(fixedSource);
    }

    [Test]
    public async Task DoesNotReportWhenTheGetRefResultIsModifiedInInitializer()
    {
        const string body = "int value = world.GetRef<Position>(entity).Value++; _ = value;";
        ImmutableArray<Diagnostic> diagnostics = await Analyze(SourceFor(body)).ConfigureAwait(false);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task ReportsAndFixesReadOnlyFunctorComponentButLeavesContextRef()
    {
        const string source = """
            namespace Delta.ECS
            {
                public interface IForEachContextEntity<TContext> { }
                public struct EntityRef { }
                public struct Position { public int Value; }
            }

            public struct ReadFunctor : Delta.ECS.IForEachContextEntity<int>
            {
                public void Invoke(ref int context, Delta.ECS.EntityRef entity, ref Delta.ECS.Position position)
                {
                    _ = context + position.Value;
                }
            }
            """;
        (Diagnostic _, string fixedSource) = await AnalyzeAndFixSource(source).ConfigureAwait(false);

        Assert.That(fixedSource, Does.Contain("Invoke(ref int context, Delta.ECS.EntityRef entity, in Delta.ECS.Position position)"));
        AssertCompiles(fixedSource);
    }

    [Test]
    public async Task ReportsReadOnlyFunctorComponentsForEveryForEachMarker()
    {
        const string source = """
            namespace Delta.ECS
            {
                public interface IForEach { }
                public interface IForEachEntity { }
                public interface IForEachContext<TContext> { }
                public interface IForEachContextEntity<TContext> { }
                public struct EntityRef { }
                public struct Position { public int Value; }
            }

            public struct ComponentFunctor : Delta.ECS.IForEach
            {
                public void Invoke(ref Delta.ECS.Position position) => _ = position.Value;
            }

            public struct EntityFunctor : Delta.ECS.IForEachEntity
            {
                public void Invoke(Delta.ECS.EntityRef entity, ref Delta.ECS.Position position) => _ = position.Value;
            }

            public struct ContextFunctor : Delta.ECS.IForEachContext<int>
            {
                public void Invoke(ref int context, ref Delta.ECS.Position position) => _ = position.Value;
            }

            public struct ContextEntityFunctor : Delta.ECS.IForEachContextEntity<int>
            {
                public void Invoke(ref int context, Delta.ECS.EntityRef entity, ref Delta.ECS.Position position) => _ = position.Value;
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await Analyze(source).ConfigureAwait(false);

        Assert.That(CreateCompilation(source).GetDiagnostics().Where(static item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(diagnostics, Has.Length.EqualTo(4));
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)), Is.All.Contains("position"));
    }

    [TestCase("position.Value++; ")]
    [TestCase("position = default; ")]
    [TestCase("position.Change(); ")]
    [TestCase("Position.Mutate(ref position); ")]
    public async Task DoesNotReportWhenLocalMayBeModified(string use)
    {
        ImmutableArray<Diagnostic> diagnostics = await Analyze(
            SourceFor($"ref Position position = ref world.GetRef<Position>(entity); {use}"))
            .ConfigureAwait(false);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task IgnoresOtherGetRefMethods()
    {
        const string source = """
            namespace Other
            {
                public struct Position { public int Value; }
                public sealed class World { public ref T GetRef<T>() => throw null; }
                public static class Consumer
                {
                    public static void Run(World world)
                    {
                        ref Position position = ref world.GetRef<Position>();
                        _ = position.Value;
                    }
                }
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await Analyze(source).ConfigureAwait(false);

        Assert.That(diagnostics, Is.Empty);
    }

    private static async Task<(Diagnostic Diagnostic, string FixedSource)> AnalyzeAndFix(string body)
        => await AnalyzeAndFixSource(SourceFor(body)).ConfigureAwait(false);

    private static async Task<(Diagnostic Diagnostic, string FixedSource)> AnalyzeAndFixSource(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        ImmutableArray<Diagnostic> diagnostics = await Analyze(compilation).ConfigureAwait(false);
        Diagnostic diagnostic = diagnostics.Single();

        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        Solution solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "ReadOnlyGetRefCodeFixTest",
                "ReadOnlyGetRefCodeFixTest",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp11),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: GetReferences()))
            .AddDocument(documentId, "Consumer.cs", SourceText.From(source));
        Document document = solution.GetDocument(documentId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new ReadOnlyAccessCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);

        ImmutableArray<CodeActionOperation> operations = await actions.Single()
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Solution fixedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        string fixedSource = (await fixedSolution.GetDocument(documentId)!.GetTextAsync().ConfigureAwait(false)).ToString();
        return (diagnostic, fixedSource);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
        => await Analyze(CreateCompilation(source)).ConfigureAwait(false);

    private static async Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation)
        => await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new ReadOnlyGetRefAnalyzer(),
                new ReadOnlyForEachRefAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

    private static void AssertCompiles(string source)
        => Assert.That(CreateCompilation(source).GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);

    private static string SourceFor(string body)
        => $$"""
            using Delta.ECS;

            namespace Delta.ECS
            {
                public struct Position
                {
                    public int Value;
                    public void Change() => Value++;
                    public static void Mutate(ref Position position) { }
                }

                public struct Entity { }
                public struct ComponentId { }

                public sealed class World
                {
                    public ref T GetRef<T>(Entity entity) => throw null;
                    public ref T GetRef<T>(Entity entity, ComponentId componentId) => throw null;
                    public ref readonly T GetReadRef<T>(Entity entity) => throw null;
                    public ref readonly T GetReadRef<T>(Entity entity, ComponentId componentId) => throw null;
                }

                public struct EntityRef
                {
                    public ref T GetRef<T>() => throw null;
                    public ref T GetRef<T>(ComponentId componentId) => throw null;
                    public ref readonly T GetReadRef<T>() => throw null;
                    public ref readonly T GetReadRef<T>(ComponentId componentId) => throw null;
                }
            }

            public static class Consumer
            {
                public static void Run(Delta.ECS.World world, Delta.ECS.Entity entity, Delta.ECS.EntityRef entityRef)
                {
                    {{body}}
                }
            }
            """;

    private static CSharpCompilation CreateCompilation(string source)
        => CSharpCompilation.Create(
            "ReadOnlyGetRefAnalyzerConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp11)) },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IEnumerable<MetadataReference> GetReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
}
