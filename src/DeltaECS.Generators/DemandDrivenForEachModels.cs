using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal sealed record InterceptionSite(
    string Id,
    IterationModel Shape,
    LambdaExpressionSyntax? Lambda,
    IMethodSymbol? MethodGroup,
    string Attribute,
    string[] Usings)
{
    private static string[] DefaultParameterNames(IterationModel shape)
        => (shape.HasContext ? new[] { "context" } : Array.Empty<string>())
            .Concat(shape.HasEntity ? new[] { "entity" } : Array.Empty<string>())
            .Concat(Enumerable.Range(0, shape.Pattern.Length).Select(static index => "component" + index))
            .ToArray();

    internal CallSiteBinding Binding { get; } = new(
        Lambda,
        MethodGroup,
        Shape.Pattern.Any(static value => value == 'V'),
        DefaultParameterNames(Shape));

    internal IterationModel IterationModel => Shape;
}

internal sealed class IterationModel(
    RegistrationBindingKind registrationBinding,
    bool hasEntity,
    bool hasContext,
    bool isFunctor,
    string pattern,
    string[] components,
    string? functorType,
    string? contextType,
    bool parallel = false,
    ContextModeKind contextMode = ContextModeKind.None,
    string methodName = "ForEach",
    bool hasEntityTarget = false,
    bool hasQuery = true,
    bool isStamp = false,
    TypeBindingKind typeBinding = TypeBindingKind.CallbackInferred,
    ContextModeKind functorPassMode = ContextModeKind.Ref,
    string namespaceName = "",
    bool orderedQueryReceiver = false)
{
    public RegistrationBindingKind RegistrationBinding => Api.Selector.RegistrationBinding;
    public bool HasEntity => Api.Callback?.HasEntity == true;
    public bool HasContext => Api.Context.Mode != ContextModeKind.None;
    public bool IsFunctor => Api.Callback?.Source == CallbackSource.Functor;
    public bool ImplicitComponents => Api.Selector.TypeBinding != TypeBindingKind.Generic;
    public string Pattern => string.Concat(ComponentModels.Select(static component =>
        component.Access == AccessKind.StampRead ? 'I' : component.Access switch
        {
            AccessKind.RowWrite => 'W',
            AccessKind.RefReadonly => 'R',
            AccessKind.RowRead => 'I',
            _ => 'V'
        }));
    public string[] Components => ComponentModels.Select(static component => component.ResolvedTypeName).ToArray();
    public ImmutableArray<ComponentModel> ComponentModels => Api.Selector.Components;
    public string? FunctorType => Api.Callback?.TypeName;
    public string? ContextType => Api.Context.TypeName;
    public bool Parallel => Api.Execution.Schedule == Schedule.Parallel;
    public ContextModeKind ContextMode => Api.Context.Mode;
    public ContextModeKind FunctorPassMode => Api.Callback?.PassMode ?? ContextModeKind.None;
    public bool HasEntityTarget => Api.Target == TargetKind.EntityList;
    public bool HasQuery => Api.Query != QueryMode.None;
    public bool IsStamp => Api.Execution.Value == ValueDomain.Stamp;
    public string MethodName => Api.Name ?? "ForEach";
    public string Namespace { get; } = namespaceName;
    public bool OrderedQueryReceiver { get; } = orderedQueryReceiver;
    internal ApiModel Api { get; } = GeneratorSupport.CreateIterationShape(
        isStamp,
        parallel,
        hasEntity,
        hasEntityTarget,
        hasQuery,
        registrationBinding,
        typeBinding,
        isFunctor,
        hasContext,
        contextMode,
        pattern,
        components,
        functorType,
        contextType,
        methodName,
        functorPassMode);
    public string Key => Namespace + "|" + Api.SignatureKey + (OrderedQueryReceiver ? "|OrderedQuery" : string.Empty);
}

internal sealed record IterationRenderModel(
    IterationModel Shape,
    bool RenderContracts,
    bool Profiling,
    ImmutableArray<ContextModeKind> SupportedContextModes);
