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

internal readonly struct ComponentModel
{
    internal ComponentModel(
        int position,
        string typeName,
        SelectorKind selector,
        AccessKind access,
        string? parameterName = null,
        string? componentIdExpression = null)
    {
        Position = position;
        TypeName = typeName;
        Selector = selector;
        Access = access;
        ParameterName = parameterName ?? "component" + position.ToString(CultureInfo.InvariantCulture);
        ComponentIdExpression = componentIdExpression;
    }

    internal int Position { get; }
    internal int Index => Position;
    internal string TypeName { get; }
    internal string ParameterName { get; }
    internal SelectorKind Selector { get; }
    internal AccessKind Access { get; }
    internal char AccessMode => Access switch
    {
        AccessKind.RowWrite => 'W',
        AccessKind.RefReadonly => 'R',
        AccessKind.StampRead => 'S',
        AccessKind.RowRead => 'I',
        _ => 'V'
    };
    internal string? ComponentIdExpression { get; }

}

internal readonly struct SelectorModel
{
    internal SelectorModel(SelectorKind kind, ImmutableArray<ComponentModel> components)
    {
        Kind = kind;
        Components = components;
    }

    internal SelectorKind Kind { get; }
    internal ImmutableArray<ComponentModel> Components { get; }
}

internal readonly struct ContextModel
{
    internal ContextModel(ContextModeKind mode, string? typeName)
    {
        Mode = mode;
        TypeName = typeName;
    }

    internal ContextModeKind Mode { get; }
    internal string? TypeName { get; }
}

internal readonly struct CallbackModel
{
    internal CallbackModel(CallbackSource source, bool hasEntity, string? typeName)
    {
        Source = source;
        HasEntity = hasEntity;
        TypeName = typeName;
    }

    internal CallbackSource Source { get; }
    internal bool HasEntity { get; }
    internal string? TypeName { get; }
}

internal readonly struct ExecutionModel
{
    internal ExecutionModel(ExecutionKind kind, ValueKind value)
    {
        Kind = kind;
        Value = value;
    }

    internal ExecutionKind Kind { get; }
    internal ValueKind Value { get; }
}

internal sealed class ApiModel
{
    internal ApiModel(
        OperationKind operation,
        TargetKind target,
        QueryMode query,
        SelectorModel selector,
        ContextModel context,
        CallbackModel? callback,
        ExecutionModel execution,
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
    internal SelectorModel Selector { get; }
    internal ContextModel Context { get; }
    internal CallbackModel? Callback { get; }
    internal ExecutionModel Execution { get; }
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
                + ":" + component.Access
                + ":" + component.ParameterName
                + ":" + component.AccessMode
                + ":" + component.ComponentIdExpression)));
}

internal sealed class StructuralPlan
{
    internal StructuralPlan(StructuralOperation operation) => Operation = operation;

    internal StructuralOperation Operation { get; }
}
