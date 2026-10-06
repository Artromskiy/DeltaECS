using System.Collections.Immutable;

namespace Delta.ECS.Generators;

internal readonly record struct InvocationCandidate(
    Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax Invocation);

internal enum OperationKind
{
    Iteration,
    QueryFactory,
    Structural,
    Where
}

internal enum ValueDomain
{
    Component,
    Stamp
}

internal enum Schedule
{
    Sequential,
    Parallel
}

internal enum Scope
{
    QueryWide,
    EntityList
}

internal enum GeneratedApiKind
{
    Unknown,
    Structural,
    QueryFactory,
    Iteration,
    Where,
    Ordering
}

internal enum TargetKind
{
    World,
    Entity,
    EntityList,
    Query
}

internal enum QueryMode
{
    None,
    Required,
    Optional
}

internal enum TypeBindingKind
{
    Generic,
    CallbackInferred,
    None
}

internal enum RegistrationBindingKind
{
    Primary,
    Explicit,
    Dynamic
}

internal enum AccessKind
{
    Value,
    RowRead,
    RowWrite,
    RefReadonly,
    StampRead
}

internal enum CallbackSource
{
    Lambda,
    Functor
}

internal enum ContextModeKind
{
    None,
    Value,
    In,
    RefReadonly,
    Ref
}

internal enum StructuralOperation
{
    Add,
    Remove,
    Create
}

internal readonly record struct ComponentModel(
    string TypeName,
    AccessKind Access,
    string? ResolvedTypeName = null)
{
    internal string ResolvedTypeName { get; } = ResolvedTypeName ?? TypeName;
    internal bool IsWrite => Access == AccessKind.RowWrite;
    internal string ParameterModifier => Access switch
    {
        AccessKind.RowWrite => "ref ",
        AccessKind.RefReadonly => "ref readonly ",
        AccessKind.StampRead or AccessKind.RowRead => "in ",
        _ => string.Empty
    };
    internal string InvocationModifier => Access switch
    {
        AccessKind.RowWrite => "ref ",
        AccessKind.RefReadonly or AccessKind.StampRead or AccessKind.RowRead => "in ",
        _ => string.Empty
    };
}

internal readonly record struct SelectorModel(
    TypeBindingKind TypeBinding,
    RegistrationBindingKind RegistrationBinding,
    ImmutableArray<ComponentModel> Components)
{
    internal int Arity => Components.Length;
}

internal readonly record struct ContextModel(ContextModeKind Mode, string? TypeName);

internal readonly record struct CallbackModel(
    CallbackSource Source,
    bool HasEntity,
    string? TypeName,
    ContextModeKind PassMode = ContextModeKind.None)
{
    /// <summary>How a functor argument is passed: <c>ref</c>, <c>in</c>, <c>ref readonly</c>, or by value.</summary>
    internal ContextModeKind PassMode { get; } = Source == CallbackSource.Functor
        ? (PassMode == ContextModeKind.None ? ContextModeKind.Ref : PassMode)
        : ContextModeKind.None;
}

internal readonly record struct ExecutionModel(Scope Scope, ValueDomain Value, Schedule Schedule);

internal sealed record ApiModel(
    OperationKind Operation,
    TargetKind Target,
    QueryMode Query,
    SelectorModel Selector,
    ContextModel Context,
    CallbackModel? Callback,
    ExecutionModel Execution,
    string? Name = null,
    string? SummaryText = null)
{
    internal string Summary => SummaryText ?? BuildSummary(Operation, Name);
    internal SignatureProjection Signature { get; } = new(Selector);
    internal string SignatureKey { get; } = BuildSignatureKey(
        Operation,
        Target,
        Query,
        Selector,
        Context,
        Callback,
        Execution,
        Name);

    private static string BuildSummary(OperationKind operation, string? name)
        => operation switch
        {
            OperationKind.QueryFactory => $"Builds a query using {name} component constraints.",
            OperationKind.Structural => $"Executes the generated {name ?? "structural"} operation.",
            OperationKind.Iteration => $"Iterates matching entities and components using {name ?? "ForEach"}.",
            OperationKind.Where => "Creates a read-only filtered query view.",
            _ => "Executes a generated ECS operation."
        };

    private static string BuildSignatureKey(
        OperationKind operation,
        TargetKind target,
        QueryMode query,
        SelectorModel selector,
        ContextModel context,
        CallbackModel? callback,
        ExecutionModel execution,
        string? name)
    {
        bool genericContext = callback?.Source != CallbackSource.Functor
            && (operation == OperationKind.Where || selector.TypeBinding == TypeBindingKind.Generic);
        return string.Join(
            "|",
            operation,
            name,
            target,
            query,
            selector.TypeBinding,
            selector.RegistrationBinding,
            context.Mode,
            genericContext ? string.Empty : context.TypeName,
            callback?.Source,
            callback?.HasEntity,
            callback?.TypeName,
            callback?.PassMode,
            execution.Scope,
            execution.Value,
            execution.Schedule,
            string.Join(";", selector.Components.Select((component, index) =>
                index + ":" + component.TypeName + ":"
                    + (selector.TypeBinding == TypeBindingKind.Generic ? string.Empty : component.ResolvedTypeName)
                    + ":" + component.Access)));
    }
}
