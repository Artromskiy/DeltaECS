using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

[TestFixture]
public sealed class ImplicitLambdaDiagnosticSuppressorTests
{
    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
        new Ide0350TestAnalyzer(),
        new ImplicitLambdaDiagnosticSuppressor());

    [Test]
    public async Task SuppressesImplicitLambdaHintOnlyWhenForEachTypesWouldBeErased()
    {
        const string source = """
            using System;

            namespace Delta.ECS
            {
                public readonly struct Query { }
                public readonly struct ComponentId { }
                public readonly struct Cmp { }
                public readonly struct EntityRef { }
                public delegate void ComponentAction<T>(in T component);
                public delegate void EntityAction<T>(T entity);

                public sealed class World
                {
                    public void ForEach(in Query query, ComponentId componentId, ComponentAction<Cmp> action) { }
                    public void ForEach<T>(in Query query, ComponentId componentId, ComponentAction<T> action) { }
                    public void ForEachEntity(in Query query, EntityAction<EntityRef> action) { }
                }
            }

            namespace Consumer
            {
                using Delta.ECS;

                public static class Other
                {
                    public static void ForEach(Action<int> action) { }
                }

                public static class CallSites
                {
                    public static void Use(World world, in Query query, ComponentId componentId)
                    {
                        world.ForEach(in query, componentId, static (in Cmp value) => { });
                        world.ForEach<Cmp>(in query, componentId, static (in Cmp value) => { });
                        world.ForEachEntity(in query, static (EntityRef entity) => { });
                        Other.ForEach(static (int value) => { });
                    }
                }
            }
            """;

        CSharpCompilation compilation = CreateCompilation(source);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(Analyzers)
            .GetAnalyzerDiagnosticsAsync();

        string[] diagnosticLines = diagnostics
            .Select(diagnostic => source.Split('\n')[diagnostic.Location.GetLineSpan().StartLinePosition.Line].Trim())
            .ToArray();
        Assert.That(diagnosticLines, Has.Length.EqualTo(2));
        string diagnosticText = string.Join('\n', diagnosticLines);
        Assert.That(diagnosticText, Does.Contain("world.ForEach<Cmp>"));
        Assert.That(diagnosticText, Does.Contain("Other.ForEach"));
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        IEnumerable<MetadataReference> references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "SuppressorHarness",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

#pragma warning disable RS1041, RS1036, RS2008
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class Ide0350TestAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new(
            "IDE0350",
            "Use implicitly typed lambda",
            "Use an implicitly typed lambda",
            "Style",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(
                AnalyzeLambda,
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression);
        }

        private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LambdaExpressionSyntax lambda
                && lambda.Parent is ArgumentSyntax)
            {
                Location location = lambda switch
                {
                    ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.GetLocation(),
                    SimpleLambdaExpressionSyntax simple => simple.Parameter.GetLocation(),
                    _ => lambda.GetLocation()
                };
                context.ReportDiagnostic(Diagnostic.Create(Rule, location));
            }
        }
    }
#pragma warning restore RS1041, RS1036, RS2008
}
