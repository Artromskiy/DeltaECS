using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

internal enum ReceiverKind
{
    None,
    World,
}

internal sealed class InterceptionSite
{
    private static string FormatMethodGroupTarget(Microsoft.CodeAnalysis.IMethodSymbol method)
    {
        if (method.ContainingType is not { } containingType)
        {
            return ThrowHelper.ThrowMethodGroupTargetMissing(method);
        }

        return containingType.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name;
    }

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
        MethodGroupTarget = methodGroup is null ? null : FormatMethodGroupTarget(methodGroup);
        Attribute = attribute;
        Usings = usings;
        LambdaParameterNames = lambda is null
            ? DefaultParameterNames(shape)
            : GetLambdaParameters(lambda).Select(static parameter => parameter.Identifier.ValueText).ToArray();
        LambdaBody = lambda switch
        {
            { Body: BlockSyntax block } => block.ToString(),
            { Body: ExpressionSyntax expression } => expression.ToString(),
            _ => null
        };
        LambdaBodyIsBlock = lambda?.Body is BlockSyntax;
        CanInlineLambda = lambda is not null
            && !lambda.Body.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any();
        var identifiers = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (lambda is not null)
        {
            foreach (string identifier in lambda.DescendantTokens()
                .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
                .Select(static token => token.ValueText))
            {
                identifiers.Add(identifier);
            }
        }
        LambdaIdentifiers = identifiers.ToImmutable();
    }

    private static string[] DefaultParameterNames(IterationModel shape)
    {
        var names = new List<string>();
        if (shape.HasContext)
        {
            names.Add("context");
        }

        if (shape.HasEntity)
        {
            names.Add("entity");
        }

        names.AddRange(Enumerable.Range(0, shape.Pattern.Length).Select(static index => "component" + index));
        return names.ToArray();
    }

    private static ParameterSyntax[] GetLambdaParameters(LambdaExpressionSyntax lambda)
        => lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.ToArray(),
            _ => Array.Empty<ParameterSyntax>()
        };

    internal string Id { get; }
    internal IterationModel IterationModel { get; }
    internal string[] LambdaParameterNames { get; }
    internal string? LambdaBody { get; }
    internal bool LambdaBodyIsBlock { get; }
    internal bool CanInlineLambda { get; }
    internal ImmutableHashSet<string> LambdaIdentifiers { get; }
    internal string? MethodGroupTarget { get; }
    internal string Attribute { get; }
    internal string[] Usings { get; }
}

internal sealed class IterationModel
{
    public IterationModel(
        ReceiverKind receiver,
        bool explicitIds,
        bool hasEntity,
        bool hasContext,
        bool isFunctor,
        bool implicitComponents,
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
        bool genericSelectors = false)
    {
        Receiver = receiver;
        ExplicitIds = explicitIds;
        HasEntity = hasEntity;
        HasContext = hasContext;
        IsFunctor = isFunctor;
        ImplicitComponents = implicitComponents;
        Pattern = pattern;
        Components = components;
        FunctorType = functorType;
        ContextType = contextType;
        Parallel = parallel;
        ContextMode = contextMode;
        MethodName = methodName;
        HasEntityTarget = hasEntityTarget;
        HasQuery = hasQuery;
        IsStamp = isStamp;
        GenericSelectors = genericSelectors;
        Api = GeneratorSupport.CreateIterationShape(
            receiver.ToString(),
            isStamp,
            parallel,
            hasEntity,
            hasEntityTarget,
            hasQuery,
            explicitIds,
            genericSelectors,
            isFunctor,
            hasContext,
            implicitComponents,
            contextMode,
            pattern,
            components,
            functorType,
            contextType,
            methodName);
    }

    public ReceiverKind Receiver { get; }
    public bool ExplicitIds { get; }
    public bool HasEntity { get; }
    public bool HasContext { get; }
    public bool IsFunctor { get; }
    public bool ImplicitComponents { get; }
    public string Pattern { get; }
    public string[] Components { get; }
    public ImmutableArray<ComponentModel> ComponentModels => Api.Selector.Components;
    public string? FunctorType { get; }
    public string? ContextType { get; }
    public bool Parallel { get; }
    public ContextModeKind ContextMode { get; }
    public bool HasEntityTarget { get; }
    public bool HasQuery { get; }
    public bool IsStamp { get; }
    public bool GenericSelectors { get; }
    public string MethodName { get; }
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
