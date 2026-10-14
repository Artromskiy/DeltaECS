using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

[TestFixture]
public sealed class CollectionExpressionDiagnosticSuppressorTests
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new Ide0300TestAnalyzer(),
        new CollectionExpressionDiagnosticSuppressor());

    [Test]
    public async Task SuppressesCollectionExpressionHintOnlyForOverloadSensitiveDeltaEcsCalls()
    {
        const string source = """
            using System;
            using Delta.ECS;

            namespace Delta.ECS
            {
                public readonly struct ComponentId { }
                public readonly struct Entity { }
                public readonly struct Query { }
                public readonly struct Position { }
                public readonly struct Health { }

                public sealed class World
                {
                    public Entity Create(ReadOnlySpan<ComponentId> components) => default;
                    public int Create(ReadOnlySpan<ComponentId> components, int count, Span<Entity> output) => count;
                }
            }

            namespace Consumer
            {
                public static class Other
                {
                    public static void Use(int[] values) { }
                }

                public static class GeneratedStructuralExtensions_Test
                {
                    public static Entity Create(this World target, ReadOnlySpan<ComponentId> componentIds, in Position position, in Health health) => default;
                }

                public static class DemandForEachExtensions_Test
                {
                    public static void ForEach(this World world, ReadOnlySpan<Entity> entities, in Query query, Action action) { }
                }

                public static class CallSites
                {
                    public static void Use(World world, ComponentId id, Entity entity, Query query, Span<Entity> output)
                    {
                        world.Create(new ComponentId[] { id }, new Position(), new Health());
                        world.Create(new ComponentId[] { id }, 1, output);
                        world.Create(new ComponentId[] { id });
                        world.ForEach(new[] { entity }, in query, static () => { });
                        Other.Use(new int[] { 1 });
                    }
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(Analyzers)
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        string[] diagnosticLines = diagnostics
            .Select(diagnostic => source.Split('\n')[diagnostic.Location.GetLineSpan().StartLinePosition.Line].Trim())
            .ToArray();
        Assert.That(diagnosticLines, Has.Length.EqualTo(3));
        string diagnosticText = string.Join('\n', diagnosticLines);
        Assert.That(diagnosticText, Does.Contain("world.Create(new ComponentId[] { id }, 1, output)"));
        Assert.That(diagnosticText, Does.Contain("world.Create(new ComponentId[] { id })"));
        Assert.That(diagnosticText, Does.Contain("Other.Use(new int[] { 1 })"));
        Assert.That(diagnosticText, Does.Not.Contain("new Position(), new Health()"));
        Assert.That(diagnosticText, Does.Not.Contain("world.ForEach(new[]"));
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        IEnumerable<MetadataReference> references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "CollectionExpressionSuppressorHarness",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

#pragma warning disable RS1041, RS1036, RS2008
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class Ide0300TestAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new(
            "IDE0300",
            "Use collection expression",
            "Use collection expression",
            "Style",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeArray,
                SyntaxKind.ArrayCreationExpression,
                SyntaxKind.ImplicitArrayCreationExpression);
        }

        private static void AnalyzeArray(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
            }
        }
    }
#pragma warning restore RS1041, RS1036, RS2008
}
