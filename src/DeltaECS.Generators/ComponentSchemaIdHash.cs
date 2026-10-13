namespace Delta.ECS.Generators;

using System.Text;

using Microsoft.CodeAnalysis;

internal static class ComponentSchemaIdHash
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    internal static AttributeData? ComponentAttribute(INamedTypeSymbol type)
        => type.GetAttributes().FirstOrDefault(static attribute =>
            GeneratorSupport.IsNamedType(attribute.AttributeClass, "ComponentSchemaAttribute"));

    internal static (bool IsPresent, ulong Value) ExplicitSchemaId(AttributeData attribute)
    {
        return attribute.ConstructorArguments.Length != 0
            ? (true, attribute.ConstructorArguments[0].Value is ulong value ? value : 0UL)
            : default;
    }

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
        string namespaceName = string.Join(".", NamespaceParts(type.ContainingNamespace));
        string typeName = string.Join("+", TypeParts(type));
        return namespaceName.Length == 0 ? typeName : namespaceName + "." + typeName;
    }

    private static IEnumerable<string> NamespaceParts(INamespaceSymbol type)
    {
        var parts = new Stack<string>();
        for (INamespaceSymbol? current = type; current is not null && !current.IsGlobalNamespace; current = current.ContainingNamespace)
        {
            parts.Push(current.Name);
        }

        return parts;
    }

    private static IEnumerable<string> TypeParts(INamedTypeSymbol type)
    {
        var parts = new Stack<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            parts.Push(current.MetadataName);
        }

        return parts;
    }
}
