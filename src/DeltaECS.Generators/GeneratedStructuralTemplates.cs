using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Delta.ECS.Generators;

/// <summary>Raw-string templates for validated structural API shapes.</summary>
internal static class GeneratedStructuralTemplates
{
    internal static string Render(StructuralModel shape)
    {
        string hash = GeneratorSupport.StableName(shape.Key);
        string methodName = MethodName(shape);
        string parameters = GeneratorTemplates.JoinNonEmpty(
            new[] { $"this {ReceiverType(shape.Receiver)} target" }
                .Concat(ParameterFragments(shape))
                .Concat(shape.IsExplicitIds && shape.Mode is not (StructuralMode.Create or StructuralMode.CreateOutput)
                    ? new[] { ComponentParameters(shape.Arity) }
                    : Array.Empty<string>()),
            ", ");
        string body = RenderBody(shape);

        string generic = shape.IsGeneric ? GeneratorSupport.GenericParameters(shape.Arity) : string.Empty;
        string declaration = $$"""
            public static {{ReturnType(shape)}} {{methodName}}{{generic}}({{parameters}})
            """.Trim();
        string method = GeneratorTemplates.Method(
            shape.Api,
            declaration,
            body,
            "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        string members = GeneratorTemplates.JoinNonEmpty(new[]
        {
            method,
            shape.HasValues
                ? RenderValueInitializer(shape.Arity, shape.Plan.Operation == StructuralOperation.Add)
                : string.Empty
        }, "\n\n");

        RenderModel member = GeneratorTemplates.ExtensionTemplate(
            shape.Api,
            $$"""GeneratedStructuralExtensions_{{hash}}""",
            isInternal: false,
            GeneratorTemplates.Indent(members, "    "));
        return GeneratorTemplates.FileTemplate(new GeneratedFileModel(
            "Delta.ECS",
            ImmutableArray<string>.Empty,
            ImmutableArray.Create(member)));
    }

    private static IEnumerable<string> ParameterFragments(StructuralModel shape)
        => shape.Mode switch
        {
            StructuralMode.Entities => new[] { "global::System.ReadOnlySpan<Entity> entities" },
            StructuralMode.SingleEntity => new[]
            {
                "Entity entity",
                shape.HasValues ? ValueParameters(shape.Arity) : string.Empty
            },
            StructuralMode.Query => new[] { "in Query query" },
            StructuralMode.Create => new[]
            {
                shape.IsExplicitIds ? ComponentParameters(shape.Arity) : string.Empty,
                "int count"
            },
            StructuralMode.CreateOutput => new[]
            {
                shape.IsExplicitIds ? ComponentParameters(shape.Arity) : string.Empty,
                "int count",
                "global::System.Span<Entity> output"
            },
            StructuralMode.ExplicitCreate => new[] { ComponentParameters(shape.Arity), "int count" },
            StructuralMode.ExplicitCreateOutput => new[]
            {
                ComponentParameters(shape.Arity),
                "int count",
                "global::System.Span<Entity> output"
            },
            _ => Array.Empty<string>()
        };

    private static string RenderBody(StructuralModel shape)
        => shape.HasValues
            ? RenderValueBody(shape)
            : shape.Mode is StructuralMode.CreateSingle or StructuralMode.Create or StructuralMode.CreateOutput
                ? RenderCreateBody(shape)
                : shape.Mode is StructuralMode.ExplicitCreate or StructuralMode.ExplicitCreateOutput
                    ? RenderExplicitCreateBody(shape)
                    : RenderMutationBody(shape);

    private static string RenderValueBody(StructuralModel shape)
    {
        string operation = shape.Plan.Operation == StructuralOperation.Add ? "Add" : "Set";
        string initializerName = $$"""Generated{{operation}}Values<{{GeneratorSupport.GenericTypes(shape.Arity)}}>""";
        string initializerArguments = string.Join(",\n", Enumerable.Range(0, shape.Arity)
            .Select(index => $$"""components[{{index}}], in value{{index}}"""));
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape),
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

    private static string RenderCreateBody(StructuralModel shape)
        => GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape),
            shape.Mode switch
            {
                StructuralMode.CreateSingle => "return target.Create(components);",
                StructuralMode.Create => "return target.Create(components, count);",
                _ => "return target.Create(components, count, output);"
            }
        });

    private static string RenderExplicitCreateBody(StructuralModel shape)
        => GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape),
            shape.Mode == StructuralMode.ExplicitCreate
                ? "return target.Create(components, count);"
                : "return target.Create(components, count, output);"
        });

    private static string RenderMutationBody(StructuralModel shape)
    {
        string operation = shape.Plan.Operation == StructuralOperation.Add ? "Add" : "Remove";
        string targetArguments = shape.Mode switch
        {
            StructuralMode.Query => "in query, components",
            StructuralMode.SingleEntity => "entity, components",
            _ => "entities, components"
        };
        return GeneratorTemplates.JoinNonEmpty(new[]
        {
            RenderComponents(shape),
            $$"""return target.{{operation}}({{targetArguments}});"""
        });
    }

    private static string RenderComponents(StructuralModel shape)
    {
        bool explicitIds = shape.IsExplicitIds
            || shape.Mode is StructuralMode.ExplicitCreate or StructuralMode.ExplicitCreateOutput;
        string assignments = explicitIds
            ? RenderComponentAssignments("components", shape.Arity, "component")
            : RenderPrimaryAssignments("target.Layouts", "components", shape.Arity);
        return $$"""
            global::System.Span<ComponentId> components = stackalloc ComponentId[{{shape.Arity}}];
            {{assignments}}
            """;
    }

    private static string RenderValueInitializer(int arity, bool isAdd)
    {
        string name = $"Generated{(isAdd ? "Add" : "Set")}Values";
        string generic = GeneratorSupport.GenericTypes(arity);
        string fields = GeneratorTemplates.JoinNonEmpty(Enumerable.Range(0, arity).Select(index => $$"""
            private readonly ComponentId component{{index}};
            private readonly T{{index + 1}} value{{index}};
            """));
        string parameters = string.Join(", ", Enumerable.Range(0, arity).Select(index => $$"""ComponentId component{{index}}, in T{{index + 1}} value{{index}}"""));
        string assignments = GeneratorTemplates.JoinNonEmpty(Enumerable.Range(0, arity).Select(index => $$"""
            this.component{{index}} = component{{index}};
            this.value{{index}} = value{{index}};
            """));
        string writes = GeneratorTemplates.JoinNonEmpty(Enumerable.Range(0, arity).Select(index => $$"""
            writer.Set{{(isAdd ? string.Empty : "Unsafe")}}(component{{index}}, in value{{index}});
            """));
        string body = GeneratorTemplates.JoinNonEmpty(new[]
        {
            GeneratorTemplates.RenderBlock($"internal {name}({parameters})", assignments),
            GeneratorTemplates.RenderBlock("public void Initialize(ref global::Delta.ECS.GeneratedComponentValueWriter writer)", writes)
        }, "\n\n");
        return GeneratorTemplates.RenderBlock(
            $"private struct {name}<{generic}> : global::Delta.ECS.IGeneratedComponentValueInitializer",
            GeneratorTemplates.JoinNonEmpty(new[] { fields, body }, "\n\n"));
    }

    private static string RenderComponentAssignments(string destination, int arity, string parameterPrefix)
        => GeneratorTemplates.JoinNonEmpty(Enumerable.Range(0, arity)
            .Select(index => $$"""{{destination}}[{{index}}] = {{parameterPrefix}}{{index}};"""));

    private static string RenderPrimaryAssignments(string registry, string destination, int arity)
        => GeneratorTemplates.JoinNonEmpty(Enumerable.Range(0, arity)
            .Select(index => $$"""{{destination}}[{{index}}] = {{registry}}.GetPrimary<T{{index + 1}}>();"""));

    private static string MethodName(StructuralModel shape)
        => shape.Plan.Operation switch
        {
            StructuralOperation.Create => "Create",
            StructuralOperation.Set => "Set",
            StructuralOperation.Add => "Add",
            StructuralOperation.Remove => "Remove",
            _ => "Set"
        };

    private static string ReturnType(StructuralModel shape)
        => shape.Mode switch
        {
            StructuralMode.CreateSingle => "Entity",
            StructuralMode.SingleEntity => "bool",
            _ => "int"
        };

    private static string ComponentParameters(int arity)
        => string.Join(", ", Enumerable.Range(0, arity).Select(static index => $$"""ComponentId component{{index}}"""));

    private static string ValueParameters(int arity)
        => string.Join(", ", Enumerable.Range(0, arity).Select(static index => $$"""in T{{index + 1}} value{{index}}"""));

    private static string ReceiverType(Receiver receiver)
        => receiver switch
        {
            Receiver.World => "World",
            _ => string.Empty
        };


}
