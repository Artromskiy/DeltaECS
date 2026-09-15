using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Generates stack-only read-only predicate views for query-wide mutation terminals.</summary>
[Generator]
public sealed class GeneratedWhereGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor WritablePredicate = new(
        "DECSGEN006",
        "Where predicate is read-only",
        "Where predicates cannot write components; use 'in' or 'ref readonly' parameters and mutate in a terminal callback",
        "Where",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor EntityPredicateRequiresWhereEntity = new(
        "DECSGEN007",
        "Where predicate does not receive Entity",
        "Where predicates cannot receive Entity; use 'WhereEntity' for an entity parameter",
        "Where",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            GeneratorPipeline.Input(context)
                .Combine(context.AnalyzerConfigOptionsProvider.Select(
                    static (provider, _) => GeneratorSupport.IsInterceptionEnabled(provider.GlobalOptions))),
            static (productionContext, input) => Execute(
                input.Left.Compilation,
                input.Left.Invocations,
                productionContext,
                input.Right));
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationCandidate> discoveredInvocations,
        SourceProductionContext context,
        bool interceptorsEnabled)
    {
        var shapes = new ShapeRegistry<PredicateModel>(static shape => shape.Key);
        var whereCalls = new Dictionary<InvocationExpressionSyntax, (PredicateModel Shape, WherePredicateBinding Binding)>();
        var interceptionSites = new Dictionary<string, List<WhereInterceptionSite>>(StringComparer.Ordinal);
        bool languageSupportsInterceptors = GeneratorSupport.SupportsInterceptors(compilation);
        ImmutableArray<InvocationCandidate> invocations = GeneratorSupport.ExcludeGenerated(discoveredInvocations);
        foreach (InvocationCandidate invocationCandidate in invocations)
        {
            InvocationExpressionSyntax invocation = invocationCandidate.Invocation;
            SemanticModel model = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryReadPredicate(model, invocation, out PredicateModel? candidate)
                || candidate is null)
            {
                if (IsWritablePredicate(model, invocation))
                {
                    context.ReportDiagnostic(Diagnostic.Create(WritablePredicate, invocation.GetLocation()));
                }
                else if (IsEntityPredicateWithoutWhereEntity(model, invocation))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EntityPredicateRequiresWhereEntity, invocation.GetLocation()));
                }

                continue;
            }

            PredicateModel shape = shapes.GetOrAdd(
                candidate,
                static (existing, duplicate) => existing.Merge(duplicate));

            whereCalls[invocation] = (shape, candidate.ConcreteBinding);
        }

        foreach (InvocationCandidate invocationCandidate in invocations)
        {
            InvocationExpressionSyntax invocation = invocationCandidate.Invocation;
            SemanticModel model = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryGetWhereReceiver(invocation, out InvocationExpressionSyntax? whereInvocation)
                || !whereCalls.TryGetValue(whereInvocation, out (PredicateModel Shape, WherePredicateBinding Binding) whereCall)
                || !TryReadTerminal(model, invocation, out TerminalModel? terminal)
                || terminal is null)
            {
                continue;
            }

            PredicateModel shape = whereCall.Shape;

            shape.Terminals.GetOrAdd(
                terminal,
                static (existing, duplicate) => existing.Merge(duplicate));

            if (interceptorsEnabled
                && languageSupportsInterceptors
                && !shape.IsFunctor
                && (terminal.IsCallback || terminal.Kind is TerminalKind.Destroy or TerminalKind.Add or TerminalKind.Remove)
                && TryCreateInterceptionSite(model, whereInvocation, invocation, shape, whereCall.Binding, terminal, invocation.SyntaxTree, out WhereInterceptionSite? site)
                && site is { } interceptionSite)
            {
                if (!interceptionSites.TryGetValue(shape.Key, out List<WhereInterceptionSite>? sites))
                {
                    sites = new List<WhereInterceptionSite>();
                    interceptionSites.Add(shape.Key, sites);
                }

                sites.Add(interceptionSite);
            }
        }

        foreach (TerminalModel initializer in shapes.Ordered()
            .SelectMany(static shape => shape.Terminals.Ordered())
            .Where(static terminal => terminal.HasValues)
            .GroupBy(static terminal => terminal.Arity)
            .Select(static group => group.First())
            .OrderBy(static terminal => terminal.Arity))
        {
            context.AddSource(
                "GeneratedWhereAddValues" + initializer.Arity.ToString(CultureInfo.InvariantCulture) + ".g.cs",
                GeneratedWhereTemplates.RenderValueInitializer(initializer));
        }

        foreach (PredicateModel shape in shapes.Ordered())
        {
            string hash = GeneratorSupport.StableName(shape.Key);
            context.AddSource("GeneratedWhere_" + hash + ".g.cs", GeneratedWhereTemplates.Render(shape));
            if (interceptionSites.TryGetValue(shape.Key, out List<WhereInterceptionSite>? sites))
            {
                foreach (WhereInterceptionSite site in sites.OrderBy(static site => site.Id, StringComparer.Ordinal))
                {
                    context.AddSource(
                        "GeneratedWhereInterceptor_" + site.Id + ".g.cs",
                        GeneratedWhereTemplates.RenderInterceptor(site));
                }
            }
        }
    }

    private static bool TryCreateInterceptionSite(
        SemanticModel model,
        InvocationExpressionSyntax whereInvocation,
        InvocationExpressionSyntax terminalInvocation,
        PredicateModel shape,
        WherePredicateBinding predicateShapeBinding,
        TerminalModel terminal,
        SyntaxTree tree,
        out WhereInterceptionSite? site)
    {
        site = null;
        if (whereInvocation.ArgumentList.Arguments.Count is not (2 or 3))
        {
            return false;
        }

        ExpressionSyntax predicateExpression = whereInvocation.ArgumentList.Arguments[whereInvocation.ArgumentList.Arguments.Count - 1].Expression;
        LambdaExpressionSyntax? predicate = predicateExpression as LambdaExpressionSyntax;
        IMethodSymbol? predicateMethod = null;
        if (predicate is not null)
        {
            if (!predicate.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
            {
                return false;
            }
        }
        else if (!TryResolveWherePredicateMethod(model, whereInvocation, shape, predicateExpression, out predicateMethod))
        {
            return false;
        }

        LambdaExpressionSyntax? action = null;
        IMethodSymbol? actionMethod = null;
        if (terminal.IsCallback && !terminal.IsFunctor)
        {
            if (terminalInvocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            ExpressionSyntax actionExpression = terminalInvocation.ArgumentList.Arguments[0].Expression;
            action = actionExpression as LambdaExpressionSyntax;
            if (action is not null)
            {
                if (!action.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
                {
                    return false;
                }
            }
            else if (!CallbackReader.TryGetStaticMethodGroupTarget(model, actionExpression, out actionMethod))
            {
                return false;
            }
        }
        else if (terminal.IsCallback
            && (terminalInvocation.ArgumentList.Arguments.Count != (terminal.HasContext ? 2 : 1)
                || !terminalInvocation.ArgumentList.Arguments.Last().RefKindKeyword.IsKind(SyntaxKind.RefKeyword)))
        {
            return false;
        }

        if (!GeneratorSupport.TryGetInterceptionLocation(model, terminalInvocation, out string locationData, out string attributeSyntax))
        {
            return false;
        }

        if ((predicate is not null && !CallbackReader.HasAccessibleLambdaReferences(model, predicate, out _))
            || (action is not null && !CallbackReader.HasAccessibleLambdaReferences(model, action, out _)))
        {
            return false;
        }

        ParameterSyntax[] predicateParameters = predicate is null ? Array.Empty<ParameterSyntax>() : CallbackReader.LambdaParameters(predicate);
        ParameterSyntax[] actionParameters = action is null ? Array.Empty<ParameterSyntax>() : CallbackReader.LambdaParameters(action);
        int predicateComponentStart = (shape.HasContext ? 1 : 0) + (shape.HasEntity ? 1 : 0);
        string[] predicateComponents = predicate is null
            ? predicateMethod is { } resolvedPredicate
                ? resolvedPredicate.Parameters.Skip(predicateComponentStart)
                    .Select(static parameter => GeneratorSupport.DisplayType(parameter.Type))
                    .ToArray()
                : shape.Components
            : predicateParameters
                .Skip(predicateComponentStart)
                .Select(parameter => GeneratorSupport.DisplayType(model.GetTypeInfo(parameter.Type!).Type!))
                .ToArray();
        int actionComponentStart = terminal.HasEntity ? 1 : 0;
        string[] actionComponents = action is null
            ? actionMethod is { } resolvedAction
                ? resolvedAction.Parameters.Skip(actionComponentStart)
                    .Select(static parameter => GeneratorSupport.DisplayType(parameter.Type))
                    .ToArray()
                : terminal.Components
            : actionParameters
                .Skip(actionComponentStart)
                .Select(parameter => GeneratorSupport.DisplayType(model.GetTypeInfo(parameter.Type!).Type!))
                .ToArray();

        var usings = tree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(static directive => directive.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        ISymbol? enclosing = model.GetEnclosingSymbol(terminalInvocation.SpanStart);
        if (enclosing is not null)
        {
            string namespaceName = enclosing.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (namespaceName.Length > 0)
            {
                usings = usings
                    .Append("using global::" + namespaceName + ";")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            if (enclosing.ContainingType is { Arity: 0 } containingType
                && GeneratorSupport.IsAccessibleSymbol(containingType))
            {
                usings = usings
                    .Append("using static " + containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ";")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        }
        string id = GeneratorSupport.StableName(shape.Key + "|" + terminal.Key + "|" + locationData);
        site = new WhereInterceptionSite(
            id,
            shape,
            terminal,
            predicateShapeBinding,
            predicateMethod is null ? null : CallbackReader.MethodGroupTarget(predicateMethod),
            actionMethod is null ? null : CallbackReader.MethodGroupTarget(actionMethod),
            predicate is null
                ? GeneratedWhereModelNames.Predicate(shape)
                : predicateParameters.Select(static parameter => parameter.Identifier.ValueText).ToArray(),
            action is null
                ? GeneratedWhereModelNames.Action(terminal)
                : actionParameters.Select(static parameter => parameter.Identifier.ValueText).ToArray(),
            predicate switch
            {
                { Body: BlockSyntax block } => block.Statements.ToFullString(),
                { Body: ExpressionSyntax expression } => expression.ToString(),
                _ => null
            },
            action switch
            {
                { Body: BlockSyntax block } => block.Statements.ToFullString(),
                { Body: ExpressionSyntax expression } => expression.ToString(),
                _ => null
            },
            predicate?.Body is BlockSyntax,
            action?.Body is BlockSyntax,
            predicateComponents,
            actionComponents,
            attributeSyntax,
            usings);
        return true;
    }

    private static bool TryResolveWherePredicateMethod(
        SemanticModel model,
        InvocationExpressionSyntax whereInvocation,
        PredicateModel shape,
        ExpressionSyntax expression,
        out IMethodSymbol? method)
    {
        if (whereInvocation.Expression is MemberAccessExpressionSyntax member
            && member.Name is GenericNameSyntax genericName)
        {
            ImmutableArray<ITypeSymbol?> expectedTypes = genericName.TypeArgumentList.Arguments
                .Select(argument => model.GetTypeInfo(argument).Type)
                .ToImmutableArray();
            int expectedCount = expectedTypes.Length + (shape.HasEntity ? 1 : 0);
            if (CallbackReader.TryGetMethodGroupTarget(
                    model,
                    expression,
                    expectedCount,
                    expectedTypes,
                    shape.HasEntity,
                    out method)
                && method is { ReturnsVoid: false, ReturnType.SpecialType: SpecialType.System_Boolean })
            {
                return true;
            }
        }

        return CallbackReader.TryGetStaticMethodGroupTarget(model, expression, out method);
    }


    private static bool TryReadPredicate(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out PredicateModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not IdentifierNameSyntax methodName
            || methodName.Identifier.ValueText is not ("Where" or "WhereEntity")
            || !ApiDescriptor.TryGet(methodName.Identifier.ValueText, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Where
            || invocation.ArgumentList.Arguments.Count is not (2 or 3)
            || !GeneratorSupport.IsNamedType(model.GetTypeInfo(member.Expression).Type, "World"))
        {
            return false;
        }

        var cursor = new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor);
        if (!cursor.TryRead(invocation.ArgumentList.Arguments.Count - 1, out _))
        {
            return false;
        }

        bool hasEntity = methodName.Identifier.ValueText == "WhereEntity";

        ArgumentSyntax predicateArgument = invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1];
        if (predicateArgument.Expression is not LambdaExpressionSyntax lambda)
        {
            if (TryReadStaticPredicateMethodGroup(model, invocation, predicateArgument.Expression, hasEntity, out shape))
            {
                return true;
            }

            ArgumentSyntax? contextArgument = invocation.ArgumentList.Arguments.Count == 3
                ? invocation.ArgumentList.Arguments[1]
                : null;
            return TryReadFunctorPredicate(model, predicateArgument, contextArgument, hasEntity, out shape);
        }

        ArgumentSyntax? lambdaContextArgument = invocation.ArgumentList.Arguments.Count == 3
            ? invocation.ArgumentList.Arguments[1]
            : null;
        bool hasContext = lambdaContextArgument is not null;
        ITypeSymbol? contextType = null;
        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        int parameterStart = 0;
        if (hasContext)
        {
            if (!lambdaContextArgument!.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || parameters.Length == 0
                || !IsContextParameter(model, parameters[0], lambdaContextArgument, out contextType))
            {
                return false;
            }

            parameterStart = 1;
        }

        if (hasEntity)
        {
            if (parameters.Length <= parameterStart || !CallbackReader.IsEntityParameter(model, parameters[parameterStart]))
            {
                return false;
            }

            parameterStart++;
        }
        else if (parameters.Skip(parameterStart).Any(parameter => CallbackReader.IsEntityParameter(model, parameter)))
        {
            return false;
        }

        string pattern = new string(parameters
            .Skip(parameterStart)
            .Select(parameter => GeneratorSupport.PatternLetter(parameter))
            .ToArray());
        if (parameters.Skip(parameterStart).Any(parameter =>
                !CallbackReader.HasTypedAccessibleParameter(model, parameter)
                || GeneratorSupport.PatternLetter(parameter) == 'W'))
        {
            return false;
        }

        shape = new PredicateModel(
            pattern,
            isFunctor: false,
            functorType: null,
            hasEntity,
            hasContext,
            contextType is null ? null : GeneratorSupport.DisplayType(contextType),
            components: null);
        return true;
    }

    private static bool TryReadFunctorPredicate(
        SemanticModel model,
        ArgumentSyntax argument,
        ArgumentSyntax? contextArgument,
        bool hasEntity,
        out PredicateModel? shape)
    {
        shape = null;
        if (!argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            || model.GetTypeInfo(argument.Expression).Type is not INamedTypeSymbol functorType
            || !CallbackReader.HasWherePredicateMarker(functorType)
            || !GeneratorSupport.IsAccessibleSymbol(functorType))
        {
            return false;
        }

        bool hasContext = contextArgument is not null;
        ITypeSymbol? contextType = null;
        if (hasContext)
        {
            if (!contextArgument!.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || model.GetTypeInfo(contextArgument.Expression).Type is not ITypeSymbol actualContext)
            {
                return false;
            }

            contextType = actualContext;
        }

        IMethodSymbol[] invokes = functorType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && method.ReturnType.SpecialType == SpecialType.System_Boolean)
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: true))
            .Where(method => method.Parameters.Skip((hasContext ? 1 : 0) + (hasEntity ? 1 : 0)).All(
                static parameter => CallbackReader.IsSupportedReadRefKind(parameter.RefKind)))
            .ToArray();
        if (invokes.Length != 1)
        {
            return false;
        }

        IMethodSymbol invoke = invokes[0];
        int componentStart = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IParameterSymbol[] components = invoke.Parameters.Skip(componentStart).ToArray();
        if (!hasEntity
            && components.Any(static component => component.RefKind == RefKind.None && GeneratorSupport.IsEntityType(component.Type)))
        {
            return false;
        }

        if (components.Any(static parameter => !GeneratorSupport.IsAccessibleSymbol(parameter.Type)))
        {
            return false;
        }

        string pattern = new(components.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray());
        shape = new PredicateModel(
            pattern,
            isFunctor: true,
            functorType: GeneratorSupport.DisplayType(functorType),
            hasEntity,
            hasContext,
            contextType: hasContext && contextType is { } resolvedContext ? GeneratorSupport.DisplayType(resolvedContext) : null,
            components: components.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray());
        return true;
    }

    private static bool TryReadStaticPredicateMethodGroup(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax expression,
        bool hasEntity,
        out PredicateModel? shape)
    {
        shape = null;
        if (!CallbackReader.TryGetStaticMethodGroupTarget(model, expression, out IMethodSymbol? method)
            || method is not { ReturnsVoid: false, ReturnType.SpecialType: SpecialType.System_Boolean })
        {
            return false;
        }

        int argumentCount = invocation.ArgumentList.Arguments.Count;
        bool hasContext = argumentCount == 3;
        int parameterIndex = 0;
        ITypeSymbol? contextType = null;
        if (hasContext)
        {
            ArgumentSyntax contextArgument = invocation.ArgumentList.Arguments[1];
            if (!contextArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || method.Parameters.Length == 0
                || method.Parameters[0].RefKind != RefKind.Ref
                || model.GetTypeInfo(contextArgument.Expression).Type is not ITypeSymbol actualContext
                || !SymbolEqualityComparer.Default.Equals(actualContext, method.Parameters[0].Type))
            {
                return false;
            }

            contextType = method.Parameters[0].Type;
            parameterIndex++;
        }

        if (hasEntity)
        {
            if (method.Parameters.Length <= parameterIndex
                || method.Parameters[parameterIndex].RefKind != RefKind.None
                || !GeneratorSupport.IsEntityType(method.Parameters[parameterIndex].Type))
            {
                return false;
            }

            parameterIndex++;
        }

        IParameterSymbol[] components = method.Parameters.Skip(parameterIndex).ToArray();
        if (components.Any(static parameter => !GeneratorSupport.IsAccessibleSymbol(parameter.Type)
                || !CallbackReader.IsSupportedReadRefKind(parameter.RefKind)))
        {
            return false;
        }

        if (!hasEntity && components.Any(static parameter => parameter.RefKind == RefKind.None && GeneratorSupport.IsEntityType(parameter.Type)))
        {
            return false;
        }

        shape = new PredicateModel(
            new string(components.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray()),
            isFunctor: false,
            functorType: null,
            hasEntity,
            hasContext,
            contextType is null ? null : GeneratorSupport.DisplayType(contextType),
            components.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray());
        shape.RegisterStaticMethodGroup();
        return true;
    }

    private static bool TryReadStaticTerminalMethodGroup(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool hasEntity,
        out TerminalModel? terminal)
    {
        terminal = null;
        if (!CallbackReader.TryGetStaticMethodGroupTarget(model, invocation.ArgumentList.Arguments[0].Expression, out IMethodSymbol? method)
            || method is not { ReturnsVoid: true, MethodKind: MethodKind.Ordinary, Arity: 0 }
            || !GeneratorSupport.IsAccessibleSymbol(method))
        {
            return false;
        }

        int parameterIndex = 0;
        if (hasEntity)
        {
            if (method.Parameters.Length == 0
                || method.Parameters[0].RefKind != RefKind.None
                || !GeneratorSupport.IsEntityType(method.Parameters[0].Type))
            {
                return false;
            }

            parameterIndex++;
        }

        IParameterSymbol[] components = method.Parameters.Skip(parameterIndex).ToArray();
        if (components.Length == 0
            || components.Any(static parameter => !GeneratorSupport.IsAccessibleSymbol(parameter.Type)
                || !CallbackReader.IsSupportedRefKind(parameter.RefKind)))
        {
            return false;
        }

        if (!hasEntity && components.Any(static parameter => GeneratorSupport.IsEntityType(parameter.Type)))
        {
            return false;
        }

        terminal = new TerminalModel(
            hasEntity ? TerminalKind.ForEachEntity : TerminalKind.ForEach,
            new string(components.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray()),
            hasEntity,
            components: components.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray(),
            methodGroupTarget: CallbackReader.MethodGroupTarget(method));
        terminal.RegisterStaticMethodGroup();
        return true;
    }

    private static bool IsWritablePredicate(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not IdentifierNameSyntax methodName
            || methodName.Identifier.ValueText is not ("Where" or "WhereEntity")
            || invocation.ArgumentList.Arguments.Count is not (2 or 3)
            || !invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            || !GeneratorSupport.IsNamedType(model.GetTypeInfo(member.Expression).Type, "World")
            || invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1].Expression is not LambdaExpressionSyntax lambda)
        {
            return false;
        }

        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        int parameterStart = invocation.ArgumentList.Arguments.Count == 3 ? 1 : 0;
        return parameters.Length > parameterStart
            && parameters.Skip(parameterStart + (methodName.Identifier.ValueText == "WhereEntity" ? 1 : 0))
                .Any(static parameter => GeneratorSupport.PatternLetter(parameter) == 'W');
    }

    private static bool IsEntityPredicateWithoutWhereEntity(SemanticModel model, InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name is not IdentifierNameSyntax methodName
            || methodName.Identifier.ValueText != "Where"
            || invocation.ArgumentList.Arguments.Count is not (2 or 3)
            || !invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.InKeyword)
            || !GeneratorSupport.IsNamedType(model.GetTypeInfo(member.Expression).Type, "World"))
        {
            return false;
        }

        ArgumentSyntax predicateArgument = invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1];
        if (predicateArgument.Expression is LambdaExpressionSyntax lambda)
        {
            ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
            int parameterStart = invocation.ArgumentList.Arguments.Count == 3 ? 1 : 0;
            return parameters.Skip(parameterStart).Any(parameter => CallbackReader.IsEntityParameter(model, parameter));
        }

        if (!predicateArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            || model.GetTypeInfo(predicateArgument.Expression).Type is not INamedTypeSymbol functorType
            || !CallbackReader.HasWherePredicateMarker(functorType))
        {
            return false;
        }

        int componentStart = invocation.ArgumentList.Arguments.Count == 3 ? 1 : 0;
        return functorType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && method.ReturnType.SpecialType == SpecialType.System_Boolean)
            .Any(method => method.Parameters
                .Skip(componentStart)
                .Any(parameter => parameter.RefKind == RefKind.None && GeneratorSupport.IsEntityType(parameter.Type)));
    }

    private static bool TryGetWhereReceiver(
        InvocationExpressionSyntax invocation,
        out InvocationExpressionSyntax whereInvocation)
    {
        whereInvocation = null!;
        return invocation.Expression is MemberAccessExpressionSyntax member
            && member.Expression is InvocationExpressionSyntax candidate
            && candidate.Expression is MemberAccessExpressionSyntax whereMember
            && whereMember.Name is IdentifierNameSyntax whereName
            && whereName.Identifier.ValueText is "Where" or "WhereEntity"
            && (whereInvocation = candidate) is not null;
    }

    private static bool TryReadTerminal(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out TerminalModel? terminal)
    {
        terminal = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        string name = member.Name.Identifier.ValueText;
        if (!ApiDescriptor.TryGet(name, out ApiDescriptor descriptor)
            || descriptor.Family is not (GeneratedApiKind.Structural or GeneratedApiKind.Iteration))
        {
            return false;
        }

        if (name == "Destroy" && member.Name is IdentifierNameSyntax && invocation.ArgumentList.Arguments.Count == 0)
        {
            terminal = new TerminalModel(TerminalKind.Destroy, string.Empty, hasEntity: false);
            return true;
        }

        if (name is "Add" or "Remove"
            && member.Name is GenericNameSyntax genericName
            && genericName.TypeArgumentList.Arguments.Count >= 1)
        {
            int arity = genericName.TypeArgumentList.Arguments.Count;
            ApiDescriptor terminalDescriptor = descriptor.WithTarget(InvocationTargetRule.None);
            if (!InvocationGrammar.TryReadStructuralMutation(
                    model,
                    invocation.ArgumentList.Arguments,
                    terminalDescriptor,
                    arity,
                    valuesAllowed: name == "Add",
                    out _,
                    out _,
                    out bool hasValues,
                    out RegistrationBindingKind registrationBinding))
            {
                return false;
            }

            terminal = new TerminalModel(
                name == "Add" ? TerminalKind.Add : TerminalKind.Remove,
                string.Empty,
                hasEntity: false,
                components: genericName.TypeArgumentList.Arguments
                    .Select(argument => GeneratorSupport.DisplayType(model.GetTypeInfo(argument).Type!))
                    .ToArray(),
                hasValues: hasValues,
                typeBinding: TypeBindingKind.Generic,
                registrationBinding: registrationBinding);
            return true;
        }

        if (name is "ForEach" or "ForEachEntity"
            && member.Name is IdentifierNameSyntax
            && TryReadFunctorTerminal(model, invocation, name == "ForEachEntity", out terminal))
        {
            return true;
        }

        if (name is not ("ForEach" or "ForEachEntity")
            || member.Name is not IdentifierNameSyntax
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax lambda)
        {
            return TryReadStaticTerminalMethodGroup(model, invocation, name == "ForEachEntity", out terminal);
        }

        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        bool hasEntity = name == "ForEachEntity";
        int componentStart = 0;
        if (hasEntity)
        {
            if (parameters.Length == 0 || !CallbackReader.IsEntityParameter(model, parameters[0], allowImplicit: true))
            {
                return false;
            }

            componentStart = 1;
        }

        ParameterSyntax[] components = parameters.Skip(componentStart).ToArray();
        if (components.Length == 0
            || components.Any(parameter => !CallbackReader.HasTypedAccessibleParameter(model, parameter))
            || (!hasEntity && components.Any(parameter => CallbackReader.IsEntityParameter(model, parameter))))
        {
            return false;
        }

        terminal = new TerminalModel(
            hasEntity ? TerminalKind.ForEachEntity : TerminalKind.ForEach,
            new string(components.Select(GeneratorSupport.PatternLetter).ToArray()),
            hasEntity);
        return true;
    }

    private static bool TryReadFunctorTerminal(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool namedEntity,
        out TerminalModel? terminal)
    {
        terminal = null;
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        ArgumentSyntax functorArgument = invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1];
        if (!functorArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
            || model.GetTypeInfo(functorArgument.Expression).Type is not INamedTypeSymbol functorType
            || !CallbackReader.TryGetForEachMarker(functorType, out bool hasContext, out bool hasEntity, out ITypeSymbol? contextType)
            || namedEntity != hasEntity
            || !GeneratorSupport.IsAccessibleSymbol(functorType))
        {
            return false;
        }

        if (hasContext)
        {
            if (invocation.ArgumentList.Arguments.Count != 2
                || !invocation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type is not ITypeSymbol actualContext
                || !SymbolEqualityComparer.Default.Equals(actualContext, contextType))
            {
                return false;
            }
        }
        else if (invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        int prefixCount = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IMethodSymbol[] invokes = functorType.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .Where(static method => !method.IsStatic && method.ReturnsVoid)
            .Where(method => CallbackReader.HasValidPrefix(method, hasContext, hasEntity, contextType, requireRefContext: true))
            .Where(method => method.Parameters.Skip(prefixCount).All(
                static parameter => CallbackReader.IsSupportedRefKind(parameter.RefKind)))
            .ToArray();
        if (invokes.Length != 1)
        {
            return false;
        }

        IMethodSymbol invoke = invokes[0];
        IParameterSymbol[] components = invoke.Parameters.Skip(prefixCount).ToArray();
        if (components.Length == 0
            || components.Any(static parameter => !GeneratorSupport.IsAccessibleSymbol(parameter.Type)))
        {
            return false;
        }

        terminal = new TerminalModel(
            namedEntity ? TerminalKind.ForEachEntity : TerminalKind.ForEach,
            new string(components.Select(static parameter => GeneratorSupport.PatternLetter(parameter.RefKind)).ToArray()),
            namedEntity,
            isFunctor: true,
            functorType: GeneratorSupport.DisplayType(functorType),
            hasContext,
            contextType: hasContext && contextType is { } resolvedContext ? GeneratorSupport.DisplayType(resolvedContext) : null,
            components: components.Select(static parameter => GeneratorSupport.DisplayType(parameter.Type)).ToArray());
        return true;
    }

    private static bool IsContextParameter(
        SemanticModel model,
        ParameterSyntax parameter,
        ArgumentSyntax contextArgument,
        out ITypeSymbol? contextType)
    {
        contextType = parameter.Type is null ? null : model.GetTypeInfo(parameter.Type).Type;
        return parameter.Modifiers.Count == 1
            && parameter.Modifiers[0].IsKind(SyntaxKind.RefKeyword)
            && contextType is not null
            && contextType.TypeKind != TypeKind.Error
            && contextType is not ITypeParameterSymbol
            && GeneratorSupport.IsAccessibleSymbol(contextType)
            && model.GetTypeInfo(contextArgument.Expression).Type is ITypeSymbol argumentType
            && SymbolEqualityComparer.Default.Equals(contextType, argumentType);
    }

}
