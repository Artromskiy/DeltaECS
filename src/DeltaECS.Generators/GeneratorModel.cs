using System.Collections.Immutable;

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

internal enum TypeBindingKind
{
    Generic,
    CallbackInferred,
    None
}

internal enum RegistrationBindingKind
{
    Primary,
    Explicit
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
    Set,
    Create
}

internal readonly struct ComponentModel
{
    internal ComponentModel(
        string typeName,
        AccessKind access,
        string? resolvedTypeName = null)
    {
        TypeName = typeName;
        ResolvedTypeName = resolvedTypeName ?? typeName;
        Access = access;
    }

    internal string TypeName { get; }
    internal string ResolvedTypeName { get; }
    internal AccessKind Access { get; }
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

internal readonly struct SelectorModel
{
    internal SelectorModel(
        TypeBindingKind typeBinding,
        RegistrationBindingKind registrationBinding,
        ImmutableArray<ComponentModel> components)
    {
        TypeBinding = typeBinding;
        RegistrationBinding = registrationBinding;
        Components = components;
    }

    internal TypeBindingKind TypeBinding { get; }
    internal RegistrationBindingKind RegistrationBinding { get; }
    internal ImmutableArray<ComponentModel> Components { get; }
    internal int Arity => Components.Length;
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
    internal CallbackModel(
        CallbackSource source,
        bool hasEntity,
        string? typeName,
        ContextModeKind passMode = ContextModeKind.None)
    {
        Source = source;
        HasEntity = hasEntity;
        TypeName = typeName;
        PassMode = source == CallbackSource.Functor
            ? (passMode == ContextModeKind.None ? ContextModeKind.Ref : passMode)
            : ContextModeKind.None;
    }

    internal CallbackSource Source { get; }
    internal bool HasEntity { get; }
    internal string? TypeName { get; }
    /// <summary>How a functor argument is passed: <c>ref</c>, <c>in</c>, <c>ref readonly</c>, or by value.</summary>
    internal ContextModeKind PassMode { get; }
}

internal readonly struct ExecutionModel
{
    internal ExecutionModel(Scope scope, ValueDomain value, Schedule schedule)
    {
        Scope = scope;
        Value = value;
        Schedule = schedule;
    }

    internal Scope Scope { get; }
    internal ValueDomain Value { get; }
    internal Schedule Schedule { get; }
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
        Summary = summary ?? BuildSummary();
        _signature = new SignatureProjection(this);
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
    internal string Summary { get; }
    internal SignatureProjection Signature => _signature;

    private readonly SignatureProjection _signature;
    private readonly string _signatureKey;

    internal string SignatureKey => _signatureKey;

    private string BuildSummary()
        => Operation switch
        {
            OperationKind.QueryFactory => $"Builds a query using {Name} component constraints.",
            OperationKind.Structural => $"Executes the generated {Name ?? "structural"} operation.",
            OperationKind.Iteration => $"Iterates matching entities and components using {Name ?? "ForEach"}.",
            OperationKind.Where => "Creates a read-only filtered query view.",
            _ => "Executes a generated ECS operation."
        };

    private string BuildSignatureKey()
    {
        bool genericContext = Callback?.Source != CallbackSource.Functor
            && (Operation == OperationKind.Where || Selector.TypeBinding == TypeBindingKind.Generic);
        return string.Join(
            "|",
            Operation,
            Name,
            Target,
            Query,
            Selector.TypeBinding,
            Selector.RegistrationBinding,
            Context.Mode,
            genericContext ? string.Empty : Context.TypeName,
            Callback?.Source,
            Callback?.HasEntity,
            Callback?.TypeName,
            Callback?.PassMode,
            Execution.Scope,
            Execution.Value,
            Execution.Schedule,
            string.Join(";", Selector.Components.Select((component, index) =>
                index + ":" + component.TypeName + ":"
                    + (Selector.TypeBinding == TypeBindingKind.Generic ? string.Empty : component.ResolvedTypeName)
                    + ":" + component.Access)));
    }
}
