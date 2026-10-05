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
    {
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax invocation
                    && invocation.Expression is MemberAccessExpressionSyntax member
                    && member.Name.Identifier.ValueText is "OrderBy" or "ThenBy",
                static (syntax, _) => new InvocationCandidate((InvocationExpressionSyntax)syntax.Node))
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider
                .Combine(invocations)
                .Combine(context.AnalyzerConfigOptionsProvider.Select(
                    static (provider, _) => GeneratorSupport.IsInterceptionEnabled(provider.GlobalOptions))),
            static (productionContext, input) => Execute(
                input.Left.Left,
                input.Left.Right,
                input.Right,
                productionContext));
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<InvocationCandidate> invocations,
        bool interceptionEnabled,
        SourceProductionContext context)
    {
        var models = new Dictionary<string, ComparerModel>(StringComparer.Ordinal);
        var sites = new List<ComparerSite>();
        bool canIntercept = interceptionEnabled && GeneratorSupport.SupportsInterceptors(compilation);

        foreach (InvocationCandidate candidate in invocations)
        {
            InvocationExpressionSyntax invocation = candidate.Invocation;
            if (invocation.Expression is not MemberAccessExpressionSyntax member)
            {
                continue;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(invocation.SyntaxTree);
            if (!IsOrderedComparerCall(semanticModel, member)
                || !TryReadSite(semanticModel, invocation, member, out ComparerSite? site)
                || site is null)
            {
                continue;
            }

            if (!models.ContainsKey(site.Model.Key))
            {
                models.Add(site.Model.Key, site.Model);
            }

            site.Model = models[site.Model.Key];
            if (canIntercept
                && !site.Model.ContextIsGeneric
                && site.Lambda.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                && GeneratorSupport.TryGetInterceptionLocation(semanticModel, invocation, out string location, out string attribute))
            {
                site.InterceptionLocation = location;
                site.InterceptionAttribute = attribute;
                sites.Add(site);
            }
        }

        foreach (ComparerModel model in models.Values.OrderBy(static value => value.Key, StringComparer.Ordinal))
        {
            context.AddSource(
                "GeneratedComponentComparerDelegate_" + model.Hash + ".g.cs",
                ComparerTemplates.Render(model));
        }

        foreach (ComparerSite site in sites.OrderBy(static value => value.Id, StringComparer.Ordinal))
        {
            context.AddSource(
                "GeneratedComponentComparerInterceptor_" + site.Id + ".g.cs",
                ComparerTemplates.RenderInterceptor(site));
        }
    }

    private static bool IsOrderedComparerCall(SemanticModel model, MemberAccessExpressionSyntax member)
    {
        string methodName = member.Name.Identifier.ValueText;
        if (methodName == "OrderBy")
        {
            return IsNamedType(model.GetTypeInfo(member.Expression).Type, "Query");
        }

        return methodName == "ThenBy" && IsOrderedQueryExpression(member.Expression, model);
    }

    private static bool IsOrderedQueryExpression(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return IsNamedType(model.GetTypeInfo(expression).Type, "OrderedQuery");
        }

        string name = member.Name.Identifier.ValueText;
        if (name == "OrderBy")
        {
            return IsNamedType(model.GetTypeInfo(member.Expression).Type, "Query");
        }

        return name == "ThenBy" && IsOrderedQueryExpression(member.Expression, model);
    }

    private static bool IsNamedType(ITypeSymbol? type, string name)
        => type is INamedTypeSymbol named
            && named.Name == name
            && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.EcsNamespace;

    private static bool TryReadSite(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax member,
        out ComparerSite? site)
    {
        site = null;
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
        int prefixLength = arguments.Count - 1;
        var matches = new List<ComparerModel>();
        var matchingSelectors = new List<SelectorKind>();
        var matchingComponentTypes = new List<string[]>();
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

                int leftEntityIndex = hasContext;
                int leftStart = hasContext + hasEntity;
                int rightEntityIndex = leftStart + arity;
                int rightStart = rightEntityIndex + hasEntity;
                if (hasEntity != 0
                    && (!IsNamedType(parameterTypes[leftEntityIndex], "Entity")
                        || !IsNamedType(parameterTypes[rightEntityIndex], "Entity")
                        || CallbackReader.ParameterRefKind(parameters[leftEntityIndex]) != RefKind.None
                        || CallbackReader.ParameterRefKind(parameters[rightEntityIndex]) != RefKind.None))
                {
                    continue;
                }

                ITypeSymbol? contextType = null;
                ContextModeKind contextMode = ContextModeKind.Ref;
                if (hasContext != 0)
                {
                    if (prefixLength == 0)
                    {
                        continue;
                    }

                    contextType = parameterTypes[0];
                    contextMode = CallbackReader.ContextMode(CallbackReader.ParameterRefKind(parameters[0]));
                    if (contextType is null
                        || !GeneratorSupport.IsAccessibleSymbol(contextType)
                        || contextType.IsRefLikeType
                        || !SymbolEqualityComparer.Default.Equals(
                            contextType,
                            semanticModel.GetTypeInfo(arguments[prefixLength - 1].Expression).Type)
                        || !CallbackReader.AreCompatibleContextModes(
                            CallbackReader.ContextMode(CallbackReader.ArgumentRefKind(arguments[prefixLength - 1])),
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

                int selectorCount = prefixLength - hasContext;
                SelectorKind selectorKind;
                if (selectorCount == 0)
                {
                    selectorKind = SelectorKind.Primary;
                }
                else if (selectorCount == 1
                    && IsComponentIdSpan(semanticModel.GetTypeInfo(arguments[0].Expression).Type))
                {
                    selectorKind = SelectorKind.Span;
                }
                else if (selectorCount == arity
                    && Enumerable.Range(0, selectorCount).All(index => IsComponentId(
                        semanticModel.GetTypeInfo(arguments[index].Expression).Type)))
                {
                    selectorKind = SelectorKind.Positional;
                }
                else
                {
                    continue;
                }

                if (!valid)
                {
                    continue;
                }

                var model = new ComparerModel(
                    componentTypes,
                    componentModes,
                    hasEntity != 0,
                    hasContext != 0,
                    ContextTypeName(contextType),
                    contextMode,
                    contextType is ITypeParameterSymbol);
                matches.Add(model);
                matchingSelectors.Add(selectorKind);
                matchingComponentTypes.Add(componentTypes);
            }
        }

        bool preferEntityForm = matches.Any(static candidate => candidate.HasEntity);
        var selectedIndices = Enumerable.Range(0, matches.Count)
            .Where(index => !preferEntityForm || matches[index].HasEntity)
            .ToArray();
        if (selectedIndices.Length != 1)
        {
            return false;
        }

        int selectedIndex = selectedIndices[0];
        ComparerModel selected = matches[selectedIndex];
        string id = GeneratorSupport.StableName(
            invocation.SyntaxTree.FilePath + "|" + invocation.SpanStart + "|" + selected.Key);
        site = new ComparerSite(
            selected,
            lambda,
            invocation,
            member.Name.Identifier.ValueText,
            matchingSelectors[selectedIndex],
            parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray(),
            matchingComponentTypes[selectedIndex],
            id,
            semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            invocation.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(static directive => directive.ToString()).Distinct(StringComparer.Ordinal).ToArray());
        return true;
    }

    private static bool HasEntitySignature(ParameterSyntax[] parameters, ITypeSymbol?[] parameterTypes)
    {
        for (int hasContext = 0; hasContext <= 1; hasContext++)
        {
            int componentParameterCount = parameters.Length - hasContext - 2;
            if (componentParameterCount <= 0 || (componentParameterCount & 1) != 0)
            {
                continue;
            }

            int arity = componentParameterCount / 2;
            int leftEntityIndex = hasContext;
            int leftStart = leftEntityIndex + 1;
            int rightEntityIndex = leftStart + arity;
            int rightStart = rightEntityIndex + 1;
            if (!IsEntityParameter(parameters, parameterTypes, leftEntityIndex)
                || !IsEntityParameter(parameters, parameterTypes, rightEntityIndex))
            {
                continue;
            }

            bool validComponents = true;
            for (int index = 0; index < arity; index++)
            {
                ITypeSymbol? leftType = parameterTypes[leftStart + index];
                ITypeSymbol? rightType = parameterTypes[rightStart + index];
                if (leftType is null
                    || !SymbolEqualityComparer.Default.Equals(leftType, rightType)
                    || !GeneratorSupport.IsAccessibleSymbol(leftType)
                    || !IsReadOnlyComponentParameter(CallbackReader.ParameterRefKind(parameters[leftStart + index]))
                    || !IsReadOnlyComponentParameter(CallbackReader.ParameterRefKind(parameters[rightStart + index])))
                {
                    validComponents = false;
                    break;
                }
            }

            if (validComponents)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEntityParameter(ParameterSyntax[] parameters, ITypeSymbol?[] parameterTypes, int index)
        => IsNamedType(parameterTypes[index], "Entity")
            && CallbackReader.ParameterRefKind(parameters[index]) == RefKind.None;

    private static string? ContextTypeName(ITypeSymbol? contextType)
        => contextType is null
            ? null
            : contextType is ITypeParameterSymbol ? "TContext" : GeneratorSupport.DisplayType(contextType);

    private static bool IsReadOnlyComponentParameter(RefKind kind)
        => kind is RefKind.None or RefKind.In || GeneratorSupport.IsRefReadonly(kind);

    private static bool IsComponentId(ITypeSymbol? type)
        => IsNamedType(type, "ComponentId");

    private static bool IsComponentIdSpan(ITypeSymbol? type)
        => type is INamedTypeSymbol named
            && named.Name is "ReadOnlySpan" or "Span"
            && named.ContainingNamespace.ToDisplayString() == GeneratorSupport.SystemNamespace
            && named.TypeArguments.Length == 1
            && IsComponentId(named.TypeArguments[0]);

    private enum SelectorKind
    {
        Primary,
        Positional,
        Span,
    }

    private sealed class InvocationCandidate
    {
        internal InvocationCandidate(InvocationExpressionSyntax invocation) => Invocation = invocation;
        internal InvocationExpressionSyntax Invocation { get; }
    }

    private sealed class ComparerModel
    {
        internal ComparerModel(
            string[] componentTypes,
            ContextModeKind[] componentModes,
            bool hasEntity,
            bool hasContext,
            string? contextType,
            ContextModeKind contextMode,
            bool contextIsGeneric)
        {
            ComponentTypes = componentTypes;
            ComponentModes = componentModes;
            HasEntity = hasEntity;
            HasContext = hasContext;
            ContextType = contextType;
            ContextMode = contextMode;
            ContextIsGeneric = contextIsGeneric;
            Key = componentTypes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + string.Join("", componentModes.Select(ModeCode))
                + "|" + hasEntity + "|" + hasContext + "|" + contextType + "|" + contextMode;
            Hash = GeneratorSupport.StableName(Key);
        }

        internal string[] ComponentTypes { get; }
        internal ContextModeKind[] ComponentModes { get; }
        internal bool HasEntity { get; }
        internal bool HasContext { get; }
        internal string? ContextType { get; }
        internal ContextModeKind ContextMode { get; }
        internal bool ContextIsGeneric { get; }
        internal string Key { get; }
        internal string Hash { get; }

        private static char ModeCode(ContextModeKind mode) => mode switch
        {
            ContextModeKind.In => 'I',
            ContextModeKind.RefReadonly => 'R',
            ContextModeKind.Value => 'V',
            ContextModeKind.Ref => 'W',
            _ => 'I'
        };
    }

    private sealed class ComparerSite
    {
        internal ComparerSite(
            ComparerModel model,
            LambdaExpressionSyntax lambda,
            InvocationExpressionSyntax invocation,
            string methodName,
            SelectorKind selectorKind,
            string[] parameterNames,
            string[] componentTypes,
            string id,
            string namespaceName,
            string[] usings)
        {
            Model = model;
            Lambda = lambda;
            Invocation = invocation;
            MethodName = methodName;
            SelectorKind = selectorKind;
            ParameterNames = parameterNames;
            ComponentTypes = componentTypes;
            Id = id;
            NamespaceName = namespaceName;
            Usings = usings;
        }

        internal ComparerModel Model { get; set; }
        internal LambdaExpressionSyntax Lambda { get; }
        internal InvocationExpressionSyntax Invocation { get; }
        internal string MethodName { get; }
        internal SelectorKind SelectorKind { get; }
        internal string[] ParameterNames { get; }
        internal string[] ComponentTypes { get; }
        internal string Id { get; }
        internal string? InterceptionLocation { get; set; }
        internal string? InterceptionAttribute { get; set; }
        internal string NamespaceName { get; }
        internal string[] Usings { get; }
    }

    private static class ComparerTemplates
    {
        internal static string Render(ComparerModel model)
        {
            string genericNames = GenericParameterNames(model);
            string genericArguments = GenericArguments(genericNames);
            string delegateName = "Compare" + genericArguments;
            string[] genericTypes = Enumerable.Range(0, model.ComponentTypes.Length)
                .Select(index => "T" + (index + 1)).ToArray();
            string delegateParameters = CallbackParameters(model, genericTypes, DefaultParameterNames(model));
            string invokeParameters = CallbackParameters(model, genericTypes, DefaultParameterNames(model));
            string invokeArguments = ComparisonArguments(model, "leftEntity", "rightEntity", "left", "right", "_context");
            string idFields = Join(model.ComponentTypes.Length, index =>
                $"private readonly global::Delta.ECS.ComponentId _componentId{index};");
            string idAssignments = Join(model.ComponentTypes.Length, index =>
                $"_componentId{index} = componentIds[{index}];");
            string validation = Join(model.ComponentTypes.Length, index =>
                $"world.ValidateGeneratedOrderedQueryKey<T{index + 1}>(in query, _componentId{index});");
            string compareRows = GeneratorTemplates.JoinNonEmpty(new[]
            {
                Join(model.ComponentTypes.Length, index =>
                    $"ref readonly T{index + 1} left{index} = ref world.GetGeneratedOrderedQueryKey<T{index + 1}>(left, _componentId{index});"),
                Join(model.ComponentTypes.Length, index =>
                    $"ref readonly T{index + 1} right{index} = ref world.GetGeneratedOrderedQueryKey<T{index + 1}>(right, _componentId{index});"),
            });
            string contextField = model.HasContext ? $"private {model.ContextType} _context;" : string.Empty;
            string contextParameter = model.HasContext
                ? ", " + SignatureProjection.ContextParameter(model.ContextMode, model.ContextType!, "context")
                : string.Empty;
            string contextArgument = model.HasContext
                ? ", " + SignatureProjection.ContextArgument(model.ContextMode, "context")
                : string.Empty;
            string callbackField = "private readonly " + delegateName + " _callback;";
            string callbackCall = "_callback(" + invokeArguments.Replace("_context", "context") + ")";
            string selectorParameters = PositionalSelectorParameters(model.ComponentTypes.Length);
            string selectorValues = PositionalSelectorValues(model.ComponentTypes.Length);
            string source = $$"""
                // <auto-generated />
                #nullable enable

                namespace Delta.ECS;

                internal static class GeneratedComponentComparerDelegateExtensions_{{model.Hash}}
                {
                    internal delegate int {{delegateName}}({{delegateParameters}});

                    internal abstract class AdapterBase<{{genericNames}}> : global::Delta.ECS.IGeneratedComponentComparer
                    {
                        {{idFields}}
                        {{contextField}}

                        protected AdapterBase(global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextParameter}})
                        {
                            global::Delta.ECS.GeneratedForEachRuntime.ValidateComponentIdCount(componentIds, {{model.ComponentTypes.Length}});
                {{GeneratorTemplates.Indent(idAssignments, "            ")}}
                            {{(model.HasContext ? "_context = context;" : string.Empty)}}
                        }

                        public void Validate(global::Delta.ECS.World world, in global::Delta.ECS.Query query)
                        {
                {{GeneratorTemplates.Indent(validation, "            ")}}
                        }

                        public int Compare(global::Delta.ECS.World world, global::Delta.ECS.Entity left, global::Delta.ECS.Entity right)
                        {
                {{GeneratorTemplates.Indent(compareRows, "            ")}}
                            {{(model.HasEntity ? "global::Delta.ECS.Entity leftEntity = left;\n            global::Delta.ECS.Entity rightEntity = right;" : string.Empty)}}
                            return Invoke({{invokeArguments}});
                        }

                        protected abstract int Invoke({{invokeParameters}});
                    }

                    private sealed class DelegateAdapter<{{genericNames}}> : AdapterBase<{{genericNames}}>
                    {
                        {{callbackField}}

                        internal DelegateAdapter(
                            global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextParameter}},
                            {{delegateName}} callback)
                            : base(componentIds{{contextArgument}})
                        {
                            global::Delta.ECS.GeneratedForEachRuntime.ThrowIfNull(callback, nameof(callback));
                            _callback = callback;
                        }

                        protected override int Invoke({{invokeParameters}})
                            => {{callbackCall}};
                    }

                {{RenderExtensions(model, delegateName, genericNames, selectorParameters, selectorValues)}}
                }
                """;

            return GeneratedSourceFormatter.Format(source);
        }

        internal static string RenderInterceptor(ComparerSite site)
        {
            ComparerModel model = site.Model;
            string genericNames = string.Join(", ", site.ComponentTypes);
            string genericArguments = GenericArguments(genericNames);
            string extensionClass = "global::Delta.ECS.GeneratedComponentComparerDelegateExtensions_" + model.Hash;
            string delegateType = extensionClass + ".Compare" + genericArguments;
            string adapterType = extensionClass + ".AdapterBase<" + genericNames + ">";
            string contextParameter = model.HasContext
                ? ", " + SignatureProjection.ContextParameter(model.ContextMode, model.ContextType!, "context")
                : string.Empty;
            string contextArgument = model.HasContext
                ? ", " + SignatureProjection.ContextArgument(model.ContextMode, "context")
                : string.Empty;
            string selectorParameters = SelectorParameters(site.SelectorKind, model.ComponentTypes.Length);
            string selectorNames = SelectorArgumentNames(site.SelectorKind, model.ComponentTypes.Length);
            string queryType = site.MethodName == "OrderBy" ? "global::Delta.ECS.Query" : "global::Delta.ECS.OrderedQuery";
            string sourceQuery = site.MethodName == "OrderBy" ? "query" : "sourceQuery";
            string sourceQueryDeclaration = site.MethodName == "OrderBy"
                ? string.Empty
                : "global::Delta.ECS.Query sourceQuery = query.SourceQuery;";
            string idsExpression = site.SelectorKind switch
            {
                SelectorKind.Primary => $"global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<{delegateType}>(in {sourceQuery}, static world => new global::Delta.ECS.ComponentId[] {{ {PrimaryIds(site.ComponentTypes, "world")} }})",
                SelectorKind.Positional => $"stackalloc global::Delta.ECS.ComponentId[] {{ {selectorNames} }}",
                _ => "componentIds"
            };
            string callbackType = $"{delegateType} _";
            string genericClause = string.Empty;
            string directParameters = CallbackParameters(model, site.ComponentTypes, site.ParameterNames);
            string callbackBody = site.Lambda.Body switch
            {
                ExpressionSyntax expression => "return " + expression + ";",
                BlockSyntax block => block.ToString(),
                _ => "return 0;"
            };
            string directInvoke = "protected override int Invoke(" + directParameters + ")";
            string usings = string.Join("\n", site.Usings
                .Where(static value => value.Trim() is not ("using Delta.ECS;" or "using global::Delta.ECS;"))
                .Concat(string.IsNullOrEmpty(site.NamespaceName) ? Array.Empty<string>() : new[] { "using global::" + site.NamespaceName + ";" })
                .Distinct(StringComparer.Ordinal));
            string directAdapter = "new DirectAdapter(resolvedComponentIds" + contextArgument + ")";
            string returnExpression = site.MethodName == "OrderBy"
                ? $"global::Delta.ECS.OrderedQuery.CreateGenerated(query, {directAdapter})"
                : $"query.AppendGenerated({directAdapter})";
            string invocationParameterList = string.Join(", ", new[]
            {
                "this " + queryType + " query",
                selectorParameters,
                contextParameter.Length == 0 ? string.Empty : contextParameter.Substring(2),
                callbackType,
            }.Where(static value => value.Length != 0));

            string source = $$"""
                // <auto-generated />
                #nullable enable
                {{usings}}

                namespace Delta.ECS.Generated
                {
                    file static class ComponentComparerInterceptor_{{site.Id}}
                    {
                        private sealed class DirectAdapter : {{adapterType}}
                        {
                            internal DirectAdapter(global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextParameter}})
                                : base(componentIds{{contextArgument}}) { }

                            {{directInvoke}}
                            {
                {{GeneratorTemplates.Indent(callbackBody, "                ")}}
                            }
                        }

                        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                        {{site.InterceptionAttribute}}
                        internal static global::Delta.ECS.OrderedQuery Intercept{{genericClause}}({{invocationParameterList}})
                        {
                            {{sourceQueryDeclaration}}
                            global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> resolvedComponentIds = {{idsExpression}};
                            return {{returnExpression}};
                        }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    file sealed class InterceptsLocationAttribute : global::System.Attribute
                    {
                        internal InterceptsLocationAttribute(int version, string data) { }
                    }
                }
                """;

            return GeneratedSourceFormatter.Format(source);
        }

        private static string RenderExtensions(
            ComparerModel model,
            string delegateName,
            string genericNames,
            string positionalParameters,
            string positionalValues)
        {
            string genericClause = GenericArguments(genericNames);
            string delegateParameter = delegateName + " callback";
            string contextParameter = model.HasContext
                ? ", " + SignatureProjection.ContextParameter(model.ContextMode, model.ContextType!, "context")
                : string.Empty;
            string contextArgument = model.HasContext
                ? ", " + SignatureProjection.ContextArgument(model.ContextMode, "context")
                : string.Empty;
            string adapter = "DelegateAdapter<" + genericNames + ">";
            string createOrder = "global::Delta.ECS.OrderedQuery.CreateGenerated(query, new " + adapter + "(componentIds" + contextArgument + ", callback))";
            string append = "query.AppendGenerated(new " + adapter + "(componentIds" + contextArgument + ", callback))";
            string queryIds = PrimaryIds(model, "world");
            string thenIds = PrimaryIds(model, "world");
            string primaryOrder = $"global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<{delegateName}>(in query, static world => new global::Delta.ECS.ComponentId[] {{ {queryIds} }})";
            string primaryThen = $"global::Delta.ECS.GeneratedForEachRuntime.GetGeneratedPrimaryComponentIds<{delegateName}>(in sourceQuery, static world => new global::Delta.ECS.ComponentId[] {{ {thenIds} }})";

            return $$"""
                    internal static global::Delta.ECS.OrderedQuery OrderBy{{genericClause}}(this global::Delta.ECS.Query query{{contextParameter}}, {{delegateParameter}})
                    {
                        global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds = {{primaryOrder}};
                        return {{createOrder}};
                    }

                    internal static global::Delta.ECS.OrderedQuery OrderBy{{genericClause}}(this global::Delta.ECS.Query query, {{positionalParameters}}{{contextParameter}}, {{delegateParameter}})
                    {
                        global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds = stackalloc global::Delta.ECS.ComponentId[] { {{positionalValues}} };
                        return {{createOrder}};
                    }

                    internal static global::Delta.ECS.OrderedQuery OrderBy{{genericClause}}(this global::Delta.ECS.Query query, global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextParameter}}, {{delegateParameter}})
                        => {{createOrder}};

                    internal static global::Delta.ECS.OrderedQuery ThenBy{{genericClause}}(this global::Delta.ECS.OrderedQuery query{{contextParameter}}, {{delegateParameter}})
                    {
                        global::Delta.ECS.Query sourceQuery = query.SourceQuery;
                        global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds = {{primaryThen}};
                        return {{append}};
                    }

                    internal static global::Delta.ECS.OrderedQuery ThenBy{{genericClause}}(this global::Delta.ECS.OrderedQuery query, {{positionalParameters}}{{contextParameter}}, {{delegateParameter}})
                    {
                        global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds = stackalloc global::Delta.ECS.ComponentId[] { {{positionalValues}} };
                        return {{append}};
                    }

                    internal static global::Delta.ECS.OrderedQuery ThenBy{{genericClause}}(this global::Delta.ECS.OrderedQuery query, global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds{{contextParameter}}, {{delegateParameter}})
                        => {{append}};
                """;
        }

        private static string SelectorParameters(SelectorKind kind, int arity)
            => kind switch
            {
                SelectorKind.Positional => PositionalSelectorParameters(arity),
                SelectorKind.Span => "global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentIds",
                _ => string.Empty
            };

        private static string SelectorArgumentNames(SelectorKind kind, int arity)
            => kind switch
            {
                SelectorKind.Positional => PositionalSelectorValues(arity),
                SelectorKind.Span => "componentIds",
                _ => string.Empty
            };

        private static string PrimaryIds(ComparerModel model, string world)
            => string.Join(", ", Enumerable.Range(0, model.ComponentTypes.Length)
                .Select(index => $"{world}.Layouts.GetPrimary(typeof(T{index + 1}))"));

        private static string PrimaryIds(string[] componentTypes, string world)
            => string.Join(", ", componentTypes
                .Select(type => $"{world}.Layouts.GetPrimary(typeof({type}))"));

        private static string PositionalSelectorParameters(int arity)
            => string.Join(", ", Enumerable.Range(0, arity)
                .Select(index => $"global::Delta.ECS.ComponentId componentId{index}"));

        private static string PositionalSelectorValues(int arity)
            => string.Join(", ", Enumerable.Range(0, arity).Select(index => $"componentId{index}"));

        private static string CallbackParameters(ComparerModel model, string[] genericTypes, string[] parameterNames)
        {
            var parameters = new List<string>();
            int nameIndex = 0;
            if (model.HasContext)
            {
                parameters.Add(SignatureProjection.ContextParameter(model.ContextMode, model.ContextType!, parameterNames[nameIndex++]));
            }

            if (model.HasEntity)
            {
                parameters.Add("global::Delta.ECS.Entity " + parameterNames[nameIndex++]);
            }

            for (int index = 0; index < model.ComponentTypes.Length; index++)
            {
                parameters.Add(ComponentParameter(model, index, genericTypes[index], parameterNames[nameIndex++]));
            }

            if (model.HasEntity)
            {
                parameters.Add("global::Delta.ECS.Entity " + parameterNames[nameIndex++]);
            }

            for (int index = 0; index < model.ComponentTypes.Length; index++)
            {
                parameters.Add(ComponentParameter(model, model.ComponentTypes.Length + index, genericTypes[index], parameterNames[nameIndex++]));
            }

            return string.Join(", ", parameters);
        }

        private static string ComponentParameter(ComparerModel model, int index, string type, string name)
        {
            string modifier = model.ComponentModes[index] switch
            {
                ContextModeKind.RefReadonly => "ref readonly ",
                ContextModeKind.Value => string.Empty,
                _ => "in "
            };
            return modifier + type + " " + name;
        }

        private static string[] DefaultParameterNames(ComparerModel model)
        {
            var names = new List<string>();
            if (model.HasContext)
            {
                names.Add("context");
            }

            if (model.HasEntity)
            {
                names.Add("leftEntity");
            }

            names.AddRange(Enumerable.Range(0, model.ComponentTypes.Length).Select(index => "left" + index));
            if (model.HasEntity)
            {
                names.Add("rightEntity");
            }

            names.AddRange(Enumerable.Range(0, model.ComponentTypes.Length).Select(index => "right" + index));
            return names.ToArray();
        }

        private static string ComparisonArguments(
            ComparerModel model,
            string leftEntity,
            string rightEntity,
            string leftPrefix,
            string rightPrefix,
            string contextName)
        {
            var arguments = new List<string>();
            if (model.HasContext)
            {
                arguments.Add(SignatureProjection.ContextArgument(model.ContextMode, contextName));
            }

            if (model.HasEntity)
            {
                arguments.Add(leftEntity);
            }

            arguments.AddRange(Enumerable.Range(0, model.ComponentTypes.Length)
                .Select(index => ComponentArgument(model, index, leftPrefix + index)));
            if (model.HasEntity)
            {
                arguments.Add(rightEntity);
            }

            arguments.AddRange(Enumerable.Range(0, model.ComponentTypes.Length)
                .Select(index => ComponentArgument(model, model.ComponentTypes.Length + index, rightPrefix + index)));
            return string.Join(", ", arguments);
        }

        private static string ComponentArgument(ComparerModel model, int index, string name)
            => model.ComponentModes[index] switch
            {
                ContextModeKind.In or ContextModeKind.RefReadonly => "in " + name,
                _ => name
            };

        private static string GenericParameterNames(ComparerModel model)
            => model.ContextIsGeneric
                ? GenericNames(model.ComponentTypes.Length) + ", TContext"
                : GenericNames(model.ComponentTypes.Length);

        private static string GenericNames(int arity)
            => string.Join(", ", Enumerable.Range(0, arity).Select(static index => "T" + (index + 1)));

        private static string GenericArguments(string names)
            => "<" + names + ">";

        private static string Join(int count, Func<int, string> render)
            => string.Join("\n", Enumerable.Range(0, count).Select(render));
    }
}
