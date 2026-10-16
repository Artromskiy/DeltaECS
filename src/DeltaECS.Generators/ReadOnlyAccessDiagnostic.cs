using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

internal static class ReadOnlyAccessDiagnostic
{
    internal const string DiagnosticId = "DECSGEN013";
    internal const string ModifierProperty = "Modifier";
    internal const string RefLocalProperty = "RefLocal";

    internal static readonly DiagnosticDescriptor Rule = GeneratorDiagnostics.Info(
        DiagnosticId,
        "Use read-only component access",
        "'{0}' is only read; use '{1}'",
        "Component access",
        "Use a read-only callback parameter or GetReadRef when a component is not modified.");
}
