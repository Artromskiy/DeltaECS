namespace Delta.ECS.Generators;

/// <summary>Validated semantic shape for a generated fluent query factory.</summary>
internal sealed class QueryModel
{
    internal QueryModel(string kind, int arity, string namespaceName)
    {
        Kind = kind;
        Namespace = namespaceName;
        Api = new ApiModel(
            OperationKind.QueryFactory,
            TargetKind.Query,
            QueryMode.None,
            new SelectorModel(
                TypeBindingKind.Generic,
                RegistrationBindingKind.Primary,
                GeneratorSupport.ComponentModels(arity, AccessKind.RowRead)),
            new ContextModel(ContextModeKind.None, null),
            null,
            new ExecutionModel(Scope.QueryWide, ValueDomain.Component, Schedule.Sequential),
            kind);
    }

    internal string Kind { get; }
    internal string Namespace { get; }
    internal ApiModel Api { get; }
    internal string Key => Namespace + "|" + Api.SignatureKey;
}
