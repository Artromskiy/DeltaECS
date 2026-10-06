using Microsoft.CodeAnalysis;

namespace Delta.ECS.Generators;

internal static class GeneratorDiagnostics
{
    internal static DiagnosticDescriptor Error(
        string id,
        string title,
        string message,
        string category,
        string? description = null)
        => Create(id, title, message, category, DiagnosticSeverity.Error, description);

    internal static DiagnosticDescriptor Info(
        string id,
        string title,
        string message,
        string category,
        string? description = null)
        => Create(id, title, message, category, DiagnosticSeverity.Info, description);

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string message,
        string category,
        DiagnosticSeverity severity,
        string? description)
        => new(
            id,
            title,
            message,
            category,
            severity,
            isEnabledByDefault: true,
            description: description);
}
