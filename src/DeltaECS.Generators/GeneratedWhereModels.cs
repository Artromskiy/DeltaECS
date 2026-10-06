using System.Collections.Immutable;
using System.Globalization;

namespace Delta.ECS.Generators;

/// <summary>Semantic shape shared by all generated Where calls with the same predicate signature.</summary>
internal sealed class PredicateModel(
    string pattern,
    bool isFunctor,
    string? functorType,
    bool hasEntity,
    bool hasContext,
    string? contextType,
    string[]? components,
    string namespaceName = "")
{
    internal string Pattern { get; } = pattern;
    internal string Namespace { get; } = namespaceName;
    internal int Arity => Pattern.Length;
    internal bool IsFunctor { get; } = isFunctor;
    internal string? FunctorType { get; } = functorType;
    internal bool HasEntity { get; } = hasEntity;
    internal bool HasContext { get; } = hasContext;
    internal string? ContextType { get; } = contextType;
    internal string[] Components { get; } = components ?? Array.Empty<string>();
    internal ImmutableArray<ComponentModel> ComponentModels => Api.Selector.Components;
    internal ApiModel Api { get; } = new(
        OperationKind.Where,
        TargetKind.World,
        QueryMode.Required,
        new SelectorModel(
            TypeBindingKind.CallbackInferred,
            RegistrationBindingKind.Primary,
            GeneratorSupport.ComponentModels(pattern, components ?? Array.Empty<string>(), isFunctor, "T")),
        new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
        new CallbackModel(
            isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
            hasEntity,
            functorType),
        new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential));
    internal ShapeRegistry<TerminalModel> Terminals { get; } = new(static terminal => terminal.SignatureKey);
    internal List<WherePredicateBinding> StaticMethodGroupBindings { get; } = new();
    internal bool HasOrderedQuerySource { get; set; }
    internal string[] ClosedTypeArguments { get; set; } = Array.Empty<string>();
    internal bool HasOpenGenericArguments { get; set; }
    internal string Key => Namespace + "|" + Api.SignatureKey;

    internal WherePredicateBinding ConcreteBinding
        => new(ContextType, Components);

    internal void Merge(PredicateModel candidate)
    {
        if (!IsFunctor && candidate.Components.Length > 0)
        {
            AddUnique(StaticMethodGroupBindings, candidate.ConcreteBinding);
        }
    }

    internal void RegisterStaticMethodGroup()
        => AddUnique(StaticMethodGroupBindings, ConcreteBinding);

    private static void AddUnique(List<WherePredicateBinding> values, WherePredicateBinding candidate)
    {
        if (!values.Any(existing => existing.Equals(candidate)))
        {
            values.Add(candidate);
        }
    }
}

/// <summary>Concrete callback types retained for one discovered Where call site.</summary>
internal sealed class WherePredicateBinding(string? contextType, string[] components)
{
    internal string? ContextType { get; } = contextType;
    internal string[] Components { get; } = components;
    internal string SortKey => (ContextType ?? string.Empty) + "|" + string.Join("|", Components);

    internal bool Equals(WherePredicateBinding other)
        => string.Equals(ContextType, other.ContextType, StringComparison.Ordinal)
            && Components.SequenceEqual(other.Components, StringComparer.Ordinal);
}

/// <summary>Semantic description of a Where terminal operation.</summary>
internal sealed class TerminalModel(
    TerminalKind kind,
    string pattern,
    bool hasEntity,
    bool isFunctor = false,
    string? functorType = null,
    bool hasContext = false,
    string? contextType = null,
    string[]? components = null,
    string? methodGroupTarget = null,
    bool hasValues = false,
    TypeBindingKind typeBinding = TypeBindingKind.CallbackInferred,
    RegistrationBindingKind registrationBinding = RegistrationBindingKind.Primary,
    ContextModeKind functorPassMode = ContextModeKind.Ref,
    bool isGeneratedEntityConsumer = false)
{
    internal TerminalKind Kind { get; } = kind;
    internal string Pattern { get; } = pattern;
    internal int Arity => Api.Selector.Arity;
    internal bool HasEntity { get; } = hasEntity;
    internal bool IsFunctor { get; } = isFunctor;
    internal string? FunctorType { get; } = functorType;
    internal ContextModeKind FunctorPassMode { get; } = isFunctor ? functorPassMode : ContextModeKind.None;
    internal bool IsGeneratedEntityConsumer { get; } = isGeneratedEntityConsumer;
    internal bool HasContext { get; } = hasContext;
    internal string? ContextType { get; } = contextType;
    internal string[] Components { get; } = components ?? Array.Empty<string>();
    internal bool HasValues { get; } = hasValues;
    internal ImmutableArray<ComponentModel> ComponentModels => Api.Selector.Components;
    internal string? MethodGroupTarget { get; } = methodGroupTarget;
    internal ApiModel Api { get; } = new(
        OperationKind.Where,
        TargetKind.World,
        QueryMode.Required,
        new SelectorModel(
            typeBinding,
            registrationBinding,
            GeneratorSupport.ComponentModels(
                pattern.Length == 0 ? new string('V', components?.Length ?? 0) : pattern,
                components ?? Array.Empty<string>(),
                isFunctor,
                "U")),
        new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
        new CallbackModel(
            isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
            hasEntity,
            functorType,
            isFunctor ? functorPassMode : ContextModeKind.None),
        new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential),
        kind + "|" + (pattern.Length == 0 ? components?.Length ?? 0 : pattern.Length).ToString(CultureInfo.InvariantCulture)
            + (hasValues ? "|values|" + registrationBinding : string.Empty),
        pattern);
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
internal sealed record WhereInterceptionSite(
    string Id,
    PredicateModel Shape,
    TerminalModel Terminal,
    WherePredicateBinding PredicateShapeBinding,
    string? PredicateMethodGroupTarget,
    string? ActionMethodGroupTarget,
    string[] PredicateParameterNames,
    string[] ActionParameterNames,
    string? PredicateBody,
    string? ActionBody,
    bool PredicateBodyIsBlock,
    bool ActionBodyIsBlock,
    string[] PredicateComponents,
    string[] ActionComponents,
    string Attribute,
    string[] Usings)
{
    internal CallSiteBinding PredicateBinding { get; } = new(
        PredicateParameterNames,
        PredicateBody,
        PredicateBodyIsBlock,
        canInline: PredicateBody is not null && !PredicateBodyIsBlock,
        PredicateMethodGroupTarget);
    internal CallSiteBinding ActionBinding { get; } = new(
        ActionParameterNames,
        ActionBody,
        ActionBodyIsBlock,
        canInline: ActionBody is not null && !ActionBodyIsBlock,
        ActionMethodGroupTarget);
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
