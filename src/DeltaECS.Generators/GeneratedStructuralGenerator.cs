using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates generic structural-operation façades only for the
/// Add/Remove/Create shapes used by a consumer assembly.
/// </summary>
[Generator]
public sealed class GeneratedStructuralGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => GeneratorPipeline.RegisterShapeOutput<StructuralModel>(
            context,
            static syntaxContext => TryReadShape(
                syntaxContext.SemanticModel,
                (InvocationExpressionSyntax)syntaxContext.Node,
                out StructuralModel? shape)
                ? shape
                : null,
            "GeneratedStructural_",
            static shape => shape.Key,
            static shape => GeneratedStructuralTemplates.Render(shape));

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out StructuralModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        string name = member.Name.Identifier.ValueText;
        if (name is not ("Add" or "Remove" or "Create")
            || !ApiDescriptor.TryGet(name, out ApiDescriptor descriptor)
            || descriptor.Family != GeneratedApiKind.Structural
            || !GeneratorSupport.IsNamedType(model.GetTypeInfo(member.Expression).Type, "World"))
        {
            return false;
        }

        if (name == "Create")
        {
            return TryReadCreateShape(
                model,
                invocation,
                member.Name is GenericNameSyntax createName
                    ? createName.TypeArgumentList.Arguments.Count
                    : 0,
                out shape);
        }

        if (name == "Add" && TryReadValueShape(model, invocation, descriptor, out shape))
        {
            return true;
        }

        int? arity = member.Name is GenericNameSyntax genericName
            ? genericName.TypeArgumentList.Arguments.Count
            : null;
        return TryReadMutationShape(model, invocation, name, descriptor, arity, out shape);
    }

    private static bool TryReadMutationShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        string name,
        ApiDescriptor descriptor,
        int? genericArity,
        out StructuralModel? shape)
    {
        shape = null;
        if (!InvocationGrammar.TryReadStructuralMutation(
                model,
                invocation.ArgumentList.Arguments,
                descriptor,
                genericArity,
                valuesAllowed: false,
                out InvocationCursorResult cursorResult,
                out int boundArity,
                out _,
                out RegistrationBindingKind registrationBinding))
        {
            return false;
        }

        if (genericArity is null && cursorResult.ComponentIdCount == 0)
        {
            return false;
        }

        shape = new StructuralModel(
            Operation(name),
            cursorResult.Target,
            boundArity,
            TypeBinding: genericArity.HasValue ? TypeBindingKind.Generic : TypeBindingKind.None,
            RegistrationBinding: registrationBinding,
            HasValues: false,
            Namespace: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

    private static bool TryReadValueShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        out StructuralModel? shape)
    {
        shape = null;
        if (!InvocationGrammar.TryReadStructuralMutation(
                model,
                invocation.ArgumentList.Arguments,
                descriptor,
                GenericArity(invocation),
                valuesAllowed: true,
                out InvocationCursorResult cursorResult,
                out int arity,
                out bool hasValues,
                out _)
            || cursorResult.Target is not (TargetKind.Entity or TargetKind.EntityList)
            || !hasValues
            || arity < 1
            || (cursorResult.ComponentIdCount != 0 && cursorResult.ComponentIdCount != arity))
        {
            return false;
        }

        if (!HasSupportedValues(model, invocation, cursorResult))
        {
            return false;
        }

        shape = new StructuralModel(
            StructuralOperation.Add,
            cursorResult.Target,
            Arity: arity,
            RegistrationBinding: cursorResult.RegistrationBinding,
            HasValues: true,
            ThrowOnTypeMismatch: arity > 1,
            Namespace: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

    private static bool TryReadCreateShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        int genericArity,
        out StructuralModel? shape)
    {
        shape = null;
        bool isGeneric = genericArity > 0;
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (!ApiDescriptor.TryGet("Create", out ApiDescriptor descriptor))
        {
            return false;
        }

        if (TryReadCreateEntityShape(model, invocation, descriptor, genericArity, out shape))
        {
            return true;
        }

        if ((isGeneric || HasNamedValueArgument(arguments))
            && TryReadCreateValueShape(model, invocation, descriptor, genericArity, out shape))
        {
            return true;
        }

        if (!new InvocationCursor(model, arguments, descriptor).TryRead(-1, out InvocationCursorResult cursorResult))
        {
            return !isGeneric && TryReadCreateValueShape(model, invocation, descriptor, 0, out shape);
        }

        int? explicitArity = cursorResult.ComponentIdCount == 0 ? null : cursorResult.ComponentIdCount;
        if (!ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out int arity,
                isGeneric ? genericArity : null,
                explicitArity))
        {
            return !isGeneric && TryReadCreateValueShape(model, invocation, descriptor, 0, out shape);
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            arity,
            TypeBinding: isGeneric ? TypeBindingKind.Generic : TypeBindingKind.None,
            RegistrationBinding: cursorResult.RegistrationBinding,
            HasOutput: cursorResult.HasOutput,
            Namespace: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

    private static bool TryReadCreateEntityShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        int genericArity,
        out StructuralModel? shape)
    {
        shape = null;
        if (!new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor.WithTail(InvocationTailRule.None))
                .TryRead(-1, out InvocationCursorResult selection)
            || !selection.HasComponentIds
            || (genericArity != 0 && selection.ComponentIdCount != 0 && selection.ComponentIdCount != genericArity)
            || (genericArity == 0 && selection.HasComponentIdSpan))
        {
            return false;
        }

        int arity = genericArity == 0 ? selection.ComponentIdCount : genericArity;
        if (arity < descriptor.MinimumArity)
        {
            return false;
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            arity,
            TypeBinding: genericArity == 0 ? TypeBindingKind.None : TypeBindingKind.Generic,
            RegistrationBinding: selection.RegistrationBinding,
            CreatesOne: true,
            Namespace: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

    private static bool TryReadCreateValueShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        int genericArity,
        out StructuralModel? shape)
    {
        shape = null;
        if (!new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor.WithTail(InvocationTailRule.Values))
                .TryRead(-1, out InvocationCursorResult cursorResult)
            || (cursorResult.ComponentIdCount != 0 && cursorResult.ComponentIdCount != genericArity && genericArity != 0)
            || (cursorResult.ComponentIdCount == 0 && !cursorResult.HasComponentIdSpan)
            || cursorResult.TailCount == 0
            || (genericArity != 0 && cursorResult.TailCount != genericArity))
        {
            return false;
        }

        int arity = genericArity == 0 ? cursorResult.TailCount : genericArity;
        GenericNameSyntax? genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
        if (!HasSupportedValues(model, invocation, cursorResult, genericName))
        {
            return false;
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            arity,
            RegistrationBinding: cursorResult.RegistrationBinding,
            HasValues: true,
            Namespace: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

    private static bool HasSupportedValues(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        InvocationCursorResult selection,
        GenericNameSyntax? genericName = null)
        => Enumerable.Range(selection.TailStart, selection.TailCount).All(index =>
        {
            ITypeSymbol? valueType = model.GetTypeInfo(
                invocation.ArgumentList.Arguments[index].Expression).Type;
            if (valueType is null || GeneratorSupport.IsComponentId(valueType))
            {
                return false;
            }

            if (genericName is null)
            {
                return true;
            }

            int componentIndex = index - selection.TailStart;
            ITypeSymbol? componentType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[componentIndex]).Type;
            return componentType is not null
                && ((CSharpCompilation)model.Compilation).ClassifyConversion(valueType, componentType).IsImplicit;
        });

    private static bool HasNamedValueArgument(SeparatedSyntaxList<ArgumentSyntax> arguments)
        => arguments.Any(static argument => argument.NameColon?.Name.Identifier.ValueText is "value" or "value0");

    private static int? GenericArity(InvocationExpressionSyntax invocation)
        => (invocation.Expression as MemberAccessExpressionSyntax)?.Name is GenericNameSyntax genericName
            ? genericName.TypeArgumentList.Arguments.Count
            : null;

    private static StructuralOperation Operation(string name)
        => name switch
        {
            "Add" => StructuralOperation.Add,
            _ => StructuralOperation.Remove
        };

}
