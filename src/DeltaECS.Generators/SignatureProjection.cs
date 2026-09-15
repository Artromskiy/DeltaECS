using System.Collections.Immutable;
using System.Globalization;

namespace Delta.ECS.Generators;

/// <summary>Projects an API model into the ordered C# slots used by source templates.</summary>
internal sealed class SignatureProjection
{
    private readonly ApiModel _api;

    internal SignatureProjection(ApiModel api)
        => _api = api;

    internal int Arity => _api.Selector.Arity;
    internal bool HasGenericSelectors => _api.Selector.TypeBinding == TypeBindingKind.Generic;
    internal bool HasExplicitIds => _api.Selector.RegistrationBinding == RegistrationBindingKind.Explicit;

    private ImmutableArray<ComponentModel> Components => _api.Selector.Components;

    internal string GenericType(int index, string prefix = "T")
        => prefix + (index + 1).ToString(CultureInfo.InvariantCulture);

    internal string GenericList(string prefix = "T")
        => Join(index => GenericType(index, prefix));

    internal string GenericParameters(string prefix = "T")
        => TypeArguments(GenericList(prefix));

    internal string ComponentIdParameter(int index, string prefix = "component")
        => $"ComponentId {prefix}{index}";

    internal string ComponentIdParameters(string prefix = "component")
        => Join(index => ComponentIdParameter(index, prefix));

    internal string ComponentIdArgument(int index, string prefix = "component")
        => prefix + index;

    internal string ComponentIdArguments(string prefix = "component")
        => Join(index => ComponentIdArgument(index, prefix));

    internal string ComponentParameter(int index, string type, string name)
        => Components[index].ParameterModifier + type + " " + name;

    internal string ComponentParameters(
        IReadOnlyList<string>? types = null,
        string prefix = "component",
        int indexOffset = 0,
        string genericPrefix = "T")
        => Join(index => ComponentParameter(
            index,
            types is null ? GenericType(index, genericPrefix) : types[index],
            prefix + (index + indexOffset)));

    internal string ComponentArgument(int index, string expression)
        => Components[index].InvocationModifier + expression;

    internal string ComponentArguments(string prefix = "component", int indexOffset = 0)
        => Join(index => ComponentArgument(index, prefix + (index + indexOffset)));

    internal string ValueParameters(
        string genericPrefix = "T",
        string valuePrefix = "value",
        IReadOnlyList<string>? types = null)
        => Join(index => "in "
            + (types is null ? GenericType(index, genericPrefix) : types[index])
            + " "
            + valuePrefix
            + index);

    internal string ValueArguments(string valuePrefix = "value")
        => Join(index => "in " + valuePrefix + index);

    internal string ValueNames(string valuePrefix = "value")
        => Join(index => valuePrefix + index);

    internal string AccessParameter(int index, bool tokens, string name)
        => tokens
            ? (Components[index].IsWrite ? "WriteAccess" : "ReadAccess") + " " + name
            : "int " + name;

    internal string AccessParameters(
        bool tokens = false,
        string prefix = "access",
        int indexOffset = 0)
        => Join(index => AccessParameter(index, tokens, prefix + (index + indexOffset)));

    internal string AccessArguments(string prefix = "access", int indexOffset = 0)
        => Join(index => prefix + (index + indexOffset));

    internal static string TypeArguments(IEnumerable<string> types)
        => TypeArguments(string.Join(", ", types));

    internal static string TypeArguments(string types)
        => types.Length == 0 ? string.Empty : "<" + types + ">";

    internal static string TypeWithArguments(string name, string types)
        => name + TypeArguments(types);

    internal static string JoinGeneric(params string[] values)
        => string.Join(", ", values.Where(static value => value.Length != 0));

    internal static string JoinParameters(string prefix, string parameters)
        => parameters.Length == 0 ? prefix : prefix + ", " + parameters;

    internal static string ContextParameter(ContextModeKind mode, string type, string name)
        => mode switch
        {
            ContextModeKind.Ref => "ref " + type + " " + name,
            ContextModeKind.In => "in " + type + " " + name,
            ContextModeKind.RefReadonly => "ref readonly " + type + " " + name,
            _ => type + " " + name
        };

    internal static string ContextArgument(ContextModeKind mode, string name)
        => mode switch
        {
            ContextModeKind.Ref => "ref " + name,
            ContextModeKind.In or ContextModeKind.RefReadonly => "in " + name,
            _ => name
        };

    private string Join(Func<int, string> render)
        => string.Join(", ", Enumerable.Range(0, Arity).Select(render));
}
