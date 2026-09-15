using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Declarative signature facts shared by the generator readers.</summary>
internal readonly struct ApiDescriptor
{
    internal ApiDescriptor(
        GeneratedApiKind family,
        bool hasEntity,
        ValueDomain value,
        Schedule schedule,
        InvocationTargetRule target,
        QueryMode query,
        bool allowsIds,
        bool allowsContext,
        bool requiresCallback,
        InvocationTailRule tail,
        int minimumArity)
    {
        Family = family;
        HasEntity = hasEntity;
        Value = value;
        Schedule = schedule;
        Target = target;
        Query = query;
        AllowsIds = allowsIds;
        AllowsContext = allowsContext;
        RequiresCallback = requiresCallback;
        Tail = tail;
        MinimumArity = minimumArity;
    }

    internal GeneratedApiKind Family { get; }
    internal bool HasEntity { get; }
    internal ValueDomain Value { get; }
    internal Schedule Schedule { get; }
    internal InvocationTargetRule Target { get; }
    internal QueryMode Query { get; }
    internal bool AllowsIds { get; }
    internal bool AllowsContext { get; }
    internal bool RequiresCallback { get; }
    internal InvocationTailRule Tail { get; }
    internal int MinimumArity { get; }

    internal ApiDescriptor WithTarget(InvocationTargetRule target)
        => new(
            Family,
            HasEntity,
            Value,
            Schedule,
            target,
            Query,
            AllowsIds,
            AllowsContext,
            RequiresCallback,
            Tail,
            MinimumArity);

    internal static bool TryGet(string name, out ApiDescriptor descriptor)
    {
        bool entity = name is "ForEachEntity" or "ForEachEntityParallel"
            or "ForEachEntityStamp" or "ForEachEntityStampParallel"
            or "WhereEntity";
        bool parallel = name is "ForEachParallel" or "ForEachEntityParallel"
            or "ForEachStampParallel" or "ForEachEntityStampParallel";
        bool stamp = name is "ForEachStamp" or "ForEachEntityStamp"
            or "ForEachStampParallel" or "ForEachEntityStampParallel";

        if (name is "ForEach" or "ForEachEntity"
            or "ForEachParallel" or "ForEachEntityParallel"
            or "ForEachStamp" or "ForEachEntityStamp"
            or "ForEachStampParallel" or "ForEachEntityStampParallel")
        {
            descriptor = new ApiDescriptor(
                GeneratedApiKind.Iteration,
                entity,
                stamp ? ValueDomain.Stamp : ValueDomain.Component,
                parallel ? Schedule.Parallel : Schedule.Sequential,
                InvocationTargetRule.EntityListOptional,
                QueryMode.Optional,
                allowsIds: true,
                allowsContext: true,
                requiresCallback: true,
                parallel ? InvocationTailRule.WorkerCount : InvocationTailRule.None,
                minimumArity: 1);
            return true;
        }

        if (name is "WhereAll" or "WhereAny" or "WhereNone")
        {
            descriptor = new ApiDescriptor(
                GeneratedApiKind.QueryFactory,
                hasEntity: false,
                ValueDomain.Component,
                Schedule.Sequential,
                InvocationTargetRule.None,
                QueryMode.None,
                allowsIds: true,
                allowsContext: false,
                requiresCallback: false,
                InvocationTailRule.None,
                minimumArity: 1);
            return true;
        }

        if (name is "Where" or "WhereEntity")
        {
            descriptor = new ApiDescriptor(
                GeneratedApiKind.Where,
                entity,
                ValueDomain.Component,
                Schedule.Sequential,
                InvocationTargetRule.None,
                QueryMode.Required,
                allowsIds: false,
                allowsContext: true,
                requiresCallback: true,
                InvocationTailRule.None,
                minimumArity: 1);
            return true;
        }

        if (name is "Add" or "Remove" or "Set" or "Create" or "Destroy")
        {
            descriptor = new ApiDescriptor(
                GeneratedApiKind.Structural,
                hasEntity: false,
                ValueDomain.Component,
                Schedule.Sequential,
                name == "Create" ? InvocationTargetRule.None : InvocationTargetRule.StructuralTarget,
                QueryMode.None,
                allowsIds: true,
                allowsContext: false,
                requiresCallback: false,
                name is "Add" or "Set"
                    ? InvocationTailRule.Values
                    : name == "Create" ? InvocationTailRule.CountOutput : InvocationTailRule.None,
                minimumArity: 1);
            return true;
        }

        descriptor = default;
        return false;
    }
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

        var evidence = new ArityEvidence();
        evidence.Add(genericArity);
        evidence.Add(result.ComponentIdCount == 0 ? null : result.ComponentIdCount);
        evidence.Add(hasValues ? result.TailCount : null);
        if (!evidence.TryBind(descriptor.MinimumArity, out arity))
        {
            return false;
        }

        registrationBinding = result.ComponentIdCount == 0
            ? RegistrationBindingKind.Primary
            : RegistrationBindingKind.Explicit;
        return true;
    }

}

/// <summary>Semantic argument cursor in the canonical target/query/ids/context/callback order.</summary>
internal sealed class InvocationCursor
{
    private readonly SemanticModel _model;
    private readonly SeparatedSyntaxList<ArgumentSyntax> _arguments;
    private readonly ApiDescriptor _descriptor;

    internal InvocationCursor(
        SemanticModel model,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        ApiDescriptor descriptor)
    {
        _model = model;
        _arguments = arguments;
        _descriptor = descriptor;
    }

    internal bool TryRead(int callbackIndex, out InvocationCursorResult result, bool requireQuery = false)
    {
        result = default;
        int index = 0;
        TargetKind target = TargetKind.World;
        bool hasTarget = false;
        bool hasQuery = false;
        int componentIdCount = 0;
        int contextIndex = -1;
        ContextModeKind contextMode = ContextModeKind.None;

        if (_descriptor.Target == InvocationTargetRule.StructuralTarget)
        {
            if (!TryTarget(index, out target))
            {
                return false;
            }

            hasTarget = true;
            index++;
        }
        else if (_descriptor.Target == InvocationTargetRule.EntityListOptional
            && TryTarget(index, out TargetKind candidateTarget)
            && candidateTarget == TargetKind.EntityList)
        {
            target = candidateTarget;
            hasTarget = true;
            index++;
        }

        if (requireQuery || _descriptor.Query == QueryMode.Required
            || (_descriptor.Query == QueryMode.Optional && HasQuery(index)))
        {
            if (!HasQuery(index))
            {
                return false;
            }

            hasQuery = true;
            index++;
        }

        if (_descriptor.AllowsIds)
        {
            while (index < _arguments.Count && IsComponentId(index))
            {
                componentIdCount++;
                index++;
            }
        }

        if (_descriptor.AllowsContext && callbackIndex > index)
        {
            if (callbackIndex != index + 1 || IsComponentId(index))
            {
                return false;
            }

            contextIndex = index;
            contextMode = CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(_arguments[index]));
            index++;
        }

        int tailStart = -1;
        bool hasOutput = false;
        if (_descriptor.RequiresCallback)
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

        int tailCount = _arguments.Count - index;
        if (_descriptor.Tail == InvocationTailRule.None && tailCount != 0)
        {
            return false;
        }

        if (_descriptor.Tail == InvocationTailRule.Values)
        {
            tailStart = index;
        }
        else if (_descriptor.Tail == InvocationTailRule.WorkerCount)
        {
            if (tailCount > 1
                || (tailCount == 1 && !GeneratorSupport.IsInt32(_model.GetTypeInfo(_arguments[index].Expression).Type)))
            {
                return false;
            }

            tailStart = index;
        }
        else if (_descriptor.Tail == InvocationTailRule.CountOutput)
        {
            hasOutput = tailCount == 2
                && GeneratorSupport.IsEntityOutput(_model.GetTypeInfo(_arguments[_arguments.Count - 1].Expression).Type);
            int countIndex = _arguments.Count - (hasOutput ? 2 : 1);
            if (tailCount is < 1 or > 2
                || !GeneratorSupport.IsInt32(_model.GetTypeInfo(_arguments[countIndex].Expression).Type)
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
            componentIdCount,
            contextIndex,
            contextMode,
            tailStart,
            tailCount,
            hasOutput);
        return true;
    }

    private bool HasQuery(int index)
        => index < _arguments.Count
            && _arguments[index].RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            && GeneratorSupport.IsEcsType(_model.GetTypeInfo(_arguments[index].Expression).Type, "Query");

    private bool IsComponentId(int index)
        => GeneratorSupport.IsComponentId(_model.GetTypeInfo(_arguments[index].Expression).Type);

    private bool TryTarget(int index, out TargetKind target)
    {
        target = TargetKind.World;
        if (index >= _arguments.Count)
        {
            return false;
        }

        ArgumentSyntax argument = _arguments[index];
        ITypeSymbol? type = _model.GetTypeInfo(argument.Expression).Type;
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

internal readonly struct InvocationCursorResult
{
    internal InvocationCursorResult(
        TargetKind target,
        bool hasTarget,
        bool hasQuery,
        int componentIdCount,
        int contextIndex,
        ContextModeKind contextMode,
        int tailStart,
        int tailCount,
        bool hasOutput)
    {
        Target = target;
        HasTarget = hasTarget;
        HasQuery = hasQuery;
        ComponentIdCount = componentIdCount;
        ContextIndex = contextIndex;
        ContextMode = contextMode;
        TailStart = tailStart;
        TailCount = tailCount;
        HasOutput = hasOutput;
    }

    internal TargetKind Target { get; }
    internal bool HasTarget { get; }
    internal bool HasQuery { get; }
    internal int ComponentIdCount { get; }
    internal int ContextIndex { get; }
    internal bool HasContext => ContextIndex >= 0;
    internal ContextModeKind ContextMode { get; }
    internal int TailStart { get; }
    internal int TailCount { get; }
    internal bool HasOutput { get; }
}

/// <summary>Reconciles every component-associated arity discovered by a reader.</summary>
internal struct ArityEvidence
{
    private int _canonical;
    private bool _hasValue;
    private bool _valid;

    internal bool Add(int? count)
    {
        if (!count.HasValue)
        {
            return _valid;
        }

        int value = count.Value;
        if (value < 0)
        {
            _valid = false;
            return false;
        }

        if (!_hasValue)
        {
            _canonical = value;
            _hasValue = true;
            _valid = true;
            return true;
        }

        _valid &= _canonical == value;
        return _valid;
    }

    internal bool TryBind(int minimum, out int arity)
    {
        arity = _canonical;
        return _valid && _hasValue && arity >= minimum;
    }
}
