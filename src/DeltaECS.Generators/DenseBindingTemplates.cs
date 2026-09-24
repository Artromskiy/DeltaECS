namespace Delta.ECS.Generators;

internal static partial class DemandDrivenForEachTemplates
{
    private static bool UsesDenseBinding(IterationModel shape)
        => !shape.Parallel && !shape.HasEntityTarget && !shape.IsStamp
            && !shape.Api.Signature.HasExplicitIds && shape.ComponentModels.Length > 0;

    private static string BindingTypeArguments(IterationModel shape, bool closed)
        => !shape.Api.Signature.HasGenericSelectors ? string.Empty : SignatureProjection.TypeArguments(
            closed ? string.Join(", ", shape.Components) : shape.Api.Signature.GenericList());

    private static string OpenDenseBinding(IterationModel shape, bool closed)
    {
        string owner = GeneratorSupport.QualifiedName(
            shape.Namespace,
            "DemandForEachExtensions_" + GeneratorSupport.StableName(shape.Key));
        string types = BindingTypeArguments(shape, closed);
        string openMethod = shape.ComponentModels.Any(static component => component.IsWrite)
            ? "OpenBoundDense"
            : "OpenBoundDenseRead";
        return $"using var execution = GeneratedForEachRuntime.{openMethod}<{owner}.DenseBinding{types}, {owner}.DenseRows{types}>(world, in query);";
    }

    private static string RenderDenseBinding(IterationModel shape)
    {
        if (!UsesDenseBinding(shape))
        {
            return string.Empty;
        }
        string generic = BindingTypeArguments(shape, closed: false);
        string fields = GeneratorTemplates.JoinNonEmpty(GeneratorTemplates.Indexed(shape.ComponentModels.Length,
            index => $"internal {ComponentType(shape, index)}[] Row{index};"));
        string routes = GeneratorTemplates.JoinNonEmpty(GeneratorTemplates.Indexed(shape.ComponentModels.Length,
            index => $"private int _route{index};"));
        string prepare = GeneratorTemplates.JoinNonEmpty(GeneratorTemplates.Indexed(shape.ComponentModels.Length,
            index => $"_route{index} = GeneratedForEachRuntime.GetPrepared{(shape.ComponentModels[index].IsWrite ? "Write" : "Read")}Route<{ComponentType(shape, index)}>(in query);"));
        string writes = string.Join(", ", GeneratorTemplates.WriteIndices(shape.ComponentModels).Select(index => $"_route{index}"));
        string assignments = string.Join(",\n", GeneratorTemplates.Indexed(shape.ComponentModels.Length,
            index => $"Row{index} = GeneratedForEachRuntime.GetGeneratedArray<{ComponentType(shape, index)}>(rows, _route{index})"));
        return $$"""
            internal struct DenseRows{{generic}}
            {
                internal GeneratedBoundChunk Chunk;
            {{GeneratorTemplates.Indent(fields, "    ")}}
            }

            internal sealed class DenseBinding{{generic}} : GeneratedDenseBinding<DenseRows{{generic}}>
            {
                public DenseBinding() { }
            {{GeneratorTemplates.Indent(routes, "    ")}}

                protected override void Prepare(in Query query)
                {
            {{GeneratorTemplates.Indent(prepare, "        ")}}
                    {{(writes.Length == 0 ? string.Empty : $"SetWriteRoutes(new int[] {{ {writes} }});")}}
                }

                protected override DenseRows{{generic}} BindRows(global::System.Array[] rows, GeneratedBoundChunk chunk)
                    => new DenseRows{{generic}}
                    {
                        Chunk = chunk,
            {{GeneratorTemplates.Indent(assignments, "            ")}}
                    };
            }
            """;
    }
}
