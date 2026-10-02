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
                global::Delta.ECS.SchemaId schemaId,
                {{parameters}})
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
    {
        var stages = new List<string>();
        for (int index = 0; index < binding.TypeParameters.Length; index++)
        {
            string priorTypeParameters = string.Join(", ", typeParameters.Take(index));
            string stageTypeArguments = priorTypeParameters.Length == 0 ? string.Empty : $"<{priorTypeParameters}>";
            string declaration = $"private struct Stage{index}{stageTypeArguments} : {VisitorInterface(binding.TypeParameters[index].Kind)}";
            string priorConstraints = ConstraintClauses(binding.TypeParameters.Take(index), typeParameters.Take(index));
            if (priorConstraints.Length != 0)
            {
                declaration += " " + priorConstraints;
            }
            var lines = new List<string> { "internal readonly State Binding;", "internal global::Delta.ECS.ComponentId Result;" };
            lines.Add($"internal Stage{index}(State binding) => Binding = binding;");

            string visitBody;
            if (index + 1 == binding.Arity)
            {
                string closedType = GenericType(binding.OpenTypeName, typeParameters.Take(index).Append(typeParameters[index]));
                string componentToken = $"new ClosedComponentTypeToken<{string.Join(", ", typeParameters.Take(index).Append(typeParameters[index]))}>()";
                visitBody = $"Result = global::Delta.ECS.GeneratedGenericBindingRegistry.RegisterComponent<{closedType}>(Binding.Layouts, Binding.SchemaId, Binding.ComponentArguments, {componentToken});";
            }
            else
            {
                string closedParameters = string.Join(", ", typeParameters.Take(index).Append(typeParameters[index]));
                string nextType = $"Stage{index + 1}<{closedParameters}>";
                visitBody = $$"""
                    var next = new {{nextType}}(Binding);
                    {{DispatchCall(binding.TypeParameters[index + 1].Kind, "remaining[0]", "remaining.Slice(1)", "next")}}
                    Result = next.Result;
                    """;
            }

            string visitConstraint = binding.TypeParameters[index].RenderClause(typeParameters[index]);
            lines.Add($"public void Visit<{typeParameters[index]}>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining){(visitConstraint.Length == 0 ? string.Empty : " " + visitConstraint)}\n{{\n    {GeneratorTemplates.Indent(visitBody, "    ")}\n}}");
            stages.Add($"{declaration}\n{{\n{GeneratorTemplates.Indent(string.Join("\n", lines), "    ")}\n}}");
        }

        return string.Join("\n\n", stages);
    }

    private static string RenderFunctorStages(GenericFunctorDispatcherBinding binding, string[] typeParameters)
    {
        var stages = new List<string>();
        for (int index = 0; index < binding.Arity; index++)
        {
            string priorTypeParameters = string.Join(", ", typeParameters.Take(index));
            string stageTypeArguments = priorTypeParameters.Length == 0 ? string.Empty : $"<{priorTypeParameters}>";
            string declaration = $"private struct Stage{index}{stageTypeArguments} : {VisitorInterface(binding.TypeParameters[index].Kind)}";
            string priorConstraints = ConstraintClauses(binding.TypeParameters.Take(index), typeParameters.Take(index));
            if (priorConstraints.Length != 0)
            {
                declaration += " " + priorConstraints;
            }
            var lines = new List<string> { "internal global::Delta.ECS.IGeneratedGenericFunctor? Result;" };

            string visitBody;
            if (index + 1 == binding.Arity)
            {
                string closedParameters = string.Join(", ", typeParameters.Take(index).Append(typeParameters[index]));
                visitBody = $"Result = new {binding.ExecutorType}<{closedParameters}>();";
            }
            else
            {
                string closedParameters = string.Join(", ", typeParameters.Take(index).Append(typeParameters[index]));
                string nextType = $"Stage{index + 1}<{closedParameters}>";
                visitBody = $$"""
                    var next = new {{nextType}}();
                    {{DispatchCall(binding.TypeParameters[index + 1].Kind, "remaining[0]", "remaining.Slice(1)", "next")}}
                    Result = next.Result;
                    """;
            }

            string visitConstraint = binding.TypeParameters[index].RenderClause(typeParameters[index]);
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
    {
        var methods = new List<string>
        {
            $"public void Dispatch<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);",
        };
        if (isValueType)
        {
            methods.Add($"public void DispatchStruct<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedStructComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
        }

        if (isUnmanaged)
        {
            methods.Add($"public void DispatchUnmanaged<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedUnmanagedComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
        }

        if (isClass)
        {
            methods.Add($"public void DispatchClass<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedClassComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
            methods.Add($"public void DispatchNullableClass<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedNullableClassComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
        }

        if (hasNew)
        {
            methods.Add($"public void DispatchConstructible<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedConstructibleComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
            if (isClass)
            {
                methods.Add($"public void DispatchClassConstructible<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedClassConstructibleComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
                methods.Add($"public void DispatchNullableClassConstructible<TVisitor>(global::System.ReadOnlySpan<global::Delta.ECS.IGeneratedComponentTypeToken> remaining, ref TVisitor visitor) where TVisitor : struct, global::Delta.ECS.IGeneratedNullableClassConstructibleComponentTypeVisitor => visitor.Visit<{componentType}>(remaining);");
            }
        }

        return string.Join("\n", methods);
    }

    private static string TokenInterfaces(bool isValueType, bool isUnmanaged, bool isClass, bool hasNew)
    {
        var interfaces = new List<string>();
        if (isValueType)
        {
            interfaces.Add("global::Delta.ECS.IGeneratedStructComponentTypeToken");
        }

        if (isUnmanaged)
        {
            interfaces.Add("global::Delta.ECS.IGeneratedUnmanagedComponentTypeToken");
        }
        if (isClass)
        {
            interfaces.Add("global::Delta.ECS.IGeneratedClassComponentTypeToken");
            interfaces.Add("global::Delta.ECS.IGeneratedNullableClassComponentTypeToken");
        }

        if (hasNew)
        {
            interfaces.Add("global::Delta.ECS.IGeneratedConstructibleComponentTypeToken");
            if (isClass)
            {
                interfaces.Add("global::Delta.ECS.IGeneratedClassConstructibleComponentTypeToken");
                interfaces.Add("global::Delta.ECS.IGeneratedNullableClassConstructibleComponentTypeToken");
            }
        }

        return interfaces.Count == 0 ? string.Empty : ", " + string.Join(", ", interfaces);
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
        string method = kind switch
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
        return method.Length == 0
            ? $"{token}.Dispatch({remaining}, ref {visitor});"
            : $"global::Delta.ECS.GeneratedComponentTypeDispatch.{method}({token}, {remaining}, ref {visitor});";
    }

    private static string ConstraintClauses(
        IEnumerable<GenericTypeParameterConstraint> constraints,
        IEnumerable<string> generatedTypeParameters)
        => string.Join(" ", constraints.Zip(generatedTypeParameters, static (constraint, typeParameter) => constraint.RenderClause(typeParameter)).Where(static clause => clause.Length != 0));
}

internal sealed class GenericComponentDispatcherBinding(
    string openTypeName,
    string dispatcherName,
    ImmutableArray<GenericTypeParameterConstraint> typeParameters,
    bool isValueType,
    bool isUnmanaged,
    bool isClass,
    bool hasPublicParameterlessConstructor)
{
    internal string OpenTypeName { get; } = openTypeName;
    internal string DispatcherName { get; } = dispatcherName;
    internal ImmutableArray<GenericTypeParameterConstraint> TypeParameters { get; } = typeParameters;
    internal int Arity => TypeParameters.Length;
    internal bool IsValueType { get; } = isValueType;
    internal bool IsUnmanaged { get; } = isUnmanaged;
    internal bool IsClass { get; } = isClass;
    internal bool HasPublicParameterlessConstructor { get; } = hasPublicParameterlessConstructor;
}

internal sealed class GenericFunctorDispatcherBinding(
    string openTypeName,
    string dispatcherName,
    string executorType,
    ImmutableArray<GenericTypeParameterConstraint> typeParameters)
{
    internal string OpenTypeName { get; } = openTypeName;
    internal string DispatcherName { get; } = dispatcherName;
    internal string ExecutorType { get; } = executorType;
    internal ImmutableArray<GenericTypeParameterConstraint> TypeParameters { get; } = typeParameters;
    internal int Arity => TypeParameters.Length;
}
