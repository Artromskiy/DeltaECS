using System;
using System.Collections.Immutable;

namespace Delta.ECS.Generators;

internal enum Receiver
{
    None,
    World,
}

internal enum StructuralMode
{
    Entities,
    SingleEntity,
    Query,
    CreateSingle,
    Create,
    CreateOutput,
    ExplicitCreate,
    ExplicitCreateOutput
}

internal sealed class StructuralModel
{
    internal StructuralModel(
        Receiver receiver,
        StructuralMode mode,
        bool isAdd,
        int arity,
        bool isGeneric = true,
        bool isExplicitIds = false,
        bool hasValues = false)
    {
        Receiver = receiver;
        Mode = mode;
        Arity = arity;
        IsGeneric = isGeneric;
        IsExplicitIds = isExplicitIds;
        HasValues = hasValues;
        SelectorKind selector = isExplicitIds ? SelectorKind.ComponentIds : isGeneric ? SelectorKind.Generic : SelectorKind.ComponentIds;
        var slots = GeneratorSupport.ComponentModels(arity, selector, AccessKind.Value);

        StructuralOperation operation = mode == StructuralMode.SingleEntity && hasValues
            ? (isAdd ? StructuralOperation.Add : StructuralOperation.Set)
            : mode is StructuralMode.CreateSingle
                or StructuralMode.Create
                or StructuralMode.CreateOutput
                or StructuralMode.ExplicitCreate
                or StructuralMode.ExplicitCreateOutput
                ? StructuralOperation.Create
                : isAdd ? StructuralOperation.Add : StructuralOperation.Remove;
        TargetKind target = mode switch
        {
            StructuralMode.Entities => TargetKind.EntityList,
            StructuralMode.SingleEntity => TargetKind.Entity,
            StructuralMode.Query => TargetKind.Query,
            _ => TargetKind.World
        };
        Api = new ApiModel(
            OperationKind.Structural,
            target,
            mode == StructuralMode.Query ? QueryMode.Required : QueryMode.None,
            new SelectorModel(selector, slots),
            new ContextModel(ContextModeKind.None, null),
            null,
            new ExecutionModel(ExecutionKind.Dense, ValueKind.Component),
            operation + "|" + mode + "|" + isAdd + "|" + hasValues);
        Plan = new StructuralPlan(operation);
    }

    internal Receiver Receiver { get; }
    internal StructuralMode Mode { get; }
    internal int Arity { get; }
    internal bool IsGeneric { get; }
    internal bool IsExplicitIds { get; }
    internal bool HasValues { get; }
    internal ApiModel Api { get; }
    internal StructuralPlan Plan { get; }
    internal string Key => Api.SignatureKey;
}
