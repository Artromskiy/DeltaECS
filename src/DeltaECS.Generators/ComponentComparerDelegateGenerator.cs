using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>Generates typed ordered-query comparer delegates and interceptors.</summary>
[Generator]
public sealed class ComponentComparerDelegateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => GeneratorPipeline.RegisterInterceptorInput(
            context,
            Execute,
            OrderedQueryInvocationGrammar.IsCandidate);

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationCandidate> invocations,
        SourceProductionContext context,
        bool interceptionEnabled)
    {
        var models = new ShapeRegistry<ComponentComparerDelegateModel>(static model => model.Key);
        var sites = new List<ComponentComparerDelegateSite>();
        var whereSources = new Dictionary<string, Dictionary<string, PredicateModel>>(StringComparer.Ordinal);
        bool canIntercept = interceptionEnabled && GeneratorSupport.SupportsInterceptors(compilation);

        foreach (InvocationCandidate candidate in invocations)
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            SemanticModel semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!TryReadSite(semanticModel, invocation, out ComponentComparerDelegateSite? site)
                || site is null)
            {
                continue;
            }

            site = site with { Model = models.GetOrAdd(site.Model) };
            if (site.WhereSource is { } whereSource)
            {
                if (!whereSources.TryGetValue(site.Model.Key, out Dictionary<string, PredicateModel>? sources))
                {
                    sources = new Dictionary<string, PredicateModel>(StringComparer.Ordinal);
                    whereSources.Add(site.Model.Key, sources);
                }

                if (!sources.ContainsKey(whereSource.Key))
                {
                    sources.Add(whereSource.Key, whereSource);
                }
            }

            if (canIntercept
                && !site.Model.ContextIsGeneric
                && site.WhereSource?.HasOpenGenericArguments != true
                && site.IsStatic
                && GeneratorSupport.TryGetInterceptionLocation(semanticModel, invocation, out string location, out string attribute))
            {
                site = site with
                {
                    InterceptionLocation = location,
                    InterceptionAttribute = attribute
                };
                sites.Add(site);
            }
        }

        foreach (ComponentComparerDelegateModel model in models.Ordered())
        {
            context.AddSource(
                "GeneratedComponentComparerDelegate_" + model.Hash + ".g.cs",
                ComponentComparerDelegateTemplates.Render(
                    model,
                    whereSources.TryGetValue(model.Key, out Dictionary<string, PredicateModel>? sources)
                        ? sources.Values.ToArray()
                        : Array.Empty<PredicateModel>()));
        }

        foreach (ComponentComparerDelegateSite site in sites.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            context.AddSource(
                "GeneratedComponentComparerInterceptor_" + site.Id + ".g.cs",
                ComponentComparerDelegateTemplates.RenderInterceptor(site));
        }
    }

    private static bool TryReadSite(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out ComponentComparerDelegateSite? site)
    {
        site = null;
        if (!OrderedQueryInvocationGrammar.TryReadMethod(
                semanticModel,
                invocation,
                out MemberAccessExpressionSyntax member,
                out ApiDescriptor descriptor,
                out PredicateModel? whereSource))
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0
            || arguments[arguments.Count - 1].Expression is not LambdaExpressionSyntax lambda)
        {
            return false;
        }

        ParameterSyntax[] parameters = CallbackReader.LambdaParameters(lambda);
        if (parameters.Any(static parameter => parameter.Type is null))
        {
            return false;
        }

        ITypeSymbol?[] parameterTypes = parameters
            .Select(parameter => semanticModel.GetTypeInfo(parameter.Type!).Type)
            .ToArray();
        if (parameterTypes.Any(static type => type is null))
        {
            return false;
        }

        ITypeSymbol?[] explicitTypes = member.Name is GenericNameSyntax genericName
            ? genericName.TypeArgumentList.Arguments.Select(argument => semanticModel.GetTypeInfo(argument).Type).ToArray()
            : Array.Empty<ITypeSymbol?>();
        var matches = new List<(ComponentComparerDelegateModel Model, RegistrationBindingKind Binding, string[] ComponentTypes)>();
        bool hasEntitySignature = HasEntitySignature(parameters, parameterTypes);

        for (int hasContext = 0; hasContext <= 1; hasContext++)
        {
            for (int hasEntity = 0; hasEntity <= 1; hasEntity++)
            {
                if (hasEntitySignature && hasEntity == 0)
                {
                    continue;
                }

                int componentParameterCount = parameters.Length - hasContext - hasEntity * 2;
                if (componentParameterCount <= 0 || (componentParameterCount & 1) != 0)
                {
                    continue;
                }

                int arity = componentParameterCount / 2;
                if (explicitTypes.Length != 0 && explicitTypes.Length != arity)
                {
                    continue;
                }

                if (!OrderedQueryInvocationGrammar.TryReadArguments(
                        semanticModel,
                        invocation,
                        descriptor,
                        arity,
                        hasContext != 0,
                        out InvocationCursorResult selection,
                        out RegistrationBindingKind registrationBinding)
                    || selection.HasContext != (hasContext != 0))
                {
                    continue;
                }

                int leftEntityIndex = hasContext;
                int leftStart = hasContext + hasEntity;
                int rightEntityIndex = leftStart + arity;
                int rightStart = rightEntityIndex + hasEntity;
                if (hasEntity != 0
                    && (!GeneratorSupport.IsEntityType(parameterTypes[leftEntityIndex])
                        || !GeneratorSupport.IsEntityType(parameterTypes[rightEntityIndex])
                        || CallbackReader.ParameterRefKind(parameters[leftEntityIndex]) != RefKind.None
                        || CallbackReader.ParameterRefKind(parameters[rightEntityIndex]) != RefKind.None))
                {
                    continue;
                }

                ITypeSymbol? contextType = null;
                ContextModeKind contextMode = ContextModeKind.Ref;
                if (hasContext != 0)
                {
                    contextType = parameterTypes[0];
                    contextMode = CallbackReader.ContextMode(CallbackReader.ParameterRefKind(parameters[0]));
                    if (contextType is null
                        || !GeneratorSupport.IsAccessibleSymbol(contextType)
                        || contextType.IsRefLikeType
                        || !SymbolEqualityComparer.Default.Equals(
                            contextType,
                            semanticModel.GetTypeInfo(arguments[selection.ContextIndex].Expression).Type)
                        || !CallbackReader.AreCompatibleContextModes(
                            CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(arguments[selection.ContextIndex])),
                            contextMode))
                    {
                        continue;
                    }

                }

                string[] componentTypes = new string[arity];
                var componentModes = new ContextModeKind[arity * 2];
                bool valid = true;
                for (int index = 0; index < arity; index++)
                {
                    ITypeSymbol leftType = parameterTypes[leftStart + index]!;
                    ITypeSymbol rightType = parameterTypes[rightStart + index]!;
                    RefKind leftMode = CallbackReader.ParameterRefKind(parameters[leftStart + index]);
                    RefKind rightMode = CallbackReader.ParameterRefKind(parameters[rightStart + index]);
                    if (!SymbolEqualityComparer.Default.Equals(leftType, rightType)
                        || !GeneratorSupport.IsAccessibleSymbol(leftType)
                        || !IsReadOnlyComponentParameter(leftMode)
                        || !IsReadOnlyComponentParameter(rightMode)
                        || explicitTypes.Length != 0
                            && !SymbolEqualityComparer.Default.Equals(leftType, explicitTypes[index]))
                    {
                        valid = false;
                        break;
                    }

                    componentTypes[index] = GeneratorSupport.DisplayType(leftType);
                    componentModes[index] = CallbackReader.ContextMode(leftMode);
                    componentModes[arity + index] = CallbackReader.ContextMode(rightMode);
                }

                if (!valid)
                {
                    continue;
                }

                var model = new ComponentComparerDelegateModel(
                    componentTypes,
                    componentModes,
                    hasEntity != 0,
                    hasContext != 0,
                    ContextTypeName(contextType),
                    contextMode,
                    contextType is ITypeParameterSymbol);
                matches.Add((model, registrationBinding, componentTypes));
            }
        }

        bool preferEntityForm = matches.Any(static match => match.Model.HasEntity);
        var selectedIndices = Enumerable.Range(0, matches.Count)
            .Where(index => !preferEntityForm || matches[index].Model.HasEntity)
            .ToArray();
        if (selectedIndices.Length != 1)
        {
            return false;
        }

        int selectedIndex = selectedIndices[0];
        var selected = matches[selectedIndex];
        string id = GeneratorSupport.StableName(
            $"{invocation.SyntaxTree.FilePath}|{invocation.SpanStart}|{selected.Model.Key}");
        string callbackBody = lambda.Body switch
        {
            ExpressionSyntax expression => "return " + expression + ";",
            BlockSyntax block => block.ToString(),
            _ => "return 0;"
        };
        site = new ComponentComparerDelegateSite(
            selected.Model,
            member.Name.Identifier.ValueText,
            selected.Binding,
            parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray(),
            selected.ComponentTypes,
            id,
            callbackBody,
            lambda.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)),
            semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            invocation.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(static directive => directive.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
            WhereSource: whereSource);
        return true;
    }

    private static bool HasEntitySignature(ParameterSyntax[] parameters, ITypeSymbol?[] parameterTypes)
        => Enumerable.Range(0, 2).Any(hasContext =>
        {
            int componentParameterCount = parameters.Length - hasContext - 2;
            if (componentParameterCount <= 0 || (componentParameterCount & 1) != 0)
            {
                return false;
            }

            int arity = componentParameterCount / 2;
            int leftStart = hasContext + 1;
            int rightEntityIndex = leftStart + arity;
            int rightStart = rightEntityIndex + 1;
            return IsEntityParameter(parameters, parameterTypes, hasContext)
                && IsEntityParameter(parameters, parameterTypes, rightEntityIndex)
                && Enumerable.Range(0, arity).All(index =>
                {
                    ITypeSymbol? leftType = parameterTypes[leftStart + index];
                    return leftType is not null
                        && SymbolEqualityComparer.Default.Equals(leftType, parameterTypes[rightStart + index])
                        && GeneratorSupport.IsAccessibleSymbol(leftType)
                        && IsReadOnlyComponentParameter(CallbackReader.ParameterRefKind(parameters[leftStart + index]))
                        && IsReadOnlyComponentParameter(CallbackReader.ParameterRefKind(parameters[rightStart + index]));
                });
        });

    private static bool IsEntityParameter(ParameterSyntax[] parameters, ITypeSymbol?[] parameterTypes, int index)
        => GeneratorSupport.IsEntityType(parameterTypes[index])
            && CallbackReader.ParameterRefKind(parameters[index]) == RefKind.None;

    private static string? ContextTypeName(ITypeSymbol? contextType)
        => contextType is null
            ? null
            : contextType is ITypeParameterSymbol ? "TContext" : GeneratorSupport.DisplayType(contextType);

    private static bool IsReadOnlyComponentParameter(RefKind kind)
        => kind is RefKind.None or RefKind.In || GeneratorSupport.IsRefReadonly(kind);

}
