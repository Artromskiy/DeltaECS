namespace Delta.ECS.Generators;

using System.Collections.Immutable;

internal static class GenericTypeDispatchTemplates
{
    internal static string RenderComponentRegistrationExtension(int arity)
    {
        string parameters = GeneratorTemplates.JoinIndexed(arity, static index => $"global::Delta.ECS.ComponentId componentArgument{index}");
        string arguments = GeneratorTemplates.JoinIndexed(arity, static index => $"componentArgument{index}");
        return $$"""
            public static global::Delta.ECS.ComponentId Register(
                this global::Delta.ECS.ComponentLayoutRegistry layouts,
                global::System.Type genericTypeDefinition,
                {{parameters}},
                global::Delta.ECS.SchemaId schemaId)
            {
                return global::Delta.ECS.GeneratedGenericBindingRegistry.RegisterComponentDefinition(
                    layouts,
                    genericTypeDefinition,
                    schemaId,
                    stackalloc global::Delta.ECS.ComponentId[] { {{arguments}} });
            }
            """;
    }

    internal static string RenderComponentDispatcher(GenericComponentDispatcherBinding binding)
    {
        string[] typeParameters = binding.TypeParameters.Select(static parameter => "T" + parameter.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string componentTokenName = "ClosedComponentTypeToken";
        string closedComponentType = GenericType(binding.OpenTypeName, typeParameters);
        string tokenTypeArguments = "<" + string.Join(", ", typeParameters) + ">";
        string source = $$"""
            internal static class {{binding.DispatcherName}}
            {
                internal static global::Delta.ECS.ComponentId Register(
                    global::Delta.ECS.ComponentLayoutRegistry layouts,
                    global::Delta.ECS.SchemaId schemaId,
                    global::System.ReadOnlySpan<global::Delta.ECS.ComponentId> componentArguments,
                    global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> typeArguments)
                {
                    var state = new State(layouts, schemaId, componentArguments.ToArray());
                    var visitor = new Stage0(state);
                    {{DispatchCall(binding.TypeParameters[0].Kind, "typeArguments[0]", "typeArguments.Slice(1)", "visitor")}}
                    return visitor.Result;
                }

                private sealed class State
                {
                    internal readonly global::Delta.ECS.ComponentLayoutRegistry Layouts;
                    internal readonly global::Delta.ECS.SchemaId SchemaId;
                    internal readonly global::Delta.ECS.ComponentId[] ComponentArguments;

                    internal State(
                        global::Delta.ECS.ComponentLayoutRegistry layouts,
                        global::Delta.ECS.SchemaId schemaId,
                        global::Delta.ECS.ComponentId[] componentArguments)
                    {
                        Layouts = layouts;
                        SchemaId = schemaId;
                        ComponentArguments = componentArguments;
                    }
                }

                private sealed class {{componentTokenName}}{{tokenTypeArguments}} : global::Delta.ECS.IGeneratedComponentTypeToken{{TokenInterfaces(binding.IsValueType, binding.IsUnmanaged, binding.IsClass, binding.HasPublicParameterlessConstructor)}}
                {{ConstraintClauses(binding.TypeParameters, typeParameters)}}
                {
                    public global::System.Type ComponentType => typeof({{closedComponentType}});

            {{GeneratorTemplates.Indent(RenderTokenMethods(closedComponentType, binding.IsValueType, binding.IsUnmanaged, binding.IsClass, binding.HasPublicParameterlessConstructor), "    ")}}
                }

            {{GeneratorTemplates.Indent(RenderComponentStages(binding, typeParameters), "    ")}}
            }
            """;
        return source;
    }

    internal static string RenderFunctorDispatcher(GenericFunctorDispatcherBinding binding)
    {
        string[] typeParameters = binding.TypeParameters.Select(static parameter => "T" + parameter.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string source = $$"""
            internal static class {{binding.DispatcherName}}
            {
                internal static global::Delta.ECS.IGeneratedGenericFunctor Create(
                    global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> typeArguments)
                {
                    var visitor = new Stage0();
                    {{DispatchCall(binding.TypeParameters[0].Kind, "typeArguments[0]", "typeArguments.Slice(1)", "visitor")}}
                    return visitor.Result!;
                }

            {{GeneratorTemplates.Indent(RenderFunctorStages(binding, typeParameters), "    ")}}
            }
            """;
        return source;
    }

    private static string RenderComponentStages(GenericComponentDispatcherBinding binding, string[] typeParameters)
        => RenderStages(
            binding.TypeParameters,
            typeParameters,
            new[] { "internal readonly State Binding;", "internal global::Delta.ECS.ComponentId Result;" },
            index => $"internal Stage{index}(State binding) {{ Binding = binding; Result = default; }}",
            index =>
            {
                string closedType = GenericType(binding.OpenTypeName, typeParameters.Take(index + 1));
                string componentToken = $"new ClosedComponentTypeToken<{string.Join(", ", typeParameters.Take(index + 1))}>()";
                return $"global::Delta.ECS.GeneratedGenericBindingRegistry.RegisterComponent<{closedType}>(Binding.Layouts, Binding.SchemaId, Binding.ComponentArguments, {componentToken})";
            });

    private static string RenderFunctorStages(GenericFunctorDispatcherBinding binding, string[] typeParameters)
        => RenderStages(
            binding.TypeParameters,
            typeParameters,
            new[] { "internal global::Delta.ECS.IGeneratedGenericFunctor? Result;" },
            null,
            index => $"new {binding.ExecutorType}<{string.Join(", ", typeParameters.Take(index + 1))}>()");

    private static string RenderStages(
        ImmutableArray<GenericTypeParameterConstraint> constraints,
        string[] typeParameters,
        IEnumerable<string> members,
        Func<int, string>? constructor,
        Func<int, string> result)
    {
        var stages = new List<string>();
        for (int index = 0; index < constraints.Length; index++)
        {
            string priorTypeParameters = string.Join(", ", typeParameters.Take(index));
            string stageTypeArguments = priorTypeParameters.Length == 0 ? string.Empty : $"<{priorTypeParameters}>";
            string declaration = $"private struct Stage{index}{stageTypeArguments} : {VisitorInterface(constraints[index].Kind)}";
            string priorConstraints = ConstraintClauses(constraints.Take(index), typeParameters.Take(index));
            if (priorConstraints.Length != 0)
            {
                declaration += " " + priorConstraints;
            }
            var lines = new List<string>(members);
            if (constructor is not null)
            {
                lines.Add(constructor(index));
            }

            string visitBody;
            if (index + 1 == constraints.Length)
            {
                visitBody = $"Result = {result(index)};";
            }
            else
            {
                string closedParameters = string.Join(", ", typeParameters.Take(index + 1));
                string nextType = $"Stage{index + 1}<{closedParameters}>";
                visitBody = $$"""
                    var next = new {{nextType}}{{(constructor is null ? "()" : "(Binding)")}};
                    {{DispatchCall(constraints[index + 1].Kind, "remaining[0]", "remaining.Slice(1)", "next")}}
                    Result = next.Result;
                    """;
            }

            string visitConstraint = constraints[index].RenderClause(typeParameters[index]);
            lines.Add($"public void Visit<{typeParameters[index]}>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining){(visitConstraint.Length == 0 ? string.Empty : " " + visitConstraint)}\n{{\n    {GeneratorTemplates.Indent(visitBody, "    ")}\n}}");
            stages.Add($"{declaration}\n{{\n{GeneratorTemplates.Indent(string.Join("\n", lines), "    ")}\n}}");
        }

        return string.Join("\n\n", stages);
    }

    private static string GenericType(string openTypeName, IEnumerable<string> typeParameters)
    {
        int genericArgumentsStart = openTypeName.LastIndexOf('<');
        return openTypeName.Substring(0, genericArgumentsStart) + "<" + string.Join(", ", typeParameters) + ">";
    }

    internal static string RenderRegisteredComponentTypeToken(GeneratedTypeTokenBinding binding)
    {
        string interfaces = TokenInterfaces(binding.IsValueType, binding.IsUnmanaged, binding.IsClass, binding.HasPublicParameterlessConstructor);
        string methods = RenderTokenMethods(binding.ComponentTypeName, binding.IsValueType, binding.IsUnmanaged, binding.IsClass, binding.HasPublicParameterlessConstructor);
        return $$"""
            internal sealed class {{binding.TokenName}} : global::Delta.ECS.IGeneratedComponentTypeToken{{interfaces}}
            {
                public global::System.Type ComponentType => typeof({{binding.ComponentTypeName}});

            {{GeneratorTemplates.Indent(methods, "    ")}}
            }
            """;
    }

    private static string RenderTokenMethods(string componentType, bool isValueType, bool isUnmanaged, bool isClass, bool hasNew)
        => string.Join("\n", TokenKinds(isValueType, isUnmanaged, isClass, hasNew)
            .Select(kind =>
            {
                string method = kind == GenericTypeConstraintKind.None ? "Dispatch" : DispatchMethod(kind);
                string visitor = VisitorInterface(kind);
                return $"public void {method}<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, {visitor} => visitor.Visit<{componentType}>(remaining);";
            }));

    private static string TokenInterfaces(bool isValueType, bool isUnmanaged, bool isClass, bool hasNew)
    {
        string interfaces = string.Join(", ", TokenKinds(isValueType, isUnmanaged, isClass, hasNew)
            .Skip(1)
            .Select(static kind => VisitorInterface(kind).Replace("Visitor", "Token")));
        return interfaces.Length == 0 ? string.Empty : ", " + interfaces;
    }

    private static IEnumerable<GenericTypeConstraintKind> TokenKinds(
        bool isValueType,
        bool isUnmanaged,
        bool isClass,
        bool hasNew)
    {
        yield return GenericTypeConstraintKind.None;
        if (isValueType)
        {
            yield return GenericTypeConstraintKind.Struct;
        }

        if (isUnmanaged)
        {
            yield return GenericTypeConstraintKind.Unmanaged;
        }

        if (isClass)
        {
            yield return GenericTypeConstraintKind.Class;
            yield return GenericTypeConstraintKind.NullableClass;
        }

        if (hasNew)
        {
            yield return GenericTypeConstraintKind.New;
            if (isClass)
            {
                yield return GenericTypeConstraintKind.ClassNew;
                yield return GenericTypeConstraintKind.NullableClassNew;
            }
        }
    }

    private static string VisitorInterface(GenericTypeConstraintKind kind)
        => kind switch
        {
            GenericTypeConstraintKind.Struct => "global::Delta.ECS.IGeneratedStructComponentTypeVisitor",
            GenericTypeConstraintKind.Unmanaged => "global::Delta.ECS.IGeneratedUnmanagedComponentTypeVisitor",
            GenericTypeConstraintKind.Class => "global::Delta.ECS.IGeneratedClassComponentTypeVisitor",
            GenericTypeConstraintKind.NullableClass => "global::Delta.ECS.IGeneratedNullableClassComponentTypeVisitor",
            GenericTypeConstraintKind.New => "global::Delta.ECS.IGeneratedConstructibleComponentTypeVisitor",
            GenericTypeConstraintKind.ClassNew => "global::Delta.ECS.IGeneratedClassConstructibleComponentTypeVisitor",
            GenericTypeConstraintKind.NullableClassNew => "global::Delta.ECS.IGeneratedNullableClassConstructibleComponentTypeVisitor",
            _ => "global::Delta.ECS.IGeneratedComponentTypeVisitor",
        };

    private static string DispatchCall(GenericTypeConstraintKind kind, string token, string remaining, string visitor)
    {
        string method = DispatchMethod(kind);
        return method.Length == 0
            ? $"{token}.Dispatch({remaining}, ref {visitor});"
            : $"global::Delta.ECS.GeneratedComponentTypeDispatch.{method}({token}, {remaining}, ref {visitor});";
    }

    private static string DispatchMethod(GenericTypeConstraintKind kind)
        => kind switch
        {
            GenericTypeConstraintKind.Struct => "DispatchStruct",
            GenericTypeConstraintKind.Unmanaged => "DispatchUnmanaged",
            GenericTypeConstraintKind.Class => "DispatchClass",
            GenericTypeConstraintKind.NullableClass => "DispatchNullableClass",
            GenericTypeConstraintKind.New => "DispatchConstructible",
            GenericTypeConstraintKind.ClassNew => "DispatchClassConstructible",
            GenericTypeConstraintKind.NullableClassNew => "DispatchNullableClassConstructible",
            _ => string.Empty,
        };

    private static string ConstraintClauses(
        IEnumerable<GenericTypeParameterConstraint> constraints,
        IEnumerable<string> generatedTypeParameters)
        => string.Join(" ", constraints.Zip(generatedTypeParameters, static (constraint, typeParameter) => constraint.RenderClause(typeParameter)).Where(static clause => clause.Length != 0));
}

internal sealed record GenericComponentDispatcherBinding(
    string OpenTypeName,
    string DispatcherName,
    ImmutableArray<GenericTypeParameterConstraint> TypeParameters,
    bool IsValueType,
    bool IsUnmanaged,
    bool IsClass,
    bool HasPublicParameterlessConstructor)
{
    internal int Arity => TypeParameters.Length;
}

internal sealed record GenericFunctorDispatcherBinding(
    string OpenTypeName,
    string DispatcherName,
    string ExecutorType,
    ImmutableArray<GenericTypeParameterConstraint> TypeParameters)
{
    internal int Arity => TypeParameters.Length;
}
