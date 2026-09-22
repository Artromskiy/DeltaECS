namespace Delta.ECS.Generators;

internal static partial class DemandDrivenForEachTemplates
{
    private static string AppendParallelTagSelectionLoop(
        IterationModel shape,
        IReadOnlyList<string> denseLoopLines,
        string contextName,
        string actionName,
        string functorName,
        string rowPrefix)
    {
        var lines = new List<string>
        {
            "if (slots.HasTagFilters && slots.TryGetTagSlots(out var tagSlots))",
            "{",
            "    for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)",
            "    {",
            "        int slotIndex = tagSlots[tagIndex];"
        };

        if (shape.HasEntity)
        {
            lines.Add("        Entity taggedEntity = global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex);");
        }

        for (int index = 0; index < shape.ComponentModels.Length; index++)
        {
            lines.Add($"        ref {shape.ComponentModels[index].TypeName} tagged{index} = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref {rowPrefix}{index}, slotIndex);");
        }

        lines.Add("        " + AppendClosedInvocation(
            shape,
            actionName,
            functorName,
            contextName,
            "tagged",
            shape.HasEntity ? "taggedEntity" : string.Empty) + ";");
        lines.Add("    }");
        lines.Add("}");
        lines.Add("else");
        lines.Add("{");
        lines.AddRange(denseLoopLines.Select(static line => "    " + line));
        lines.Add("}");
        return string.Join("\n", lines);
    }

    private static string AppendParallelStampTagSelectionLoop(
        IterationModel shape,
        string denseLoopBody,
        string contextName,
        string actionName,
        string functorName)
    {
        if (!shape.HasEntity)
        {
            return denseLoopBody;
        }

        string entityArgument = "Entity entity = global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex);";
        string stampLocals = GeneratorTemplates.JoinNonEmpty(GeneratorTemplates.Indexed(shape.ComponentModels.Length, index =>
            $"Stamp component{index} = slots.GetGeneratedStamp(_access{index}, tagIndex);"));
        string invocation = AppendClosedInvocation(shape, actionName, functorName, contextName, "component", "entity");
        return $$"""
            if (slots.HasTagFilters && slots.TryGetTagSlots(out var tagSlots))
            {
                for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)
                {
                    int slotIndex = tagSlots[tagIndex];
                    {{entityArgument}}
            {{GeneratorTemplates.Indent(stampLocals, "        ")}}
                    {{invocation}};
                }
            }
            else
            {
            {{GeneratorTemplates.Indent(denseLoopBody, "    ")}}
            }
            """;
    }

    private static void AppendBoundTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        string contextName,
        IReadOnlyList<string> denseLoopLines)
        => AppendTagSelectionLoop(
            lines,
            shape,
            contextName,
            denseLoopLines,
            "batch.Chunk.HasTagFilters && batch.Chunk.TryGetTagSlots(out var tagSlots)",
            "global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex)");

    private static void AppendUnboundTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        string contextName,
        IReadOnlyList<string> denseLoopLines,
        bool usesReadSlots)
        => AppendTagSelectionLoop(
            lines,
            shape,
            contextName,
            denseLoopLines,
            "execution.HasTagFilters && execution.TryGetTagSlots(out var tagSlots)",
            usesReadSlots
                ? "global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in firstEntity), slotIndex)"
                : "global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex)");

    private static void AppendTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        string contextName,
        IReadOnlyList<string> denseLoopLines,
        string tryGetTagSlots,
        string entityAtSlot)
    {
        lines.Add($"        if ({tryGetTagSlots})");
        lines.Add("        {");
        lines.Add("            for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)");
        lines.Add("            {");
        lines.Add("                int slotIndex = tagSlots[tagIndex];");
        if (shape.HasEntity)
        {
            lines.Add($"                Entity taggedEntity = {entityAtSlot};");
        }

        for (int index = 0; index < shape.ComponentModels.Length; index++)
        {
            lines.Add($"                ref {ComponentType(shape, index)} tagged{index} = ref {UnsafeAdd($"component{index}", "slotIndex")};");
        }

        lines.Add("                " + AppendClosedInvocation(
            shape,
            "action",
            "action",
            contextName,
            "tagged",
            shape.HasEntity ? "taggedEntity" : string.Empty) + ";");
        lines.Add("            }");
        lines.Add("        }");
        lines.Add("        else");
        lines.Add("        {");
        lines.AddRange(denseLoopLines.Select(static line => "    " + line));
        lines.Add("        }");
    }

    private static void AppendInterceptedTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        InterceptionSite site,
        string tagSlotsLookup,
        string tagContextSetup,
        bool usesReadSlots,
        string callbackName,
        string[] parameters,
        bool inlineLambda,
        string[] rowNames,
        IReadOnlyList<string> denseLoopLines)
    {
        lines.Add($"        if ({tagSlotsLookup})");
        lines.Add("        {");
        if (tagContextSetup.Length != 0)
        {
            lines.AddRange(SplitLines(tagContextSetup).Select(static line => "            " + line));
        }
        lines.Add("            for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)");
        lines.Add("            {");
        lines.Add("                int slotIndex = tagSlots[tagIndex];");

        int parameterIndex = shape.HasContext ? 1 : 0;
        if (shape.HasEntity)
        {
            string entityAtSlot = usesReadSlots
                ? "global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in firstEntity), slotIndex)"
                : "global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex)";
            lines.Add($"                global::Delta.ECS.Entity {parameters[parameterIndex]} = {entityAtSlot};");
            parameterIndex++;
        }

        if (inlineLambda)
        {
            for (int index = 0; index < shape.ComponentModels.Length; index++)
            {
                string modifier = shape.ComponentModels[index].IsWrite ? "ref " : "ref readonly ";
                lines.Add($"                {modifier}{shape.ComponentModels[index].ResolvedTypeName} {parameters[parameterIndex + index]} = ref {UnsafeAdd(rowNames[index], "slotIndex")};");
            }

            lines.Add(AppendInterceptedLambdaBody(site, "                "));
        }
        else
        {
            string[] callbackParameters = (string[])parameters.Clone();
            int firstComponentParameter = parameterIndex;
            for (int index = 0; index < rowNames.Length; index++)
            {
                callbackParameters[firstComponentParameter + index] = UnsafeAdd(rowNames[index], "slotIndex");
            }

            lines.Add("                " + AppendCallbackInvocation(shape, callbackName, callbackParameters, string.Empty));
        }

        lines.Add("            }");
        lines.Add("        }");
        lines.Add("        else");
        lines.Add("        {");
        lines.AddRange(denseLoopLines.Select(static line => "    " + line));
        lines.Add("        }");
    }

    private static void AppendInterceptedParallelStampTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        InterceptionSite site,
        string callbackName,
        string[] parameters,
        bool inlineLambda,
        IReadOnlyList<string> denseLoopLines)
    {
        lines.Add("        if (slots.HasTagFilters && slots.TryGetTagSlots(out var tagSlots))");
        lines.Add("        {");
        lines.Add("            for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)");
        lines.Add("            {");
        lines.Add("                int slotIndex = tagSlots[tagIndex];");
        int parameterIndex = shape.HasContext ? 1 : 0;
        if (shape.HasEntity)
        {
            lines.Add($"                global::Delta.ECS.Entity {parameters[parameterIndex++]} = global::System.Runtime.CompilerServices.Unsafe.Add(ref firstEntity, slotIndex);");
        }

        for (int index = 0; index < shape.ComponentModels.Length; index++)
        {
            lines.Add($"                Stamp {parameters[parameterIndex + index]} = slots.GetGeneratedStamp(_access{index}, tagIndex);");
        }

        if (inlineLambda)
        {
            lines.Add(AppendInterceptedLambdaBody(site, "                "));
        }
        else
        {
            lines.Add("                " + AppendCallbackInvocation(shape, callbackName, parameters, string.Empty));
        }

        lines.Add("            }");
        lines.Add("        }");
        lines.Add("        else");
        lines.Add("        {");
        lines.AddRange(denseLoopLines.Select(static line => "    " + line));
        lines.Add("        }");
    }
}
