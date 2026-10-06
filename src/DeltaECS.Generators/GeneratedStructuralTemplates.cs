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
                    $"Generated{(shape.Operation == StructuralOperation.Create ? "Create" : "Add")}Values",
                    slots,
                    "T",
                    shape.Operation == StructuralOperation.Create
                        ? "InitializeCreated"
                        : "InitializeAdded")
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
        if (shape.Operation == StructuralOperation.Create)
        {
            return new[] { CreateParameters(shape, slots) };
        }

        TargetKind target = shape.Api.Target;
        string firstParameter = target switch
        {
            TargetKind.EntityList => "global::System.ReadOnlySpan<Entity> entities",
            TargetKind.Entity => "Entity entity",
            TargetKind.Query => "in Query query",
            _ => string.Empty
        };
        if (firstParameter.Length == 0)
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            firstParameter,
            slots.HasExplicitIds ? slots.ComponentIdParameters() : string.Empty,
            shape.HasValues && target != TargetKind.Query ? ValueParameters(slots) : string.Empty
        };
    }

    private static string CreateParameters(StructuralModel shape, SignatureProjection slots)
    {
        if (shape.CreatesOne)
        {
            return slots.ComponentIdParameters();
        }

        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            slots.HasExplicitIds
                ? slots.ComponentIdParameters()
                : string.Empty,
            shape.HasValues ? ValueParameters(slots) : "int count",
            shape.HasOutput ? "global::System.Span<Entity> output" : string.Empty
        }, ", ");
    }

    private static string ValueParameters(SignatureProjection slots)
        => slots.Arity == 1
            ? $"in {slots.GenericType(0)} value"
            : slots.ValueParameters();

    private static string RenderBody(StructuralModel shape, SignatureProjection slots)
    {
        string validateTypes = slots.HasExplicitIds && slots.HasGenericSelectors
            ? shape.Operation == StructuralOperation.Create || shape.ThrowOnTypeMismatch
                ? GeneratorTemplates.JoinIndexed(
                    slots.Arity,
                    index => $$"""GeneratedForEachRuntime.ValidateComponentType<{{slots.GenericType(index)}}>(target, {{slots.ComponentIdArgument(index)}});""",
                    "\n")
                : GeneratorTemplates.JoinIndexed(
                    slots.Arity,
                    index => $$"""if (!GeneratedForEachRuntime.IsComponentType<{{slots.GenericType(index)}}>(target, {{slots.ComponentIdArgument(index)}})) return {{(shape.Api.Target == TargetKind.Entity ? "false" : "0")}};""",
                    "\n")
            : string.Empty;
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            slots.HasDynamicIds && slots.HasGenericSelectors
                ? $"GeneratedForEachRuntime.ValidateComponentIdCount(componentIds, {slots.Arity});"
                : string.Empty,
            validateTypes,
            shape.HasValues
            ? RenderValueBody(shape, slots)
            : shape.Operation == StructuralOperation.Create
                ? RenderCreateBody(shape, slots)
                : RenderMutationBody(shape, slots)
        });
    }

    private static string RenderValueBody(StructuralModel shape, SignatureProjection slots)
    {
        string operation = shape.Operation switch
        {
            StructuralOperation.Create => "Create",
            _ => "Add"
        };
        string initializerName = $$"""Generated{{operation}}Values{{slots.GenericParameters()}}""";
        string initializerArguments = GeneratorTemplates.JoinIndexed(
            slots.Arity,
            index => "components[" + index + "], in " + (slots.Arity == 1 ? "value" : "value" + index),
            ",\n");
        string execute = shape.Operation switch
        {
            StructuralOperation.Create => $$"""return GeneratedForEachRuntime.ExecuteGeneratedCreate(target, components, ref initializer);""",
            StructuralOperation.Add when shape.Api.Target == TargetKind.EntityList => $$"""return GeneratedForEachRuntime.ExecuteGeneratedAdd(target, entities, components, ref initializer);""",
            _ => $$"""return GeneratedForEachRuntime.ExecuteGeneratedAdd(target, entity, components, ref initializer);"""
        };
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape, slots),
            $$"""
                var initializer = new {{initializerName}}(
                {{initializerArguments}}
                );
                """,
            execute
        });
    }

    private static string RenderCreateBody(StructuralModel shape, SignatureProjection slots)
        => GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape, slots),
            shape.CreatesOne
                ? "return target.Create(components);"
                : $"return target.Create(components, count{(shape.HasOutput ? ", output" : string.Empty)});"
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
        => GeneratorTemplates.ComponentIdSpan(slots, "target", shape.Namespace);

    private static string MethodName(StructuralModel shape)
        => shape.Operation switch
        {
            StructuralOperation.Create => "Create",
            StructuralOperation.Add => "Add",
            StructuralOperation.Remove => "Remove",
            _ => "Create"
        };

    private static string ReturnType(StructuralModel shape)
        => shape.Operation == StructuralOperation.Create && (shape.HasValues || shape.CreatesOne)
            ? "Entity"
            : shape.Api.Target == TargetKind.Entity ? "bool" : "int";


}
