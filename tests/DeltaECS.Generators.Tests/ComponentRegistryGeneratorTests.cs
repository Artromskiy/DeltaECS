using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using NUnit.Framework;

namespace Delta.ECS.Generators.Tests;

public sealed class ComponentRegistryGeneratorTests
{
    private static readonly string[] ExpectedRegisteredComponentNames = ["Example.Position", "Example.Health", "Example.Marker"];

    [Test]
    public void GeneratesStableCatalogFactoriesAndComponentTypeTokens()
    {
        const string source = """
            using Delta.ECS;
            namespace Example
            {
                [ComponentSchema]
                [ComponentAlias("position")]
                public struct Position { public float X; }

                [ComponentSchema(42UL)]
                public struct Health { public float Value; }

                [ComponentSchema]
                public struct Marker { }

                public static class Entry
                {
                    public static void Touch() { }

                    public static IGeneratedComponentRegistration GetPositionRegistration()
                        => GeneratedComponentCatalog.GetRegistration<Position>();
                }
            }
            """;
        CSharpCompilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[]
            {
                new ComponentRegistryGenerator().AsSourceGenerator(),
                new GeneratedGenericBindingsGenerator().AsSourceGenerator(),
            },
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);

        Assert.That(generatorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);

        string[] generatedSources = driver.GetRunResult().GeneratedTrees.Select(static tree => tree.GetText().ToString()).ToArray();
        string catalog = string.Join("\n", generatedSources);
        Assert.That(catalog, Does.Contain("GeneratedComponentRegistrationRegistry.Register(new"));
        Assert.That(catalog, Does.Contain("static partial void RegisterComponentCatalog()"));
        Assert.That(catalog, Does.Contain("ComponentType => typeof(global::Example.Position)"));
        Assert.That(catalog, Does.Contain("ComponentType => typeof(global::Example.Health)"));
        Assert.That(catalog, Does.Contain("IGeneratedComponentRegistration.Register("));
        Assert.That(catalog, Does.Not.Contain("global::Delta.ECS.World world"));
        Assert.That(catalog, Does.Contain("layouts.AddAlias(\"position\", componentId);"));
        Assert.That(catalog, Does.Contain("ComponentRegistryRegistrationExtensions.Register<global::Example.Position>"));
        Assert.That(catalog, Does.Contain("ComponentRegistryRegistrationExtensions.Register<global::Example.Health>"));
        Assert.That(catalog, Does.Contain("SchemaId => new(0x000000000000002AUL)"));
        Assert.That(catalog, Does.Contain("public bool IsTag => true;"));
        Assert.That(catalog, Does.Contain("public bool IsTag => false;"));

        ulong expectedPositionId = ComputeFNV1A("Example.Position");
        Assert.That(catalog, Does.Contain($"SchemaId => new(0x{expectedPositionId:X16}UL)"));
        Assert.That(catalog, Does.Contain("RegisteredComponentTypeToken_"));
        Assert.That(catalog, Does.Contain("visitor.Visit<global::Example.Position>"));
        Assert.That(catalog, Does.Contain("visitor.Visit<global::Example.Health>"));
        Assert.That(catalog, Does.Contain("visitor.Visit<global::Example.Marker>"));

        using var assemblyImage = new MemoryStream();
        EmitResult emit = output.Emit(assemblyImage);
        Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));
        assemblyImage.Position = 0;
        Assembly generatedAssembly = AssemblyLoadContext.Default.LoadFromStream(assemblyImage);
        generatedAssembly.GetType("Example.Entry")!
            .GetMethod("Touch", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null);

        IGeneratedComponentRegistration[] registered = GeneratedComponentCatalog.GetRegistrations();
        Assert.That(registered, Has.Length.EqualTo(3));
        Assert.That(registered.Select(static item => item.ComponentType.FullName),
            Is.EquivalentTo(ExpectedRegisteredComponentNames));
        Assert.That(registered.Single(static item => item.ComponentType.FullName == "Example.Position").IsTag, Is.False);
        Assert.That(registered.Single(static item => item.ComponentType.FullName == "Example.Marker").IsTag, Is.True);

        var layouts = new ComponentRegistry();
        foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
        {
            layouts.Register(registration);
        }

        Assert.That(layouts.TryGetPrimary(generatedAssembly.GetType("Example.Position")!, out ComponentId positionId), Is.True);
        Assert.That(layouts.GetComponentType(positionId).FullName, Is.EqualTo("Example.Position"));
        Assert.That(layouts.TryGetPrimary(generatedAssembly.GetType("Example.Health")!, out ComponentId healthId), Is.True);
        Assert.That(layouts.GetComponentType(healthId).FullName, Is.EqualTo("Example.Health"));

        Assert.That(layouts.TryGetPrimary(generatedAssembly.GetType("Example.Marker")!, out ComponentId markerId), Is.True);
        using var generatedWorld = new World(layouts);
        Assert.That(generatedWorld.GetAlias("position"), Is.EqualTo(positionId));
        Entity marker = generatedWorld.Create(markerId);
        Assert.That(generatedWorld.Has(marker, markerId), Is.True);

        var selectedRegistrationLayouts = new ComponentRegistry();
        var entryType = generatedAssembly.GetType("Example.Entry")!;
        var positionRegistration = (IGeneratedComponentRegistration)entryType
            .GetMethod("GetPositionRegistration", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        ComponentId selectedPositionId = selectedRegistrationLayouts.Register(positionRegistration);
        Assert.That(selectedRegistrationLayouts.GetComponentType(selectedPositionId).FullName, Is.EqualTo("Example.Position"));
        Assert.That(selectedRegistrationLayouts.GetAlias("position"), Is.EqualTo(selectedPositionId));
        Assert.Throws<InvalidOperationException>(() => GeneratedComponentCatalog.GetRegistration<Guid>());
    }

    [Test]
    public async Task AnalyzerReportsGeneratedIdAndRejectsZero()
    {
        const string source = """
            using Delta.ECS;
            namespace Example
            {
                [ComponentSchema]
                public struct Position { }

                [ComponentSchema(0UL)]
                public struct ZeroSchema { }
            }
            """;
        CSharpCompilation compilation = CreateCompilation(source);

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ComponentSchemaIdAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);

        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Does.Contain("DECS0001"));
        Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Does.Contain("DECS0002"));
    }

    [Test]
    public async Task SchemaIdCodeFixPinsTheNameDerivedId()
    {
        const string source = "using Delta.ECS;\n[ComponentSchema]\npublic struct Position { }";
        CSharpCompilation compilation = CreateCompilation(source);
        Diagnostic diagnostic = (await compilation
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ComponentSchemaIdAnalyzer()))
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(false))
            .Single(static item => item.Id == ComponentSchemaIdAnalyzer.PinSchemaIdDiagnosticId);

        using var workspace = new AdhocWorkspace();
        ProjectId projectId = ProjectId.CreateNewId();
        DocumentId documentId = DocumentId.CreateNewId(projectId);
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(World).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        Solution solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "CodeFixTest",
                "CodeFixTest",
                LanguageNames.CSharp,
                parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: references))
            .AddDocument(documentId, "Components.cs", SourceText.From(source));
        Document document = solution.GetDocument(documentId)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);

        await new ComponentSchemaIdCodeFixProvider().RegisterCodeFixesAsync(context).ConfigureAwait(false);

        Assert.That(actions, Has.Count.EqualTo(1));
        ImmutableArray<CodeActionOperation> operations = await actions[0]
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Solution fixedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        string fixedSource = (await fixedSolution.GetDocument(documentId)!.GetTextAsync().ConfigureAwait(false)).ToString();
        ulong expectedSchemaId = ComputeFNV1A("Position");

        Assert.That(fixedSource, Does.Contain($"[ComponentSchema(0x{expectedSchemaId:X16}UL)]"));
    }

    [Test]
    public void RegistryGeneratorRejectsZeroAndDuplicateSchemaIds()
    {
        const string source = """
            using Delta.ECS;
            namespace Example
            {
                [ComponentSchema(0UL)]
                public struct ZeroSchema { }

                [ComponentSchema(42UL)]
                public struct First { }

                [ComponentSchema(42UL)]
                public struct Second { }
            }
            """;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new ComponentRegistryGenerator().AsSourceGenerator() },
            parseOptions: new CSharpParseOptions(LanguageVersion.CSharp9));

        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out _, out _);

        string[] diagnosticIds = driver.GetRunResult().Diagnostics.Select(static diagnostic => diagnostic.Id).ToArray();
        Assert.That(diagnosticIds, Does.Contain("DECS0004"));
        Assert.That(diagnosticIds, Does.Contain("DECS0005"));
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(World).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "ComponentRegistryConsumer",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static ulong ComputeFNV1A(string value)
    {
        const ulong basis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = basis;
        foreach (byte valueByte in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ valueByte) * prime;
        }

        return hash;
    }
}
