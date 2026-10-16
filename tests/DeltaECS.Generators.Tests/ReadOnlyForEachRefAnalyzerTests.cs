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

public sealed class ReadOnlyForEachRefAnalyzerTests
{
    [TestCase(LanguageVersion.CSharp11, "in")]
    [TestCase(LanguageVersion.CSharp12, "ref readonly")]
    public async Task ReportsAndFixesReadOnlyForEachLambda(LanguageVersion languageVersion, string expectedModifier)
    {
        string source = SourceFor(languageVersion, "_ = position.Value;");
        CSharpCompilation compilation = CreateCompilation(source, languageVersion);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyForEachRefAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        Diagnostic diagnostic = diagnostics.Single();
        Assert.That(diagnostic.Id, Is.EqualTo(ReadOnlyForEachRefAnalyzer.DiagnosticId));
        Assert.That(diagnostic.GetMessage(CultureInfo.InvariantCulture), Does.Contain(expectedModifier));

        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        IEnumerable<MetadataReference> references = GetReferences();
        Solution solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "ReadOnlyRefCodeFixTest",
                "ReadOnlyRefCodeFixTest",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(languageVersion),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: references))
            .AddDocument(documentId, "Consumer.cs", SourceText.From(source));
        Document document = solution.GetDocument(documentId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);

        await new ReadOnlyForEachRefCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);

        CodeAction action = actions.Single();
        ImmutableArray<CodeActionOperation> operations = await action
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Solution fixedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        string fixedSource = (await fixedSolution.GetDocument(documentId)!.GetTextAsync().ConfigureAwait(false)).ToString();

        Assert.That(fixedSource, Does.Contain($"({expectedModifier} Position position)"));
        fixedSource = expectedModifier == "in"
            ? fixedSource.Replace("public void ForEach(RefAction action) { }", string.Empty, StringComparison.Ordinal)
                .Replace("public void ForEach(RefReadonlyAction action) { }", string.Empty, StringComparison.Ordinal)
            : fixedSource.Replace("public void ForEach(RefAction action) { }", string.Empty, StringComparison.Ordinal)
                .Replace("public void ForEach(InAction action) { }", string.Empty, StringComparison.Ordinal);
        Assert.That(CreateCompilation(fixedSource, languageVersion).GetDiagnostics()
            .Where(static item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [TestCase("position.Value++;")]
    [TestCase("position = default;")]
    [TestCase("position.Change();")]
    [TestCase("Position.Mutate(ref position);")]
    [TestCase("position.Items.Add(1);")]
    public async Task DoesNotReportWhenCallbackMayWrite(string body)
    {
        CSharpCompilation compilation = CreateCompilation(SourceFor(LanguageVersion.CSharp11, body), LanguageVersion.CSharp11);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyForEachRefAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task ReportsWhenParameterIsOnlyPassedToReadOnlyCode()
    {
        CSharpCompilation compilation = CreateCompilation(
            SourceFor(LanguageVersion.CSharp11, "Position.Inspect(in position);"),
            LanguageVersion.CSharp11);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyForEachRefAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        Assert.That(diagnostics, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task LeavesCallerOwnedContextModifierForTheContextOperation()
    {
        const string source = """
            namespace Delta.ECS
            {
                public struct Position { public int Value; }
                public struct Context { public int Value; }
                public delegate void ContextAction(ref Context context, ref Position position);

                public sealed partial class World
                {
                    public void ForEach<TContext>(ref TContext context, ContextAction action) { }
                }
            }

            public static class ContextConsumer
            {
                public static void Run(Delta.ECS.World world, ref Delta.ECS.Context state)
                    => world.ForEach(ref state, static (ref Delta.ECS.Context context, ref Delta.ECS.Position position) =>
                    {
                        _ = position.Value;
                    });
            }
            """;
        CSharpCompilation compilation = CreateCompilation(source, LanguageVersion.CSharp11);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyForEachRefAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        Assert.That(diagnostics, Has.Length.EqualTo(1));
        Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture), Does.Contain("position"));
    }

    private static string SourceFor(LanguageVersion languageVersion, string body)
    {
        string readonlyDelegate = languageVersion >= LanguageVersion.CSharp12
            ? "public delegate void RefReadonlyAction(ref readonly Position position);"
            : string.Empty;

        return $$"""
            namespace Delta.ECS
            {
                public struct Position
                {
                    public int Value;
                    public System.Collections.Generic.List<int> Items;
                    public void Change() => Value++;
                    public static void Mutate(ref Position position) { }
                    public static void Inspect(in Position position) { }
                }

                public delegate void RefAction(ref Position position);
                public delegate void InAction(in Position position);
                {{readonlyDelegate}}

                public sealed class World
                {
                    public void ForEach(RefAction action) { }
                    public void ForEach(InAction action) { }
                    {{(languageVersion >= LanguageVersion.CSharp12 ? "public void ForEach(RefReadonlyAction action) { }" : string.Empty)}}
                }
            }

            public static class Consumer
            {
                public static void Run(Delta.ECS.World world)
                    => world.ForEach(static (ref Delta.ECS.Position position) =>
                    {
                        {{body}}
                    });
            }
            """;
    }

    private static CSharpCompilation CreateCompilation(string source, LanguageVersion languageVersion)
        => CSharpCompilation.Create(
            "ReadOnlyRefAnalyzerConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion)) },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IEnumerable<MetadataReference> GetReferences()
        => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
}
