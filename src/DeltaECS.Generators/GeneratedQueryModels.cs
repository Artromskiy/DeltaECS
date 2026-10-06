namespace Delta.ECS.Generators;

/// <summary>Validated semantic shape for a generated fluent query factory.</summary>
internal sealed record QueryModel(
    string Kind,
    int Arity,
    string Namespace,
    TypeBindingKind TypeBinding,
    RegistrationBindingKind RegistrationBinding,
    bool IsQuerySpecReceiver)
{
    internal ApiModel Api { get; } = new(
        OperationKind.QueryFactory,
        TargetKind.Query,
        QueryMode.None,
        new SelectorModel(
            TypeBinding,
            RegistrationBinding,
            GeneratorSupport.ComponentModels(Arity, AccessKind.RowRead)),
        new ContextModel(ContextModeKind.None, null),
        null,
        new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential),
        Kind);
    internal string Key => Namespace + "|" + IsQuerySpecReceiver + "|" + Api.SignatureKey;
}
