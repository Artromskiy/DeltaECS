using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Delta.ECS.Generators;

/// <summary>Projects an API model into the ordered C# slots used by source templates.</summary>
internal sealed class SignatureProjection(SelectorModel selector)
{
    internal int Arity => selector.Arity;
    internal bool HasGenericSelectors => selector.TypeBinding == TypeBindingKind.Generic;
    internal bool HasExplicitIds => selector.RegistrationBinding != RegistrationBindingKind.Primary;
    internal bool HasDynamicIds => selector.RegistrationBinding == RegistrationBindingKind.Dynamic;

    private ImmutableArray<ComponentModel> Components => selector.Components;

    internal string GenericType(int index, string prefix = "T") => prefix + (index + 1).ToString(CultureInfo.InvariantCulture);

    internal string GenericList(string prefix = "T") => Join(index => GenericType(index, prefix));

    internal string GenericParameters(string prefix = "T") => TypeArguments(GenericList(prefix));

    internal string ComponentIdParameter(int index, string prefix = "component") => $"ComponentId {ComponentIdName(index, prefix)}";

    internal string ComponentIdParameters(string prefix = "component") => HasDynamicIds
            ? "global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds"
            : Join(index => ComponentIdParameter(index, prefix));

    internal string ComponentIdStorageParameters(string prefix = "componentId") => HasDynamicIds
            ? "global::Delta.ECS.ComponentId[] componentIds"
            : HasExplicitIds ? ComponentIdParameters(prefix) : string.Empty;

    internal string ComponentIdFields() => ComponentIdState(
            "private readonly global::Delta.ECS.ComponentId[] _componentIds;",
            static index => $"private readonly global::Delta.ECS.ComponentId _componentId{index};");

    internal string ComponentIdAssignments() => ComponentIdState(
            "_componentIds = componentIds;",
            index => $"_componentId{index} = {ComponentIdArgument(index, "componentId")};");

    internal string ComponentIdLocals(string dynamicName = "componentIds", string prefix = "componentId")
        => ComponentIdState(
            $"global::Delta.ECS.ComponentId[] {dynamicName} = _componentIds;",
            index => $"global::Delta.ECS.ComponentId {ComponentIdArgument(index, prefix)} = _componentId{index};");

    private string ComponentIdState(string dynamicValue, Func<int, string> explicitValue) => !HasExplicitIds
            ? string.Empty
            : HasDynamicIds ? dynamicValue : GeneratorTemplates.JoinIndexed(Arity, explicitValue, "\n");

    internal string ComponentIdArgument(int index, string prefix = "component") => HasDynamicIds ? $"componentIds[{index}]" : ComponentIdName(index, prefix);

    internal string ComponentIdArguments(string prefix = "component") => Join(index => ComponentIdArgument(index, prefix));

    private string ComponentIdName(int index, string prefix) => Arity == 1
            ? (prefix.EndsWith("Id", StringComparison.Ordinal) ? prefix : prefix + "Id")
            : prefix + index;

    internal string ComponentIdListArgument(string prefix = "component") => HasDynamicIds ? "componentIds" : ComponentIdArguments(prefix);

    internal string ComponentParameter(int index, string type, string name) => Components[index].ParameterModifier + type + " " + name;

    internal string ComponentParameters(
        IReadOnlyList<string>? types = null,
        string prefix = "component",
        int indexOffset = 0,
        string genericPrefix = "T")
        => Join(index => ComponentParameter(
            index,
            types is null ? GenericType(index, genericPrefix) : types[index],
            prefix + (index + indexOffset)));

    internal string ComponentArgument(int index, string expression) => Components[index].InvocationModifier + expression;

    internal string ComponentArguments(string prefix = "component", int indexOffset = 0) => Join(index => ComponentArgument(index, prefix + (index + indexOffset)));

    internal string ValueParameters(
        string genericPrefix = "T",
        string valuePrefix = "value",
        IReadOnlyList<string>? types = null)
        => Join(index => "in "
            + (types is null ? GenericType(index, genericPrefix) : types[index])
            + " "
            + valuePrefix
            + index);

    internal string ValueArguments(string valuePrefix = "value") => Join(index => "in " + valuePrefix + index);

    internal string ValueNames(string valuePrefix = "value") => Join(index => valuePrefix + index);

    internal string AccessParameter(int index, bool tokens, string name) => tokens
            ? (Components[index].IsWrite ? "WriteAccess" : "ReadAccess") + " " + name
            : "int " + name;

    internal string AccessParameters(bool tokens = false, string prefix = "access", int indexOffset = 0)
        => Join(index => AccessParameter(index, tokens, prefix + (index + indexOffset)));

    internal string AccessArguments(string prefix = "access", int indexOffset = 0) => Join(index => prefix + (index + indexOffset));

    internal static string TypeArguments(IEnumerable<string> types) => TypeArguments(string.Join(", ", types));

    internal static string TypeArguments(string types) => types.Length == 0 ? string.Empty : "<" + types + ">";

    internal static string TypeWithArguments(string name, string types) => name + TypeArguments(types);

    internal static string JoinGeneric(params string[] values) => string.Join(", ", values.Where(static value => value.Length != 0));

    internal static string JoinParameters(string prefix, string parameters) => parameters.Length == 0 ? prefix : prefix + ", " + parameters;

    internal static string ContextParameter(ContextModeKind mode, string type, string name) => mode switch
    {
        ContextModeKind.Ref => "ref " + type + " " + name,
        ContextModeKind.In => "in " + type + " " + name,
        ContextModeKind.RefReadonly => "ref readonly " + type + " " + name,
        _ => type + " " + name
    };

    internal static string ContextLocal(ContextModeKind mode, string type, string name, string source) => mode switch
    {
        ContextModeKind.Ref => $"ref {type} {name} = ref {source};",
        ContextModeKind.Value => $"{type} {name} = {source};",
        _ => $"ref readonly {type} {name} = ref {source};"
    };

    internal static string ContextArgument(ContextModeKind mode, string name) => mode switch
    {
        ContextModeKind.Ref => "ref " + name,
        ContextModeKind.In or ContextModeKind.RefReadonly => "in " + name,
        _ => name
    };

    internal static string ComponentParameter(ContextModeKind mode, string type, string name) => (mode switch
    {
        ContextModeKind.RefReadonly => "ref readonly ",
        ContextModeKind.Value => string.Empty,
        ContextModeKind.Ref => "ref ",
        _ => "in "
    }) + type + " " + name;

    internal static string ComponentArgument(ContextModeKind mode, string expression) => mode is ContextModeKind.In or ContextModeKind.RefReadonly ? "in " + expression : expression;

    private string Join(Func<int, string> render) => string.Join(", ", Enumerable.Range(0, Arity).Select(render));
}
