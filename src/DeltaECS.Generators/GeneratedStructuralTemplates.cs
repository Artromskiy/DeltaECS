using System.Collections.Immutable;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for validated structural API shapes.</summary>
internal static class GeneratedStructuralTemplates
{
    internal static string Render(StructuralModel shape)
    {
        SignatureProjection slots = shape.Api.Signature;
        string hash = GeneratorSupport.StableName(shape.Key);
        string parameters = GeneratorTemplates.JoinNonEmpty(
            new[] { "this World target" }
                .Concat(ParameterFragments(shape, slots)),
            ", ");
        string body = RenderBody(shape, slots);

        string generic = slots.HasGenericSelectors
            ? slots.GenericParameters()
            : string.Empty;
        string declaration = $$"""
            public static {{ReturnType(shape)}} {{MethodName(shape)}}{{generic}}({{parameters}})
            """.Trim();
        string method = GeneratorTemplates.Method(
            shape.Api,
            declaration,
            body,
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        string extensionMembers = GeneratorTemplates.JoinNonEmpty(new[]
        {
            method,
            shape.HasValues
                ? GeneratorTemplates.ValueInitializer(
                    $"Generated{(shape.Operation == StructuralOperation.Add ? "Add" : "Set")}Values",
                    slots,
                    "T",
                    shape.Operation == StructuralOperation.Add ? "Set" : "SetUnsafe")
                : string.Empty
        }, "\n\n");

        string extension = GeneratorTemplates.ExtensionTemplate(
            $$"""GeneratedStructuralExtensions_{{hash}}""",
            isInternal: false,
            GeneratorTemplates.Indent(extensionMembers, "    "));
        string[] members = new[]
            {
                slots.HasExplicitIds
                    ? null
                    : GeneratorTemplates.PrimaryComponentSetKeyDeclaration(slots.Arity),
                extension
            }
            .OfType<string>()
            .ToArray();
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            shape.Namespace,
            GeneratorSupport.EcsNamespaceUsings(shape.Namespace),
            members.ToImmutableArray()));
    }

    private static IEnumerable<string> ParameterFragments(StructuralModel shape, SignatureProjection slots)
    {
        TargetKind target = shape.Api.Target;
        return shape.Operation == StructuralOperation.Create
            ? new[] { CreateParameters(shape, slots) }
            : target switch
            {
                TargetKind.EntityList => new[]
                {
                "global::System.ReadOnlySpan<Entity> entities",
                slots.HasExplicitIds
                    ? slots.ComponentIdParameters()
                    : string.Empty
            },
                TargetKind.Entity => new[]
                {
                "Entity entity",
                shape.HasValues ? slots.ValueParameters() : string.Empty,
                slots.HasExplicitIds
                    ? slots.ComponentIdParameters()
                    : string.Empty
            },
                TargetKind.Query => new[]
                {
                "in Query query",
                slots.HasExplicitIds
                    ? slots.ComponentIdParameters()
                    : string.Empty
            },
                _ => Array.Empty<string>()
            };
    }

    private static string CreateParameters(StructuralModel shape, SignatureProjection slots)
    {
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            slots.HasExplicitIds
                ? slots.ComponentIdParameters()
                : string.Empty,
            "int count",
            shape.HasOutput ? "global::System.Span<Entity> output" : string.Empty
        }, ", ");
    }

    private static string RenderBody(StructuralModel shape, SignatureProjection slots)
        => shape.HasValues
            ? RenderValueBody(shape, slots)
            : shape.Operation == StructuralOperation.Create
                ? RenderCreateBody(shape, slots)
                : RenderMutationBody(shape, slots);

    private static string RenderValueBody(StructuralModel shape, SignatureProjection slots)
    {
        string operation = shape.Operation == StructuralOperation.Add ? "Add" : "Set";
        string initializerName = $$"""Generated{{operation}}Values{{slots.GenericParameters()}}""";
        string initializerArguments = GeneratorTemplates.JoinIndexed(
            slots.Arity,
            index => $$"""components[{{index}}], in value{{index}}""",
            ",\n");
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape, slots),
            $$"""
                var initializer = new {{initializerName}}(
                {{initializerArguments}}
                );
                """,
            $$"""
                return GeneratedForEachRuntime.ExecuteGenerated{{operation}}(
                    target,
                    entity,
                    components,
                    ref initializer);
                """
        });
    }

    private static string RenderCreateBody(StructuralModel shape, SignatureProjection slots)
        => GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape, slots),
            $"return target.Create(components, count{(shape.HasOutput ? ", output" : string.Empty)});"
        });

    private static string RenderMutationBody(StructuralModel shape, SignatureProjection slots)
    {
        string operation = shape.Operation == StructuralOperation.Add ? "Add" : "Remove";
        string targetArguments = shape.Api.Target switch
        {
            TargetKind.Query => "in query, components",
            TargetKind.Entity => "entity, components",
            _ => "entities, components"
        };
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape, slots),
            $$"""return target.{{operation}}({{targetArguments}});"""
        });
    }

    private static string RenderComponents(StructuralModel shape, SignatureProjection slots)
    {
        if (!slots.HasExplicitIds)
        {
            string components = GeneratorTemplates.PrimaryComponentIds(
                "target",
                GeneratorTemplates.Indexed(slots.Arity, index => slots.GenericType(index)).ToArray(),
                namespaceName: shape.Namespace);
            return $$"""global::System.ReadOnlySpan<ComponentId> components = {{components}};""";
        }

        string assignments = RenderComponentAssignments("components", slots, "component");
        return $$"""
            global::System.Span<ComponentId> components = stackalloc ComponentId[{{slots.Arity}}];
            {{assignments}}
            """;
    }

    private static string RenderComponentAssignments(string destination, SignatureProjection slots, string parameterPrefix)
        => GeneratorTemplates.JoinIndexed(slots.Arity, index => $$"""{{destination}}[{{index}}] = {{parameterPrefix}}{{index}};""", "\n");

    private static string MethodName(StructuralModel shape)
        => shape.Operation switch
        {
            StructuralOperation.Create => "Create",
            StructuralOperation.Set => "Set",
            StructuralOperation.Add => "Add",
            StructuralOperation.Remove => "Remove",
            _ => "Set"
        };

    private static string ReturnType(StructuralModel shape)
        => shape.Api.Target == TargetKind.Entity ? "bool" : "int";


}
