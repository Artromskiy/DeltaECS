namespace Delta.ECS.Generators;

internal sealed record StructuralModel(
    StructuralOperation Operation,
    TargetKind Target,
    int Arity,
    TypeBindingKind TypeBinding = TypeBindingKind.Generic,
    RegistrationBindingKind RegistrationBinding = RegistrationBindingKind.Primary,
    bool HasValues = false,
    bool ThrowOnTypeMismatch = false,
    bool HasOutput = false,
    bool CreatesOne = false,
    string Namespace = "")
{
    internal ApiModel Api { get; } = new(
        OperationKind.Structural,
        Target,
        Target == TargetKind.Query ? QueryMode.Required : QueryMode.None,
        new SelectorModel(
            TypeBinding,
            RegistrationBinding,
            GeneratorSupport.ComponentModels(Arity, AccessKind.Value)),
        new ContextModel(ContextModeKind.None, null),
        null,
        new ExecutionModel(
            Target == TargetKind.EntityList ? Scope.EntityList : Scope.QueryWide,
            ValueDomain.Component,
            Schedule.Sequential),
        Operation + "|" + HasValues + "|" + HasOutput,
        SummaryText: $"Executes the generated {Operation} operation.");
    internal string Key => Namespace + "|" + Api.SignatureKey + "|" + CreatesOne;
}
