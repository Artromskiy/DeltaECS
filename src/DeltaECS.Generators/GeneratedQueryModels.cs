namespace Delta.ECS.Generators;

/// <summary>Validated semantic shape for a generated fluent query factory.</summary>
internal sealed class QueryModel
{
    internal QueryModel(
        string kind,
        int arity,
        string namespaceName,
        TypeBindingKind typeBinding,
        RegistrationBindingKind registrationBinding,
        bool querySpecReceiver)
    {
        Kind = kind;
        Namespace = namespaceName;
        IsQuerySpecReceiver = querySpecReceiver;
        Api = new ApiModel(
            OperationKind.QueryFactory,
            TargetKind.Query,
            QueryMode.None,
            new SelectorModel(
                typeBinding,
                registrationBinding,
                GeneratorSupport.ComponentModels(arity, AccessKind.RowRead)),
            new ContextModel(ContextModeKind.None, null),
            null,
            new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential),
            kind);
    }

    internal string Kind { get; }
    internal string Namespace { get; }
    internal bool IsQuerySpecReceiver { get; }
    internal ApiModel Api { get; }
    internal string Key => Namespace + "|" + IsQuerySpecReceiver + "|" + Api.SignatureKey;
}
