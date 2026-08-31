namespace Delta.ECS.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

[TestFixture]
public sealed class ThrowHelperPlacementTests
{
    [Test]
    public void ExplicitThrowKeywords_AreOnlyInThrowHelpers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !string.Equals(
                Path.GetFileName(path),
                "ThrowHelpers.cs",
                StringComparison.Ordinal)
                && !path.Contains(
                    Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)
                && !path.Contains(
                    Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            .Select(path => new
            {
                Path = path,
                Text = File.ReadAllText(path)
            })
            .Where(item => Regex.IsMatch(item.Text, @"\bthrow\b", RegexOptions.CultureInvariant))
            .Select(static item => item.Path)
            .ToArray();

        Assert.That(
            offenders,
            Is.Empty,
            "Explicit throw keywords must stay in ThrowHelpers.cs: "
                + string.Join(", ", offenders.Select(Path.GetFileName)));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeltaECS.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the DeltaECS repository root.");
    }
}
