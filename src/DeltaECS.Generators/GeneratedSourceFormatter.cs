using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Delta.ECS.Generators;

internal static class GeneratedSourceFormatter
{
    public static string Format(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        return tree.GetRoot().NormalizeWhitespace(indentation: "    ", eol: "\n").ToFullString();
    }
}
