namespace Delta.ECS.Generators;

internal sealed record ComponentComparerDelegateModel(
    string[] ComponentTypes,
    ContextModeKind[] ComponentModes,
    bool HasEntity,
    bool HasContext,
    string? ContextType,
    ContextModeKind ContextMode,
    bool ContextIsGeneric)
{
    internal string Key => $"{ComponentTypes.Length}|{string.Join("", ComponentModes.Select(ModeCode))}"
        + $"|{HasEntity}|{HasContext}|{ContextType ?? string.Empty}|{ContextModeName(ContextMode)}";
    internal string Hash => GeneratorSupport.StableName(Key);

    private static string ContextModeName(ContextModeKind mode) => mode switch
    {
        ContextModeKind.In => "In",
        ContextModeKind.RefReadonly => "RefReadonly",
        ContextModeKind.Value => "Value",
        ContextModeKind.Ref => "Ref",
        _ => "None"
    };

    private static char ModeCode(ContextModeKind mode) => mode switch
    {
        ContextModeKind.In => 'I',
        ContextModeKind.RefReadonly => 'R',
        ContextModeKind.Value => 'V',
        ContextModeKind.Ref => 'W',
        _ => 'I'
    };
}

internal sealed record ComponentComparerDelegateSite(
    ComponentComparerDelegateModel Model,
    string MethodName,
    RegistrationBindingKind RegistrationBinding,
    string[] ParameterNames,
    string[] ComponentTypes,
    string Id,
    string CallbackBody,
    bool IsStatic,
    string NamespaceName,
    string[] Usings,
    string? InterceptionLocation = null,
    string? InterceptionAttribute = null,
    PredicateModel? WhereSource = null);
