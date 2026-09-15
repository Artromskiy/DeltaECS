using System;
using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Validated semantic shape for a generated fluent query factory.</summary>
internal sealed class QueryModel
{
    internal QueryModel(string kind, int arity)
    {
        Kind = kind;
        Arity = arity;
        Api = new ApiModel(
            OperationKind.QueryFactory,
            TargetKind.Query,
            QueryMode.None,
            new SelectorModel(
                SelectorKind.Generic,
                GeneratorSupport.ComponentModels(arity, SelectorKind.Generic, AccessKind.RowRead)),
            new ContextModel(ContextModeKind.None, null),
            null,
            new ExecutionModel(ExecutionKind.Dense, ValueKind.Component),
            kind);
    }

    internal string Kind { get; }
    internal int Arity { get; }
    internal ApiModel Api { get; }
    internal string Key => Api.SignatureKey;
}
