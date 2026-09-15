using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal sealed class InterceptionSite
{
    internal InterceptionSite(
        string id,
        IterationModel shape,
        LambdaExpressionSyntax? lambda,
        IMethodSymbol? methodGroup,
        string attribute,
        string[] usings)
    {
        Id = id;
        IterationModel = shape;
        Binding = lambda is null
            ? new CallSiteBinding(
                DefaultParameterNames(shape),
                body: null,
                bodyIsBlock: false,
                canInline: false,
                methodGroup is null ? null : CallbackReader.MethodGroupTarget(methodGroup))
            : new CallSiteBinding(
                lambda,
                methodGroup,
                shape.Pattern.Any(static value => value == 'V'));
        Attribute = attribute;
        Usings = usings;
    }

    private static string[] DefaultParameterNames(IterationModel shape)
        => (shape.HasContext ? new[] { "context" } : Array.Empty<string>())
            .Concat(shape.HasEntity ? new[] { "entity" } : Array.Empty<string>())
            .Concat(Enumerable.Range(0, shape.Pattern.Length).Select(static index => "component" + index))
            .ToArray();

    internal string Id { get; }
    internal IterationModel IterationModel { get; }
    internal string Attribute { get; }
    internal string[] Usings { get; }
    internal CallSiteBinding Binding { get; }
}

internal sealed class IterationModel
{
    public IterationModel(
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
        TypeBindingKind typeBinding = TypeBindingKind.CallbackInferred)
    {
        Api = GeneratorSupport.CreateIterationShape(
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
            methodName);
    }

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
    public bool HasEntityTarget => Api.Target == TargetKind.EntityList;
    public bool HasQuery => Api.Query != QueryMode.None;
    public bool IsStamp => Api.Execution.Value == ValueDomain.Stamp;
    public string MethodName => Api.Name ?? "ForEach";
    internal ApiModel Api { get; }
    public string Key => Api.SignatureKey;
}

internal sealed class IterationRenderModel
{
    internal IterationRenderModel(
        IterationModel shape,
        bool renderContracts,
        bool profiling,
        ImmutableArray<ContextModeKind> supportedContextModes)
    {
        Shape = shape;
        RenderContracts = renderContracts;
        Profiling = profiling;
        SupportedContextModes = supportedContextModes;
    }

    internal IterationModel Shape { get; }
    internal bool RenderContracts { get; }
    internal bool Profiling { get; }
    internal ImmutableArray<ContextModeKind> SupportedContextModes { get; }
}
