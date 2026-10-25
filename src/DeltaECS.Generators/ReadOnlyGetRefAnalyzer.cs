using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Delta.ECS.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyGetRefAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = ReadOnlyAccessDiagnostic.DiagnosticId;
    internal const string RefLocalProperty = ReadOnlyAccessDiagnostic.RefLocalProperty;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ReadOnlyAccessDiagnostic.Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeFunctorMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.ValueText != "GetRef"
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsEcsGetRef(method))
        {
            return;
        }

        VariableDeclaratorSyntax? variable = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (variable?.Initializer is null
            || !IsDirectInitializer(variable.Initializer.Value, invocation)
            || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol local
            || !IsReadOnly(variable, local, context.SemanticModel, context.CancellationToken, out bool isRefLocal))
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(RefLocalProperty, isRefLocal.ToString());
        context.ReportDiagnostic(Diagnostic.Create(
            ReadOnlyAccessDiagnostic.Rule,
            memberAccess.Name.GetLocation(),
            properties,
            "GetRef()",
            "GetReadRef()"));
    }

    private static void AnalyzeFunctorMethod(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax
            || methodSyntax.Identifier.ValueText != "Invoke"
            || context.SemanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken) is not IMethodSymbol method
            || method.ContainingType is null
            || !CallbackReader.TryGetForEachMarker(method.ContainingType, out bool hasContext, out bool hasEntity, out ITypeSymbol? contextType)
            || !CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: false, entityRef: true))
        {
            return;
        }

        int firstComponent = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        for (int index = firstComponent; index < method.Parameters.Length; index++)
        {
            IParameterSymbol parameter = method.Parameters[index];
            if (parameter.RefKind != RefKind.Ref
                || !ReadOnlyForEachRefAnalyzer.IsReadOnly(methodSyntax, parameter, context.SemanticModel, context.CancellationToken))
            {
                continue;
            }

            string modifier = ReadOnlyForEachRefAnalyzer.SupportsRefReadonly(methodSyntax.SyntaxTree.Options)
                ? "ref readonly"
                : "in";
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReadOnlyAccessDiagnostic.ModifierProperty, modifier);
            context.ReportDiagnostic(Diagnostic.Create(
                ReadOnlyAccessDiagnostic.Rule,
                methodSyntax.ParameterList.Parameters[index].Modifiers.First(static token => token.IsKind(SyntaxKind.RefKeyword)).GetLocation(),
                properties,
                parameter.Name,
                modifier));
        }
    }

    private static bool IsDirectInitializer(ExpressionSyntax initializer, InvocationExpressionSyntax invocation)
    {
        while (initializer is ParenthesizedExpressionSyntax parenthesized)
        {
            initializer = parenthesized.Expression;
        }

        if (initializer is RefExpressionSyntax refExpression)
        {
            initializer = refExpression.Expression;
        }

        while (initializer is ParenthesizedExpressionSyntax parenthesized)
        {
            initializer = parenthesized.Expression;
        }

        return ReferenceEquals(initializer, invocation);
    }

    private static bool IsEcsGetRef(IMethodSymbol method)
        => method.Name == "GetRef"
            && method.ReturnsByRef
            && !method.ReturnsByRefReadonly
            && method.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace
            && method.ContainingType.Name is "World" or "EntityRef";

    private static bool IsReadOnly(
        VariableDeclaratorSyntax variable,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out bool isRefLocal)
    {
        isRefLocal = variable.Parent?.Parent is LocalDeclarationStatementSyntax
            && variable.Parent is VariableDeclarationSyntax declaration && declaration.Type is RefTypeSyntax;

        var scope = variable.Ancestors().FirstOrDefault(IsExecutableScope);
        if (scope is null)
        {
            return false;
        }
        var operation = semanticModel.GetOperation(scope, cancellationToken);
        if (operation is null)
        {
            return false;
        }

        var walker = new WritableUseWalker(local);
        walker.Visit(operation);
        if (walker.HasWritableUse)
        {
            return false;
        }

        foreach (IdentifierNameSyntax identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, local)
                && identifier.Ancestors().TakeWhile(node => !ReferenceEquals(node, variable)).Any(static node => node is RefExpressionSyntax))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExecutableScope(SyntaxNode node)
        => node is BaseMethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax
            or AccessorDeclarationSyntax;

    private sealed class WritableUseWalker : OperationWalker
    {
        private readonly ILocalSymbol _local;

        internal bool HasWritableUse { get; private set; }

        internal WritableUseWalker(ILocalSymbol local) => _local = local;

        public override void VisitAddressOf(IAddressOfOperation operation)
        {
            HasWritableUse |= ReferencesLocal(operation.Reference);
            base.VisitAddressOf(operation);
        }

        public override void VisitArgument(IArgumentOperation operation)
        {
            RefKind argumentRefKind = operation.Parameter?.RefKind ?? RefKind.None;
            bool passedReadOnly = argumentRefKind == RefKind.In || GeneratorSupport.IsRefReadonly(argumentRefKind);
            if (ReferencesLocal(operation.Value)
                && (argumentRefKind is RefKind.Ref or RefKind.Out
                    || (!passedReadOnly
                        && (operation.Value.Type?.IsReferenceType == true
                            || operation.Value is ILocalReferenceOperation))))
            {
                HasWritableUse = true;
            }

            base.VisitArgument(operation);
        }

        public override void VisitCompoundAssignment(ICompoundAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesLocal(operation.Target);
            base.VisitCompoundAssignment(operation);
        }

        public override void VisitEventAssignment(IEventAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesLocal(operation.EventReference);
            base.VisitEventAssignment(operation);
        }

        public override void VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation)
        {
            HasWritableUse |= ReferencesLocal(operation.Target);
            base.VisitIncrementOrDecrement(operation);
        }

        public override void VisitInvocation(IInvocationOperation operation)
        {
            var instance = operation.Instance;
            if (instance is not null
                && ReferencesLocal(instance)
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
                && ReferencesLocal(instance)
                && operation.Property.GetMethod?.IsReadOnly != true)
            {
                HasWritableUse = true;
            }

            base.VisitPropertyReference(operation);
        }

        public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
        {
            HasWritableUse |= ReferencesLocal(operation.Target);
            base.VisitSimpleAssignment(operation);
        }

        private bool ReferencesLocal(IOperation? operation)
        {
            if (operation is null)
            {
                return false;
            }

            if (operation is ILocalReferenceOperation localReference
                && SymbolEqualityComparer.Default.Equals(localReference.Local, _local))
            {
                return true;
            }

            foreach (IOperation child in operation.ChildOperations)
            {
                if (ReferencesLocal(child))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
