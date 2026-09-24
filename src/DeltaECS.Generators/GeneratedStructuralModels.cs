namespace Delta.ECS.Generators;

internal sealed class StructuralModel
{
    internal StructuralModel(
        StructuralOperation operation,
        TargetKind target,
        int arity,
        TypeBindingKind typeBinding = TypeBindingKind.Generic,
        RegistrationBindingKind registrationBinding = RegistrationBindingKind.Primary,
        bool hasValues = false,
        bool hasOutput = false,
        string namespaceName = "")
    {
        Operation = operation;
        Namespace = namespaceName;
        HasValues = hasValues;
        HasOutput = hasOutput;
        var slots = GeneratorSupport.ComponentModels(arity, AccessKind.Value);

        Api = new ApiModel(
            OperationKind.Structural,
            target,
            target == TargetKind.Query ? QueryMode.Required : QueryMode.None,
            new SelectorModel(typeBinding, registrationBinding, slots),
            new ContextModel(ContextModeKind.None, null),
            null,
            new ExecutionModel(
                target == TargetKind.EntityList ? Scope.EntityList : Scope.QueryWide,
                ValueDomain.Component,
                Schedule.Sequential),
            operation + "|" + hasValues + "|" + hasOutput,
            summary: $"Executes the generated {operation} operation.");
    }

    internal StructuralOperation Operation { get; }
    internal string Namespace { get; }
    internal bool HasValues { get; }
    internal bool HasOutput { get; }
    internal ApiModel Api { get; }
    internal string Key => Namespace + "|" + Api.SignatureKey;
}
