namespace Delta.ECS.Generators;

using System.Text;
using Microsoft.CodeAnalysis;

internal static class ComponentSchemaIdHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    internal static ulong Compute(string metadataName)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(metadataName);
        ulong hash = FnvOffsetBasis;
        for (int index = 0; index < bytes.Length; index++)
        {
            hash = (hash ^ bytes[index]) * FnvPrime;
        }

        return hash;
    }

    internal static string GetMetadataFullName(INamedTypeSymbol type)
    {
        var namespaceParts = new Stack<string>();
        for (INamespaceSymbol? current = type.ContainingNamespace; current is not null && !current.IsGlobalNamespace; current = current.ContainingNamespace)
        {
            namespaceParts.Push(current.Name);
        }

        var typeParts = new Stack<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            typeParts.Push(current.MetadataName);
        }

        var result = new StringBuilder();
        while (namespaceParts.Count > 0)
        {
            if (result.Length > 0)
            {
                result.Append('.');
            }

            result.Append(namespaceParts.Pop());
        }

        if (result.Length > 0)
        {
            result.Append('.');
        }

        bool firstTypePart = true;
        while (typeParts.Count > 0)
        {
            if (!firstTypePart)
            {
                result.Append('+');
            }

            result.Append(typeParts.Pop());
            firstTypePart = false;
        }

        return result.ToString();
    }
}
