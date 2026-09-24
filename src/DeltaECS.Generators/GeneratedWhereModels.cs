using System.Collections.Immutable;
using System.Globalization;

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
        string[]? components,
        string namespaceName = "")
    {
        Namespace = namespaceName;
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
            new SelectorModel(TypeBindingKind.CallbackInferred, RegistrationBindingKind.Primary, ComponentModels),
            new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
            new CallbackModel(
                isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
                hasEntity,
                functorType),
            new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential));
    }

    internal string Pattern { get; }
    internal string Namespace { get; }
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
    internal List<WherePredicateBinding> StaticMethodGroupBindings { get; } = new();
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
internal sealed class WherePredicateBinding
{
    internal WherePredicateBinding(string? contextType, string[] components)
    {
        ContextType = contextType;
        Components = components;
    }

    internal string? ContextType { get; }
    internal string[] Components { get; }
    internal string SortKey => (ContextType ?? string.Empty) + "|" + string.Join("|", Components);

    internal bool Equals(WherePredicateBinding other)
        => string.Equals(ContextType, other.ContextType, StringComparison.Ordinal)
            && Components.SequenceEqual(other.Components, StringComparer.Ordinal);
}

/// <summary>Semantic description of a Where terminal operation.</summary>
internal sealed class TerminalModel
{
    internal TerminalModel(
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
        ContextModeKind functorPassMode = ContextModeKind.Ref)
    {
        Kind = kind;
        Pattern = pattern;
        HasEntity = hasEntity;
        IsFunctor = isFunctor;
        FunctorType = functorType;
        FunctorPassMode = isFunctor ? functorPassMode : ContextModeKind.None;
        HasContext = hasContext;
        ContextType = contextType;
        Components = components ?? Array.Empty<string>();
        MethodGroupTarget = methodGroupTarget;
        HasValues = hasValues;
        int componentCount = pattern.Length == 0 ? Components.Length : pattern.Length;
        string componentPattern = pattern.Length == 0
            ? new string('V', componentCount)
            : pattern;
        ComponentModels = GeneratorSupport.ComponentModels(componentPattern, Components, isFunctor, "U");

        Api = new ApiModel(
            OperationKind.Where,
            TargetKind.World,
            QueryMode.Required,
            new SelectorModel(typeBinding, registrationBinding, ComponentModels),
            new ContextModel(hasContext ? ContextModeKind.Value : ContextModeKind.None, contextType),
            new CallbackModel(
                isFunctor ? CallbackSource.Functor : CallbackSource.Lambda,
                hasEntity,
                functorType,
                FunctorPassMode),
            new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential),
            Kind + "|" + componentCount.ToString(CultureInfo.InvariantCulture)
                + (hasValues ? "|values|" + registrationBinding : string.Empty),
            Pattern);
    }

    internal TerminalKind Kind { get; }
    internal string Pattern { get; }
    internal int Arity => Api.Selector.Arity;
    internal bool HasEntity { get; }
    internal bool IsFunctor { get; }
    internal string? FunctorType { get; }
    internal ContextModeKind FunctorPassMode { get; }
    internal bool HasContext { get; }
    internal string? ContextType { get; }
    internal string[] Components { get; }
    internal bool HasValues { get; }
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
        WherePredicateBinding predicateShapeBinding,
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
        PredicateShapeBinding = predicateShapeBinding;
        PredicateComponents = predicateComponents;
        ActionComponents = actionComponents;
        Attribute = attribute;
        Usings = usings;
        PredicateBinding = new CallSiteBinding(
            predicateParameterNames,
            predicateBody,
            predicateBodyIsBlock,
            canInline: predicateBody is not null && !predicateBodyIsBlock,
            predicateMethodGroupTarget);
        ActionBinding = new CallSiteBinding(
            actionParameterNames,
            actionBody,
            actionBodyIsBlock,
            canInline: actionBody is not null && !actionBodyIsBlock,
            actionMethodGroupTarget);
    }

    internal string Id { get; }
    internal PredicateModel Shape { get; }
    internal TerminalModel Terminal { get; }
    internal WherePredicateBinding PredicateShapeBinding { get; }
    internal string[] PredicateComponents { get; }
    internal string[] ActionComponents { get; }
    internal string Attribute { get; }
    internal string[] Usings { get; }
    internal CallSiteBinding PredicateBinding { get; }
    internal CallSiteBinding ActionBinding { get; }
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
