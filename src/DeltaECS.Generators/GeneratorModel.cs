using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Delta.ECS.Generators;

internal readonly struct InvocationCandidate
{
    internal InvocationCandidate(Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation)
        => Invocation = invocation;

    internal Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax Invocation { get; }
}

internal enum OperationKind
{
    Iteration,
    StampIteration,
    QueryFactory,
    Structural,
    Where
}

internal enum GeneratedApiKind
{
    Unknown,
    Structural,
    QueryFactory,
    Iteration,
    Where
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

internal enum SelectorKind
{
    Inferred,
    Generic,
    ComponentIds
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
    MethodGroup,
    Functor
}

internal enum ExecutionKind
{
    Dense,
    EntityList,
    Parallel
}

internal enum ValueKind
{
    Component,
    Stamp
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
    Set,
    Create
}

internal readonly struct ComponentSlot
{
    internal ComponentSlot(int position, string typeName, SelectorKind selector, AccessKind access)
    {
        Position = position;
        TypeName = typeName;
        Selector = selector;
        Access = access;
    }

    internal int Position { get; }
    internal string TypeName { get; }
    internal SelectorKind Selector { get; }
    internal AccessKind Access { get; }

}

internal readonly struct SelectorSpec
{
    internal SelectorSpec(SelectorKind kind, ImmutableArray<ComponentSlot> components)
    {
        Kind = kind;
        Components = components;
    }

    internal SelectorKind Kind { get; }
    internal ImmutableArray<ComponentSlot> Components { get; }
}

internal readonly struct ContextSpec
{
    internal ContextSpec(ContextModeKind mode, string? typeName)
    {
        Mode = mode;
        TypeName = typeName;
    }

    internal ContextModeKind Mode { get; }
    internal string? TypeName { get; }
}

internal readonly struct CallbackSpec
{
    internal CallbackSpec(CallbackSource source, bool hasEntity, string? typeName)
    {
        Source = source;
        HasEntity = hasEntity;
        TypeName = typeName;
    }

    internal CallbackSource Source { get; }
    internal bool HasEntity { get; }
    internal string? TypeName { get; }
}

internal readonly struct ExecutionSpec
{
    internal ExecutionSpec(ExecutionKind kind, ValueKind value)
    {
        Kind = kind;
        Value = value;
    }

    internal ExecutionKind Kind { get; }
    internal ValueKind Value { get; }
}

internal sealed class ApiShape
{
    internal ApiShape(
        OperationKind operation,
        TargetKind target,
        QueryMode query,
        SelectorSpec selector,
        ContextSpec context,
        CallbackSpec? callback,
        ExecutionSpec execution,
        string? name = null,
        string? pattern = null)
    {
        Operation = operation;
        Target = target;
        Query = query;
        Selector = selector;
        Context = context;
        Callback = callback;
        Execution = execution;
        Name = name;
        Pattern = pattern;
        _signatureKey = BuildSignatureKey();
    }

    internal OperationKind Operation { get; }
    internal TargetKind Target { get; }
    internal QueryMode Query { get; }
    internal SelectorSpec Selector { get; }
    internal ContextSpec Context { get; }
    internal CallbackSpec? Callback { get; }
    internal ExecutionSpec Execution { get; }
    internal string? Name { get; }
    internal string? Pattern { get; }

    private readonly string _signatureKey;

    internal string SignatureKey => _signatureKey;

    private string BuildSignatureKey()
        => string.Join(
            "|",
            Operation,
            Name,
            Pattern,
            Target,
            Query,
            Selector.Kind,
            Context.Mode,
            Context.TypeName,
            Callback?.Source,
            Callback?.HasEntity,
            Callback?.TypeName,
            Execution.Kind,
            Execution.Value,
            string.Join(";", Selector.Components.Select(static component =>
                component.Position.ToString(CultureInfo.InvariantCulture)
                + ":" + component.TypeName
                + ":" + component.Selector
                + ":" + component.Access)));
}

internal sealed class StructuralPlan
{
    internal StructuralPlan(StructuralOperation operation) => Operation = operation;

    internal StructuralOperation Operation { get; }
}
