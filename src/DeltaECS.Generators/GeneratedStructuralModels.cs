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
        bool throwOnTypeMismatch = false,
        bool hasOutput = false,
        bool createsOne = false,
        string namespaceName = "")
    {
        Operation = operation;
        Namespace = namespaceName;
        HasValues = hasValues;
        ThrowOnTypeMismatch = throwOnTypeMismatch;
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
        CreatesOne = createsOne;
    }

    internal StructuralOperation Operation { get; }
    internal string Namespace { get; }
    internal bool HasValues { get; }
    internal bool ThrowOnTypeMismatch { get; }
    internal bool HasOutput { get; }
    internal bool CreatesOne { get; }
    internal ApiModel Api { get; }
    internal string Key => Namespace + "|" + Api.SignatureKey + "|" + CreatesOne;
}
