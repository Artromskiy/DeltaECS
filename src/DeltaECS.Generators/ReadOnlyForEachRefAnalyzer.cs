using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Delta.ECS.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyForEachRefAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = ReadOnlyAccessDiagnostic.DiagnosticId;
    internal const string ModifierProperty = ReadOnlyAccessDiagnostic.ModifierProperty;
    private const int FirstRefReadonlyLanguageVersion = 1200;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ReadOnlyAccessDiagnostic.Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    internal static bool SupportsRefReadonly(ParseOptions options)
        => options is CSharpParseOptions csharpOptions
            && (int)csharpOptions.LanguageVersion >= FirstRefReadonlyLanguageVersion;

    internal static bool IsReadOnly(
        SyntaxNode executable,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var operation = semanticModel.GetOperation(executable, cancellationToken);
        if (operation is null)
        {
            return false;
        }

        var walker = new WritableUseWalker(parameter);
        walker.Visit(operation is IAnonymousFunctionOperation anonymousFunction ? anonymousFunction.Body : operation);
        if (walker.HasWritableUse)
        {
            return false;
        }

        foreach (ArgumentSyntax argument in executable.DescendantNodes().OfType<ArgumentSyntax>())
        {
            if (argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword)
                || argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                if (ContainsParameterReference(argument.Expression, parameter, semanticModel, cancellationToken))
                {
                    return false;
                }
            }
        }

        foreach (IdentifierNameSyntax identifier in executable.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, parameter))
            {
                continue;
            }

            if (identifier.Ancestors()
                .TakeWhile(node => !ReferenceEquals(node, executable))
                .Any(static node => node is RefExpressionSyntax))
            {
                return false;
            }
        }

        return true;
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ParameterSyntax parameter
            || !parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.RefKeyword))
            || parameter.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))
            || parameter.Type is null
            || !TryGetForEachLambda(parameter, context.SemanticModel, out LambdaExpressionSyntax? lambda)
            || lambda is null
            || context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not IParameterSymbol parameterSymbol
            || IsContextParameter(lambda, parameterSymbol, context.SemanticModel)
            || !IsReadOnly(lambda, parameterSymbol, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        string modifier = SupportsRefReadonly(parameter.SyntaxTree.Options) ? "ref readonly" : "in";
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(ModifierProperty, modifier);
        context.ReportDiagnostic(Diagnostic.Create(
            ReadOnlyAccessDiagnostic.Rule,
            parameter.Modifiers.First(static modifier => modifier.IsKind(SyntaxKind.RefKeyword)).GetLocation(),
            properties,
            parameter.Identifier.ValueText,
            modifier));
    }

    private static bool TryGetForEachLambda(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        out LambdaExpressionSyntax? lambda)
    {
        LambdaExpressionSyntax? candidate = parameter.Parent as LambdaExpressionSyntax
            ?? parameter.Parent?.Parent as LambdaExpressionSyntax;
        if (candidate is null)
        {
            lambda = null;
            return false;
        }

        ExpressionSyntax expression = candidate;
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        if (expression.Parent is not ArgumentSyntax argument
            || argument.Parent?.Parent is not InvocationExpressionSyntax invocation
            || !TryGetForEachMethod(invocation, semanticModel))
        {
            lambda = null;
            return false;
        }

        lambda = candidate;
        return true;
    }

    private static bool TryGetForEachMethod(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        IMethodSymbol? method = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (method is not null)
        {
            if (!method.Name.StartsWith("ForEach", StringComparison.Ordinal) || method.Name == "ForEachChange")
            {
                return false;
            }

            ITypeSymbol owner = method.IsExtensionMethod && method.Parameters.Length != 0
                ? method.Parameters[0].Type
                : method.ContainingType;
            return IsEcsType(owner);
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.Name.Identifier.ValueText.StartsWith("ForEach", StringComparison.Ordinal)
            || memberAccess.Name.Identifier.ValueText == "ForEachChange")
        {
            return false;
        }

        return IsEcsType(semanticModel.GetTypeInfo(memberAccess.Expression).Type);
    }

    private static bool IsEcsType(ITypeSymbol? type)
        => type?.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;

    private static bool IsContextParameter(
        LambdaExpressionSyntax lambda,
        IParameterSymbol parameter,
        SemanticModel semanticModel)
    {
        if (parameter.Ordinal != 0)
        {
            return false;
        }

        ExpressionSyntax expression = lambda;
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        if (expression.Parent is not ArgumentSyntax callbackArgument
            || callbackArgument.Parent is not ArgumentListSyntax argumentList)
        {
            return false;
        }

        foreach (ArgumentSyntax argument in argumentList.Arguments)
        {
            if (ReferenceEquals(argument, callbackArgument))
            {
                return false;
            }

            if ((argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword))
                && SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetTypeInfo(argument.Expression).Type,
                    parameter.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReadOnly(
        LambdaExpressionSyntax lambda,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
        => IsReadOnly((SyntaxNode)lambda, parameter, semanticModel, cancellationToken);

    private static bool ContainsParameterReference(
        SyntaxNode node,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
        => node.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => SymbolEqualityComparer.Default.Equals(
                semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                parameter));

    private sealed class WritableUseWalker : OperationWalker
    {
        private readonly IParameterSymbol _parameter;

        internal bool HasWritableUse { get; private set; }

        internal WritableUseWalker(IParameterSymbol parameter) => _parameter = parameter;

        public override void VisitAddressOf(IAddressOfOperation operation)
        {
            HasWritableUse |= ReferencesParameter(operation.Reference);
            base.VisitAddressOf(operation);
        }

        public override void VisitArgument(IArgumentOperation operation)
        {
            RefKind argumentRefKind = operation.Parameter?.RefKind ?? RefKind.None;
            bool passedReadOnly = argumentRefKind == RefKind.In || GeneratorSupport.IsRefReadonly(argumentRefKind);
            if (ReferencesParameter(operation.Value)
                && (argumentRefKind is RefKind.Ref or RefKind.Out
                    || (!passedReadOnly
                        && (operation.Value.Type?.IsReferenceType == true
                            || operation.Value is IParameterReferenceOperation))))
            {
                HasWritableUse = true;
            }

            base.VisitArgument(operation);
        }

        public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesParameter(operation.Target);
            base.VisitCompoundAssignment(operation);
        }

        public override void VisitEventAssignment(IEventAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesParameter(operation.EventReference);
            base.VisitEventAssignment(operation);
        }

        public override void VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation)
        {
            HasWritableUse |= ReferencesParameter(operation.Target);
            base.VisitIncrementOrDecrement(operation);
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            var instance = operation.Instance;
            if (instance is not null
                && ReferencesParameter(instance)
                && !operation.TargetMethod.IsReadOnly)
            {
                HasWritableUse = true;
            }

            base.VisitInvocation(operation);
        }

        public override void VisitPropertyReference(IPropertyReferenceOperation operation)
        {
            var instance = operation.Instance;
            if (instance is not null
                && ReferencesParameter(instance)
                && operation.Property.GetMethod?.IsReadOnly != true)
            {
                HasWritableUse = true;
            }

            base.VisitPropertyReference(operation);
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesParameter(operation.Target);
            base.VisitSimpleAssignment(operation);
        }

        private bool ReferencesParameter(IOperation? operation)
        {
            if (operation is null)
            {
                return false;
            }

            if (operation is IParameterReferenceOperation parameterReference
                && SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, _parameter))
            {
                return true;
            }

            foreach (IOperation child in operation.ChildOperations)
            {
                if (ReferencesParameter(child))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
