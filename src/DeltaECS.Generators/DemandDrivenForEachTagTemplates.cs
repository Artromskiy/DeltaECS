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
            "{"
        };

        if (shape.HasEntity)
        {
            lines.Add("    int tagCount = tagSlots.Length;");
            lines.Add("    ref int tagSlot = ref GeneratedForEachRuntime.GetGeneratedTagSlotReference(tagSlots);");
            lines.Add("    for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)");
            lines.Add("    {");
            lines.Add("        int slotIndex = tagSlot;");
            lines.Add("        slots.SetEntityRefSlot(ref entityRefView, slotIndex);");
        }
        else
        {
            lines.Add("    for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)");
            lines.Add("    {");
            lines.Add("        int slotIndex = tagSlots[tagIndex];");
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
            shape.HasEntity ? "entity" : string.Empty) + ";");
        if (shape.HasEntity)
        {
            lines.Add("        tagSlot = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref tagSlot, 1);");
        }
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

        string entityAssignment = "slots.SetEntityRefSlot(ref entityRefView, slotIndex);";
        string stampLocals = GeneratorTemplates.JoinNonEmpty(GeneratorTemplates.Indexed(shape.ComponentModels.Length, index =>
            $"Stamp component{index} = slots.GetGeneratedStamp(_access{index}, tagIndex);"));
        string invocation = AppendClosedInvocation(shape, actionName, functorName, contextName, "component", "entity");
        return $$"""
            if (slots.HasTagFilters && slots.TryGetTagSlots(out var tagSlots))
            {
                int tagCount = tagSlots.Length;
                ref int tagSlot = ref GeneratedForEachRuntime.GetGeneratedTagSlotReference(tagSlots);
                for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)
                {
                    int slotIndex = tagSlot;
                    {{entityAssignment}}
            {{GeneratorTemplates.Indent(stampLocals, "        ")}}
                    {{invocation}};
                    tagSlot = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref tagSlot, 1);
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
            "batch.Chunk.TryGetTagSlots(out var tagSlots)",
            shape.HasEntity ? "entity" : string.Empty);

    private static void AppendUnboundTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        string contextName,
        IReadOnlyList<string> denseLoopLines)
        => AppendTagSelectionLoop(
            lines,
            shape,
            contextName,
            denseLoopLines,
            "execution.TryGetTagSlots(out var tagSlots)",
            shape.HasEntity ? "entity" : string.Empty);

    private static void AppendTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        string contextName,
        IReadOnlyList<string> denseLoopLines,
        string tryGetTagSlots,
        string entityName)
    {
        var selectedBody = new List<string>();
        if (shape.HasEntity)
        {
            selectedBody.Add("                slots.SetEntityRefSlot(ref entityRefView, slotIndex);");
        }

        for (int index = 0; index < shape.ComponentModels.Length; index++)
        {
            selectedBody.Add($"                ref {ComponentType(shape, index)} tagged{index} = ref {UnsafeAdd($"component{index}", "slotIndex")};");
        }

        selectedBody.Add("                " + AppendClosedInvocation(
            shape,
            "action",
            "action",
            contextName,
            "tagged",
            shape.HasEntity ? entityName : string.Empty) + ";");
        AppendTagSelectionBranch(lines, tryGetTagSlots, selectedBody, denseLoopLines, entityName);
    }

    private static void AppendInterceptedTagSelectionLoop(
        List<string> lines,
        IterationModel shape,
        InterceptionSite site,
        string tagSlotsLookup,
        string tagContextSetup,
        string callbackName,
        string[] parameters,
        bool inlineLambda,
        string[] rowNames,
        IReadOnlyList<string> denseLoopLines)
    {
        var setup = new List<string>();
        if (tagContextSetup.Length != 0)
        {
            setup.AddRange(SplitLines(tagContextSetup).Select(static line => "            " + line));
        }

        var selectedBody = new List<string>();
        int parameterIndex = shape.HasContext ? 1 : 0;
        if (shape.HasEntity)
        {
            selectedBody.Add("                slots.SetEntityRefSlot(ref entityRefView, slotIndex);");
            parameterIndex++;
        }

        if (inlineLambda)
        {
            for (int index = 0; index < shape.ComponentModels.Length; index++)
            {
                string modifier = shape.ComponentModels[index].IsWrite ? "ref " : "ref readonly ";
                selectedBody.Add($"                {modifier}{shape.ComponentModels[index].ResolvedTypeName} {parameters[parameterIndex + index]} = ref {UnsafeAdd(rowNames[index], "slotIndex")};");
            }

            selectedBody.Add(AppendInterceptedLambdaBody(site, "                "));
        }
        else
        {
            string[] callbackParameters = (string[])parameters.Clone();
            int firstComponentParameter = parameterIndex;
            for (int index = 0; index < rowNames.Length; index++)
            {
                callbackParameters[firstComponentParameter + index] = UnsafeAdd(rowNames[index], "slotIndex");
            }

            selectedBody.Add("                " + AppendCallbackInvocation(shape, callbackName, callbackParameters, string.Empty));
        }

        AppendTagSelectionBranch(
            lines,
            tagSlotsLookup,
            selectedBody,
            denseLoopLines,
            shape.HasEntity ? parameters[shape.HasContext ? 1 : 0] : string.Empty,
            setup);
    }

    private static void AppendTagSelectionBranch(
        List<string> lines,
        string tagSlotsLookup,
        IReadOnlyList<string> selectedBody,
        IReadOnlyList<string> denseLoopLines,
        string entityName = "",
        IReadOnlyList<string>? setup = null)
    {
        lines.Add($"        if ({tagSlotsLookup})");
        lines.Add("        {");
        if (setup is not null)
        {
            lines.AddRange(setup);
        }

        if (entityName.Length != 0)
        {
            lines.Add("            int tagCount = tagSlots.Length;");
            lines.Add("            ref int tagSlot = ref GeneratedForEachRuntime.GetGeneratedTagSlotReference(tagSlots);");
        }

        lines.Add(entityName.Length != 0
            ? "            for (int tagIndex = 0; tagIndex < tagCount; tagIndex++)"
            : "            for (int tagIndex = 0; tagIndex < tagSlots.Length; tagIndex++)");
        lines.Add("            {");
        lines.Add(entityName.Length != 0
            ? "                int slotIndex = tagSlot;"
            : "                int slotIndex = tagSlots[tagIndex];");
        lines.AddRange(selectedBody);
        if (entityName.Length != 0)
        {
            lines.Add("                tagSlot = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref tagSlot, 1);");
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
        var selectedBody = new List<string>();
        int parameterIndex = shape.HasContext ? 1 : 0;
        if (shape.HasEntity)
        {
            selectedBody.Add("                slots.SetEntityRefSlot(ref entityRefView, slotIndex);");
            parameterIndex++;
        }

        for (int index = 0; index < shape.ComponentModels.Length; index++)
        {
            selectedBody.Add($"                Stamp {parameters[parameterIndex + index]} = slots.GetGeneratedStamp(_access{index}, tagIndex);");
        }

        if (inlineLambda)
        {
            selectedBody.Add(AppendInterceptedLambdaBody(site, "                "));
        }
        else
        {
            selectedBody.Add("                " + AppendCallbackInvocation(shape, callbackName, parameters, string.Empty));
        }

        AppendTagSelectionBranch(
            lines,
            "slots.HasTagFilters && slots.TryGetTagSlots(out var tagSlots)",
            selectedBody,
            denseLoopLines,
            shape.HasEntity ? parameters[shape.HasContext ? 1 : 0] : string.Empty);
    }
}
