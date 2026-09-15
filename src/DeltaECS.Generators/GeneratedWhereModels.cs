using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Delta.ECS.Generators;

/// <summary>Semantic shape shared by all generated Where calls with the same predicate signature.</summary>
internal sealed class PredicateModel
{
    internal PredicateModel(
        string pattern,
        bool isFunctor,
        string? functorType,
        bool hasEntity,
        bool hasContext,
        string? contextType,
        string[]? components)
    {
        Pattern = pattern;
        IsFunctor = isFunctor;
        FunctorType = functorType;
        HasEntity = hasEntity;
        HasContext = hasContext;
        ContextType = contextType;
        Components = components ?? Array.Empty<string>();
        ComponentModels = GeneratorSupport.ComponentModels(Pattern, Components, isFunctor, "T");

        Api = new ApiModel(
            OperationKind.Where,
            TargetKind.World,
            QueryMode.Required,
            new SelectorModel(isFunctor ? SelectorKind.Inferred : SelectorKind.Generic, ComponentModels),
            new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
            new CallbackModel(
                isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
                hasEntity,
                functorType),
            new ExecutionModel(ExecutionKind.Dense, ValueKind.Component),
            pattern: Pattern);
    }

    internal string Pattern { get; }
    internal int Arity => Pattern.Length;
    internal bool IsFunctor { get; }
    internal string? FunctorType { get; }
    internal bool HasEntity { get; }
    internal bool HasContext { get; }
    internal string? ContextType { get; }
    internal string[] Components { get; }
    internal ImmutableArray<ComponentModel> ComponentModels { get; }
    internal ApiModel Api { get; }
    internal ShapeRegistry<TerminalModel> Terminals { get; } = new(static terminal => terminal.SignatureKey);
    internal List<string[]> StaticMethodGroupComponents { get; } = new();
    internal string Key => Api.SignatureKey;

    internal void Merge(PredicateModel candidate)
    {
        if (!IsFunctor && candidate.Components.Length > 0)
        {
            AddUnique(StaticMethodGroupComponents, candidate.Components);
        }
    }

    internal void RegisterStaticMethodGroup()
        => AddUnique(StaticMethodGroupComponents, Components);

    private static void AddUnique(List<string[]> values, string[] candidate)
    {
        if (!values.Any(existing => existing.SequenceEqual(candidate, StringComparer.Ordinal)))
        {
            values.Add(candidate);
        }
    }
}

/// <summary>Semantic description of a Where terminal operation.</summary>
internal sealed class TerminalModel
{
    internal TerminalModel(
        TerminalKind kind,
        string pattern,
        int arity,
        bool hasEntity,
        bool isFunctor = false,
        string? functorType = null,
        bool hasContext = false,
        string? contextType = null,
        string[]? components = null,
        string? methodGroupTarget = null)
    {
        Kind = kind;
        Pattern = pattern;
        Arity = arity;
        HasEntity = hasEntity;
        IsFunctor = isFunctor;
        FunctorType = functorType;
        HasContext = hasContext;
        ContextType = contextType;
        Components = components ?? Array.Empty<string>();
        MethodGroupTarget = methodGroupTarget;
        ComponentModels = GeneratorSupport.ComponentModels(Pattern, Components, isFunctor, "U");

        Api = new ApiModel(
            OperationKind.Where,
            TargetKind.World,
            QueryMode.Required,
            new SelectorModel(isFunctor ? SelectorKind.Inferred : SelectorKind.Generic, ComponentModels),
            new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
            new CallbackModel(
                isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
                hasEntity,
                functorType),
            new ExecutionModel(ExecutionKind.Dense, ValueKind.Component),
            Kind + "|" + arity.ToString(CultureInfo.InvariantCulture),
            Pattern);
    }

    internal TerminalKind Kind { get; }
    internal string Pattern { get; }
    internal int Arity { get; }
    internal bool HasEntity { get; }
    internal bool IsFunctor { get; }
    internal string? FunctorType { get; }
    internal bool HasContext { get; }
    internal string? ContextType { get; }
    internal string[] Components { get; }
    internal ImmutableArray<ComponentModel> ComponentModels { get; }
    internal string? MethodGroupTarget { get; }
    internal ApiModel Api { get; }
    internal List<string[]> StaticMethodGroupComponents { get; } = new();
    internal bool IsCallback => Kind is TerminalKind.ForEach or TerminalKind.ForEachEntity;
    internal string SignatureKey => Api.SignatureKey;
    internal string Key => SignatureKey + "|" + string.Join(";", Components);

    internal void Merge(TerminalModel candidate)
    {
        if (!IsFunctor && candidate.MethodGroupTarget is not null)
        {
            AddUnique(StaticMethodGroupComponents, candidate.Components);
        }
    }

    internal void RegisterStaticMethodGroup()
        => AddUnique(StaticMethodGroupComponents, Components);

    private static void AddUnique(List<string[]> values, string[] candidate)
    {
        if (!values.Any(existing => existing.SequenceEqual(candidate, StringComparer.Ordinal)))
        {
            values.Add(candidate);
        }
    }
}

/// <summary>Materialized source data consumed by interception templates.</summary>
internal sealed class WhereInterceptionSite
{
    internal WhereInterceptionSite(
        string id,
        PredicateModel shape,
        TerminalModel terminal,
        string? predicateMethodGroupTarget,
        string? actionMethodGroupTarget,
        string[] predicateParameterNames,
        string[] actionParameterNames,
        string? predicateBody,
        string? actionBody,
        bool predicateBodyIsBlock,
        bool actionBodyIsBlock,
        string[] predicateComponents,
        string[] actionComponents,
        string attribute,
        string[] usings)
    {
        Id = id;
        Shape = shape;
        Terminal = terminal;
        PredicateMethodGroupTarget = predicateMethodGroupTarget;
        ActionMethodGroupTarget = actionMethodGroupTarget;
        PredicateParameterNames = predicateParameterNames;
        ActionParameterNames = actionParameterNames;
        PredicateBody = predicateBody;
        ActionBody = actionBody;
        PredicateBodyIsBlock = predicateBodyIsBlock;
        ActionBodyIsBlock = actionBodyIsBlock;
        PredicateComponents = predicateComponents;
        ActionComponents = actionComponents;
        Attribute = attribute;
        Usings = usings;
    }

    internal string Id { get; }
    internal PredicateModel Shape { get; }
    internal TerminalModel Terminal { get; }
    internal string[] PredicateParameterNames { get; }
    internal string[] ActionParameterNames { get; }
    internal string? PredicateBody { get; }
    internal string? ActionBody { get; }
    internal bool PredicateBodyIsBlock { get; }
    internal bool ActionBodyIsBlock { get; }
    internal string? PredicateMethodGroupTarget { get; }
    internal string? ActionMethodGroupTarget { get; }
    internal string[] PredicateComponents { get; }
    internal string[] ActionComponents { get; }
    internal string Attribute { get; }
    internal string[] Usings { get; }
}

internal enum TerminalKind
{
    Destroy,
    Add,
    Remove,
    ForEach,
    ForEachEntity
}

internal static class GeneratedWhereModelNames
{
    internal static string[] Predicate(PredicateModel shape)
        => Names(shape.HasContext ? 1 : 0, shape.HasEntity ? 1 : 0, shape.Arity);

    internal static string[] Action(TerminalModel terminal)
        => Names(0, terminal.HasEntity ? 1 : 0, terminal.Arity);

    private static string[] Names(int contextCount, int entityCount, int componentCount)
    {
        var names = new List<string>(contextCount + entityCount + componentCount);
        if (contextCount != 0)
        {
            names.Add("context");
        }

        if (entityCount != 0)
        {
            names.Add("entity");
        }

        names.AddRange(Enumerable.Range(0, componentCount).Select(static index => "component" + index));
        return names.ToArray();
    }
}
