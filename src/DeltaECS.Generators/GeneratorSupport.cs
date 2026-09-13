using System;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

internal static class GeneratorSupport
{
    internal static bool IsNamedType(ITypeSymbol? type, string name)
        => type is INamedTypeSymbol named
            && named.Name == name
            && named.ContainingNamespace.ToDisplayString() == "Delta.ECS";

    internal static string GenericTypes(int arity, string prefix = "T")
    {
        var values = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            values[index] = prefix + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(", ", values);
    }

    internal static string StableName(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash = (hash ^ character) * 16777619;
            }

            return hash.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
