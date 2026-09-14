namespace Delta.ECS.Generators;

/// <summary>Template for a generated query invoker.</summary>
internal static partial class GeneratorTemplates
{
    internal static RenderModel InvokerTemplate(InvokerModel model)
        => new(model.Api, model.Source ?? RenderInvoker(model));

    private static string RenderInvoker(InvokerModel model)
    {
        string declaration = $"internal struct {model.Name}{model.GenericParameters} : {model.Contract}";
        string fields = RenderLines(model.Fields) + "\n";
        string constructor = model.ConstructorParameters.IsDefault
            ? string.Empty
            : string.Join(
                "\n",
                $"    internal {model.Name}({string.Join(", ", model.ConstructorParameters)})",
                "    {",
                RenderLines(model.Assignments),
                RenderLines(model.AccessInitializers),
                "    }",
                string.Empty);
        string initializer = string.IsNullOrEmpty(model.InitializerBody)
            ? string.Empty
            : model.InitializerBody + "\n\n";
        string execute = string.Join(
            "\n",
            "    public void Execute(",
            "        ref GeneratedQuerySlots slots,",
            "        ref GeneratedWhereStructuralContext context)",
            "    {",
            model.ExecuteBody,
            "    }");
        return RenderBlock(declaration, fields + constructor + initializer + execute);
    }
}
