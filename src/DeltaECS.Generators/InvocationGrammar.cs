using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Declarative signature facts shared by the generator readers.</summary>
internal readonly record struct ApiDescriptor(
    GeneratedApiKind Family,
    bool HasEntity,
    ValueDomain Value,
    Schedule Schedule,
    InvocationTargetRule Target,
    QueryMode Query,
    bool AllowsIds,
    bool AllowsContext,
    bool RequiresCallback,
    InvocationTailRule Tail,
    int MinimumArity)
{
    internal ApiDescriptor WithTarget(InvocationTargetRule target) => this with { Target = target };

    internal ApiDescriptor WithTail(InvocationTailRule tail) => this with { Tail = tail };

    internal static bool TryGet(string name, out ApiDescriptor descriptor)
    {
        descriptor = name switch
        {
            "ForEach" or "ForEachEntity"
                or "ForEachParallel" or "ForEachEntityParallel"
                or "ForEachStamp" or "ForEachEntityStamp"
                or "ForEachStampParallel" or "ForEachEntityStampParallel" => Iteration(name),
            "WhereAll" or "WhereAny" or "WhereNone" => QueryFactory(),
            "Where" or "WhereEntity" => Where(name),
            "OrderBy" or "ThenBy" => Ordering(),
            "Add" or "Remove" or "Create" or "Destroy" => Structural(name),
            _ => default
        };
        return descriptor.Family != GeneratedApiKind.Unknown;
    }

    private static ApiDescriptor Iteration(string name)
    {
        bool hasEntity = name is "ForEachEntity" or "ForEachEntityParallel"
            or "ForEachEntityStamp" or "ForEachEntityStampParallel";
        bool isStamp = name.Contains("Stamp", StringComparison.Ordinal);
        bool isParallel = name.EndsWith("Parallel", StringComparison.Ordinal);
        return CreateDescriptor(
            GeneratedApiKind.Iteration,
            hasEntity: hasEntity,
            value: isStamp ? ValueDomain.Stamp : ValueDomain.Component,
            schedule: isParallel ? Schedule.Parallel : Schedule.Sequential,
            target: InvocationTargetRule.EntityListOptional,
            query: QueryMode.Optional,
            allowsIds: true,
            allowsContext: true,
            requiresCallback: true,
            tail: isParallel ? InvocationTailRule.WorkerCount : InvocationTailRule.None,
            minimumArity: hasEntity && !isStamp ? 0 : 1);
    }

    private static ApiDescriptor QueryFactory() => CreateDescriptor(GeneratedApiKind.QueryFactory, allowsIds: true);

    private static ApiDescriptor Where(string name) => CreateDescriptor(
            GeneratedApiKind.Where,
            hasEntity: name == "WhereEntity",
            query: QueryMode.Required,
            allowsContext: true,
            requiresCallback: true);

    private static ApiDescriptor Ordering() => CreateDescriptor(
            GeneratedApiKind.Ordering,
            allowsIds: true,
            allowsContext: true,
            requiresCallback: true);

    private static ApiDescriptor Structural(string name) => CreateDescriptor(
            GeneratedApiKind.Structural,
            target: name == "Create" ? InvocationTargetRule.None : InvocationTargetRule.StructuralTarget,
            allowsIds: true,
            tail: name == "Add"
                ? InvocationTailRule.Values
                : name == "Create" ? InvocationTailRule.CountOutput : InvocationTailRule.None);

    private static ApiDescriptor CreateDescriptor(
        GeneratedApiKind family,
        bool hasEntity = false,
        ValueDomain value = ValueDomain.Component,
        Schedule schedule = Schedule.Sequential,
        InvocationTargetRule target = InvocationTargetRule.None,
        QueryMode query = QueryMode.None,
        bool allowsIds = false,
        bool allowsContext = false,
        bool requiresCallback = false,
        InvocationTailRule tail = InvocationTailRule.None,
        int minimumArity = 1)
        => new(
            family,
            hasEntity,
            value,
            schedule,
            target,
            query,
            allowsIds,
            allowsContext,
            requiresCallback,
            tail,
            minimumArity);
}

internal enum InvocationTargetRule
{
    None,
    EntityListOptional,
    StructuralTarget
}

internal enum InvocationTailRule
{
    None,
    Values,
    WorkerCount,
    CountOutput
}

internal static class InvocationGrammar
{
    internal static bool IsWhereInvocation(ExpressionSyntax expression, out InvocationExpressionSyntax? whereInvocation)
    {
        whereInvocation = expression as InvocationExpressionSyntax;
        return whereInvocation?.Expression is MemberAccessExpressionSyntax member
            && member.Name.Identifier.ValueText is "Where" or "WhereEntity";
    }

    internal static bool TryGetWhereReceiver(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax? whereInvocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: InvocationExpressionSyntax candidate
            }
            && candidate.Expression is MemberAccessExpressionSyntax
            {
                Name: IdentifierNameSyntax
                {
                    Identifier.ValueText: "Where" or "WhereEntity"
                }
            })
        {
            whereInvocation = candidate;
            return true;
        }

        whereInvocation = null;
        return false;
    }

    internal static bool TryReadStructuralMutation(
        SemanticModel model,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ApiDescriptor descriptor,
        int? genericArity,
        bool valuesAllowed,
        out InvocationCursorResult result,
        out int arity,
        out bool hasValues,
        out RegistrationBindingKind registrationBinding)
    {
        result = default;
        arity = 0;
        hasValues = false;
        registrationBinding = RegistrationBindingKind.Primary;
        var cursor = new InvocationCursor(model, arguments, descriptor);
        if (!cursor.TryRead(-1, out result))
        {
            return false;
        }

        hasValues = result.TailCount != 0;
        if (hasValues && !valuesAllowed)
        {
            return false;
        }

        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out arity,
                genericArity,
                result.ComponentIdCount == 0 ? null : result.ComponentIdCount,
                hasValues ? result.TailCount : null))
        {
            return false;
        }

        registrationBinding = result.RegistrationBinding;
        return true;
    }

}

/// <summary>Semantic argument cursor in the canonical target/query/ids/context/callback order.</summary>
internal sealed class InvocationCursor(
    SemanticModel model,
    SeparatedSyntaxList<ArgumentSyntax> arguments,
    ApiDescriptor descriptor)
{
    internal bool TryRead(
        int callbackIndex,
        out InvocationCursorResult result,
        bool requireQuery = false,
        bool contextArgumentPresent = false)
    {
        result = default;
        int index = 0;
        TargetKind target = TargetKind.World;
        bool hasTarget = false;
        bool hasQuery = false;
        int queryArgumentIndex = -1;
        int componentIdCount = 0;
        int componentIdSpanIndex = -1;
        int contextIndex = -1;
        ContextModeKind contextMode = ContextModeKind.None;

        if (contextArgumentPresent && (!descriptor.AllowsContext || callbackIndex < 1))
        {
            return false;
        }

        if (descriptor.Target == InvocationTargetRule.StructuralTarget)
        {
            if (!TryTarget(index, out target))
            {
                return false;
            }

            hasTarget = true;
            index++;
        }
        else if (descriptor.Target == InvocationTargetRule.EntityListOptional
            && TryTarget(index, out TargetKind candidateTarget)
            && candidateTarget == TargetKind.EntityList)
        {
            target = candidateTarget;
            hasTarget = true;
            index++;
        }

        if (requireQuery || descriptor.Query == QueryMode.Required
            || (descriptor.Query == QueryMode.Optional && HasQuery(index)))
        {
            if (!HasQuery(index))
            {
                return false;
            }

            hasQuery = true;
            queryArgumentIndex = index;
            index++;
        }

        int componentArgumentLimit = contextArgumentPresent
            ? callbackIndex - 1
            : callbackIndex >= 0 ? callbackIndex : arguments.Count;
        if (descriptor.AllowsIds)
        {
            while (index < componentArgumentLimit && IsComponentId(index))
            {
                componentIdCount++;
                index++;
            }

            if (index < componentArgumentLimit
                && GeneratorSupport.IsComponentIdBatch(model.GetTypeInfo(arguments[index].Expression).Type))
            {
                if (componentIdCount != 0)
                {
                    return false;
                }

                componentIdSpanIndex = index++;
            }
        }

        if (descriptor.AllowsContext && (contextArgumentPresent || callbackIndex > index))
        {
            if (callbackIndex != index + 1 || (!contextArgumentPresent && IsComponentId(index)))
            {
                return false;
            }

            contextIndex = index;
            contextMode = CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(arguments[index]));
            index++;
        }

        int tailStart = -1;
        bool hasOutput = false;
        if (descriptor.RequiresCallback)
        {
            if (callbackIndex != index)
            {
                return false;
            }

            index++;
        }
        else if (callbackIndex >= 0)
        {
            return false;
        }

        int tailCount = arguments.Count - index;
        if (descriptor.Tail == InvocationTailRule.None && tailCount != 0)
        {
            return false;
        }

        if (descriptor.Tail == InvocationTailRule.Values)
        {
            tailStart = index;
        }
        else if (descriptor.Tail == InvocationTailRule.WorkerCount)
        {
            if (tailCount > 1
                || (tailCount == 1 && !GeneratorSupport.IsInt32(model.GetTypeInfo(arguments[index].Expression).Type)))
            {
                return false;
            }

            tailStart = index;
        }
        else if (descriptor.Tail == InvocationTailRule.CountOutput)
        {
            hasOutput = tailCount == 2
                && GeneratorSupport.IsEntityOutput(model.GetTypeInfo(arguments[arguments.Count - 1].Expression).Type);
            int countIndex = arguments.Count - (hasOutput ? 2 : 1);
            if (tailCount is < 1 or > 2
                || !GeneratorSupport.IsInt32(model.GetTypeInfo(arguments[countIndex].Expression).Type)
                || (tailCount == 2 && !hasOutput))
            {
                return false;
            }

            tailStart = countIndex;
        }

        result = new InvocationCursorResult(
            target,
            hasTarget,
            hasQuery,
            queryArgumentIndex,
            componentIdCount,
            componentIdSpanIndex,
            contextIndex,
            contextMode,
            tailStart,
            tailCount,
            hasOutput);
        return true;
    }

    private bool HasQuery(int index) => index < arguments.Count
            && arguments[index].RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            && GeneratorSupport.IsEcsType(model.GetTypeInfo(arguments[index].Expression).Type, "Query");

    private bool IsComponentId(int index) => GeneratorSupport.IsComponentId(model.GetTypeInfo(arguments[index].Expression).Type);

    private bool TryTarget(int index, out TargetKind target)
    {
        target = TargetKind.World;
        if (index >= arguments.Count)
        {
            return false;
        }

        ArgumentSyntax argument = arguments[index];
        ITypeSymbol? type = model.GetTypeInfo(argument.Expression).Type;
        if (GeneratorSupport.IsEntityBatch(type))
        {
            target = TargetKind.EntityList;
        }
        else if (GeneratorSupport.IsEntityType(type))
        {
            target = TargetKind.Entity;
        }
        else if (argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            && GeneratorSupport.IsEcsType(type, "Query"))
        {
            target = TargetKind.Query;
        }
        else
        {
            return false;
        }

        return true;
    }
}

internal readonly record struct InvocationCursorResult(
    TargetKind Target,
    bool HasTarget,
    bool HasQuery,
    int QueryArgumentIndex,
    int ComponentIdCount,
    int ComponentIdSpanIndex,
    int ContextIndex,
    ContextModeKind ContextMode,
    int TailStart,
    int TailCount,
    bool HasOutput)
{
    internal bool HasComponentIdSpan => ComponentIdSpanIndex >= 0;
    internal bool HasComponentIds => ComponentIdCount != 0 || HasComponentIdSpan;
    internal RegistrationBindingKind RegistrationBinding
        => HasComponentIdSpan
            ? RegistrationBindingKind.Dynamic
            : ComponentIdCount == 0 ? RegistrationBindingKind.Primary : RegistrationBindingKind.Explicit;
    internal bool HasContext => ContextIndex >= 0;
}

/// <summary>Reconciles every component-associated arity discovered by a reader.</summary>
internal static class ArityEvidence
{
    internal static bool TryBind(int minimum, out int arity, int? first = null, int? second = null, int? third = null)
    {
        int? canonical = first ?? second ?? third;
        arity = canonical.GetValueOrDefault();
        return canonical.HasValue
            && arity >= minimum
            && Matches(first, arity)
            && Matches(second, arity)
            && Matches(third, arity);
    }

    private static bool Matches(int? evidence, int arity) => !evidence.HasValue || evidence.Value >= 0 && evidence.Value == arity;
}
