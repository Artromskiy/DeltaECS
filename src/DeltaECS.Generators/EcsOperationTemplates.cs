namespace Delta.ECS.Generators;

internal static class EcsOperationTemplates
{
    internal static string InvokerStruct(
        string visibility,
        string name,
        string genericParameters,
        string interfaceType,
        string genericConstraints,
        string fields,
        string constructorParameters,
        string assignments,
        string methods)
        => $$"""
            [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
            {{visibility}} struct {{name}}{{genericParameters}} : {{interfaceType}}{{genericConstraints}}
            {
            {{fields}}

                internal {{name}}({{constructorParameters}})
                {
            {{GeneratorTemplates.Indent(assignments, "        ")}}
                }

            {{methods}}
            }
            """;

    internal static string OperationType(string invokerType, string? contextType, string? functorType)
        => "global::Delta.ECS.EcsOperation" + GenericArguments(contextType, functorType, invokerType);

    internal static string InvokerInterface(string? contextType, string? functorType) => "global::Delta.ECS.IEcsOperationInvoker" + GenericArguments(contextType, functorType);

    internal static string InvokeMethod(string interfaceType, string? contextType, string? functorType)
    {
        var parameters = new List<string>();
        if (contextType is not null)
        {
            parameters.Add($"ref {contextType} context");
        }

        if (functorType is not null)
        {
            parameters.Add($"ref {functorType} functor");
        }

        return $"void {interfaceType}.Invoke({string.Join(", ", parameters)})";
    }

    internal static string CreationExpression(
        string operationType,
        string invoker,
        string? contextType,
        string? functorType,
        string contextName = "context",
        string functorName = "functor")
    {
        var arguments = new List<string>();
        if (contextType is not null)
        {
            arguments.Add(contextName);
        }

        if (functorType is not null)
        {
            arguments.Add(functorName);
        }

        arguments.Add(invoker);
        return $"new {operationType}({string.Join(", ", arguments)})";
    }

    private static string GenericArguments(params string?[] types)
    {
        string arguments = string.Join(", ", types.Where(static type => type is not null));
        return arguments.Length == 0 ? string.Empty : $"<{arguments}>";
    }
}
