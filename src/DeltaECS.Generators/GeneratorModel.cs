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
        string? componentIdExpression = null,
        string? resolvedTypeName = null,
        string? genericTypeName = null)
    {
        Position = position;
        TypeName = typeName;
        ResolvedTypeName = resolvedTypeName ?? typeName;
        GenericTypeName = genericTypeName ?? typeName;
        Selector = selector;
        Access = access;
        ParameterName = parameterName ?? "component" + position.ToString(CultureInfo.InvariantCulture);
        ComponentIdExpression = componentIdExpression;
    }

    internal int Position { get; }
    internal int Index => Position;
    internal string TypeName { get; }
    internal string ResolvedTypeName { get; }
    internal string GenericTypeName { get; }
    internal string ParameterName { get; }
    internal SelectorKind Selector { get; }
    internal AccessKind Access { get; }
    internal bool IsGeneric => Selector == SelectorKind.Generic;
    internal bool IsComponentId => Selector == SelectorKind.ComponentIds;
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
        string? pattern = null,
        string? summary = null)
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
        Summary = summary ?? BuildSummary();
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
    internal string Summary { get; }

    private readonly string _signatureKey;

    internal string SignatureKey => _signatureKey;

    private string BuildSummary()
        => Operation switch
        {
            OperationKind.QueryFactory => $"Builds a query using {Name} component constraints.",
            OperationKind.Structural => $"Executes the generated {Name?.Split('|')[0] ?? "structural"} operation.",
            OperationKind.StampIteration => $"Iterates matching entities and reads their component stamps.",
            OperationKind.Iteration => $"Iterates matching entities and components using {Name?.Split('|')[0] ?? "ForEach"}.",
            OperationKind.Where => "Creates a read-only filtered query view.",
            _ => "Executes a generated ECS operation."
        };

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
                + ":" + component.ComponentIdExpression
                + ":" + component.GenericTypeName)));
}

internal sealed class StructuralPlan
{
    internal StructuralPlan(StructuralOperation operation) => Operation = operation;

    internal StructuralOperation Operation { get; }
}
