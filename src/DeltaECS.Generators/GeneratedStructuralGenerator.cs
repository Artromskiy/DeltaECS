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

    private static bool TryReadShape(SemanticModel model, InvocationExpressionSyntax invocation, out StructuralModel? shape)
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
                valuesAllowed: name == "Add",
                out InvocationCursorResult cursorResult,
                out int boundArity,
                out bool hasValues,
                out RegistrationBindingKind registrationBinding))
        {
            return false;
        }

        if (hasValues
            && (name != "Add"
                || cursorResult.Target is not (TargetKind.Entity or TargetKind.EntityList)
                || boundArity < 1
                || !HasSupportedValues(model, invocation, cursorResult)))
        {
            return false;
        }

        if (!hasValues && genericArity is null && cursorResult.ComponentIdCount == 0)
        {
            return false;
        }

        shape = new StructuralModel(
            name == "Remove" ? StructuralOperation.Remove : StructuralOperation.Add,
            cursorResult.Target,
            boundArity,
            TypeBinding: hasValues || genericArity.HasValue ? TypeBindingKind.Generic : TypeBindingKind.None,
            RegistrationBinding: registrationBinding,
            HasValues: hasValues,
            ThrowOnTypeMismatch: hasValues && boundArity > 1,
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

        if (TryReadCreatePayloadShape(model, invocation, descriptor, genericArity, values: false, out shape))
        {
            return true;
        }

        if ((isGeneric || HasNamedValueArgument(arguments))
            && TryReadCreatePayloadShape(model, invocation, descriptor, genericArity, values: true, out shape))
        {
            return true;
        }

        if (!new InvocationCursor(model, arguments, descriptor).TryRead(-1, out InvocationCursorResult cursorResult)
            || !ArityEvidence.TryBind(
                descriptor.MinimumArity,
                out int arity,
                isGeneric ? genericArity : null,
                cursorResult.ComponentIdCount == 0 ? null : cursorResult.ComponentIdCount))
        {
            return !isGeneric && TryReadCreatePayloadShape(model, invocation, descriptor, 0, values: true, out shape);
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

    private static bool TryReadCreatePayloadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        int genericArity,
        bool values,
        out StructuralModel? shape)
    {
        shape = null;
        InvocationTailRule tail = values ? InvocationTailRule.Values : InvocationTailRule.None;
        if (!new InvocationCursor(model, invocation.ArgumentList.Arguments, descriptor.WithTail(tail))
                .TryRead(-1, out InvocationCursorResult selection))
        {
            return false;
        }

        int arity;
        TypeBindingKind typeBinding;
        bool createsOne;
        if (values)
        {
            if ((selection.ComponentIdCount != 0 && genericArity != 0 && selection.ComponentIdCount != genericArity)
                || (!selection.HasComponentIds)
                || selection.TailCount == 0
                || (genericArity != 0 && selection.TailCount != genericArity))
            {
                return false;
            }

            GenericNameSyntax? genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;
            if (!HasSupportedValues(model, invocation, selection, genericName))
            {
                return false;
            }

            arity = genericArity == 0 ? selection.TailCount : genericArity;
            typeBinding = TypeBindingKind.Generic;
            createsOne = false;
        }
        else
        {
            if (!selection.HasComponentIds
                || (genericArity != 0 && selection.ComponentIdCount != 0 && selection.ComponentIdCount != genericArity)
                || (genericArity == 0 && selection.HasComponentIdSpan))
            {
                return false;
            }

            arity = genericArity == 0 ? selection.ComponentIdCount : genericArity;
            if (arity < descriptor.MinimumArity)
            {
                return false;
            }

            typeBinding = genericArity == 0 ? TypeBindingKind.None : TypeBindingKind.Generic;
            createsOne = true;
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            arity,
            TypeBinding: typeBinding,
            RegistrationBinding: selection.RegistrationBinding,
            HasValues: values,
            CreatesOne: createsOne,
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

    private static bool HasNamedValueArgument(SeparatedSyntaxList<ArgumentSyntax> arguments) => arguments.Any(static argument => argument.NameColon?.Name.Identifier.ValueText is "value" or "value0");

}
