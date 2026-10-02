using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

internal enum GenericTypeConstraintKind
{
    None,
    Struct,
    Unmanaged,
    Class,
    NullableClass,
    New,
    ClassNew,
    NullableClassNew,
}

internal sealed class GenericTypeParameterConstraint(int index, GenericTypeConstraintKind kind)
{
    internal int Index { get; } = index;
    internal GenericTypeConstraintKind Kind { get; } = kind;

    internal string RenderClause(string generatedTypeParameter)
    {
        string constraint = Kind switch
        {
            GenericTypeConstraintKind.Struct => "struct",
            GenericTypeConstraintKind.Unmanaged => "unmanaged",
            GenericTypeConstraintKind.Class => "class",
            GenericTypeConstraintKind.NullableClass => "class?",
            GenericTypeConstraintKind.New => "new()",
            GenericTypeConstraintKind.ClassNew => "class, new()",
            GenericTypeConstraintKind.NullableClassNew => "class?, new()",
            _ => string.Empty,
        };
        return constraint.Length == 0 ? string.Empty : $"where {generatedTypeParameter} : {constraint}";
    }
}

internal static class GenericTypeConstraintSupport
{
    internal static bool TryGetSupported(
        INamedTypeSymbol type,
        out ImmutableArray<GenericTypeParameterConstraint> constraints)
    {
        if (type.ContainingType is { IsGenericType: true })
        {
            constraints = default;
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<GenericTypeParameterConstraint>(type.TypeParameters.Length);
        for (int index = 0; index < type.TypeParameters.Length; index++)
        {
            ITypeParameterSymbol parameter = type.TypeParameters[index];
            bool notNullIsImpliedByPrimaryConstraint = parameter.HasUnmanagedTypeConstraint
                || parameter.HasValueTypeConstraint
                || parameter.HasReferenceTypeConstraint;
            if (parameter.HasNotNullConstraint && !notNullIsImpliedByPrimaryConstraint
                || parameter.ConstraintTypes.Length != 0)
            {
                constraints = default;
                return false;
            }

            GenericTypeConstraintKind kind = parameter.HasUnmanagedTypeConstraint
                ? GenericTypeConstraintKind.Unmanaged
                : parameter.HasValueTypeConstraint
                    ? GenericTypeConstraintKind.Struct
                    : parameter.HasReferenceTypeConstraint
                        ? parameter.HasConstructorConstraint
                            ? parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                                ? GenericTypeConstraintKind.NullableClassNew
                                : GenericTypeConstraintKind.ClassNew
                            : parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                                ? GenericTypeConstraintKind.NullableClass
                                : GenericTypeConstraintKind.Class
                        : parameter.HasConstructorConstraint
                            ? GenericTypeConstraintKind.New
                            : GenericTypeConstraintKind.None;
            builder.Add(new GenericTypeParameterConstraint(index, kind));
        }

        constraints = builder.ToImmutable();
        return true;
    }

    internal static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
        => type.SpecialType != SpecialType.System_Nullable_T
            && (type.IsValueType || !type.IsAbstract && type.InstanceConstructors.Any(static constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 0));
}

internal sealed class GeneratedTypeTokenBinding(
    string componentTypeName,
    string tokenName,
    bool isValueType,
    bool isUnmanaged,
    bool isClass,
    bool hasPublicParameterlessConstructor)
{
    internal string ComponentTypeName { get; } = componentTypeName;
    internal string TokenName { get; } = tokenName;
    internal bool IsValueType { get; } = isValueType;
    internal bool IsUnmanaged { get; } = isUnmanaged;
    internal bool IsClass { get; } = isClass;
    internal bool HasPublicParameterlessConstructor { get; } = hasPublicParameterlessConstructor;
}
