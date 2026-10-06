namespace Delta.ECS.Generators;

internal sealed record ComponentComparerSignature(
    string[] ComponentTypes,
    bool HasContext,
    string? ContextType,
    ContextModeKind ContextMode,
    bool HasEntity)
{
}

internal sealed record ComponentComparerModel(
    string FunctorType,
    string FunctorDisplayName,
    bool IsReferenceType,
    ComponentComparerSignature? Signature)
{
    internal string[] ComponentTypes => Signature?.ComponentTypes ?? Array.Empty<string>();

    internal string Key =>
        $"{FunctorType}|{string.Join("|", ComponentTypes)}|{Signature?.HasContext}|{Signature?.ContextType}|"
        + $"{Signature?.ContextMode}|{Signature?.HasEntity}";
}
