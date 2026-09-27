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
    {
        context.RegisterSourceOutput(
            GeneratorPipeline.ShapeProvider<StructuralModel>(
                    context,
                    static syntaxContext => TryReadShape(
                        syntaxContext.SemanticModel,
                        (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax)syntaxContext.Node,
                        out StructuralModel? shape)
                        ? shape
                        : null)
                .Where(static shape => shape is not null)
                .Select(static (shape, _) => shape!)
                .Collect(),
            static (productionContext, shapes) => GeneratorPipeline.EmitShapes<StructuralModel>(
                shapes,
                productionContext,
                "GeneratedStructural_",
                static shape => shape.Key,
                static shape => GeneratedStructuralTemplates.Render(shape)));
    }

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

        if (member.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        int arity = genericName.TypeArgumentList.Arguments.Count;
        return TryReadMutationShape(model, invocation, name, descriptor, arity, out shape);
    }

    private static bool TryReadMutationShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        string name,
        ApiDescriptor descriptor,
        int arity,
        out StructuralModel? shape)
    {
        shape = null;
        if (!InvocationGrammar.TryReadStructuralMutation(
                model,
                invocation.ArgumentList.Arguments,
                descriptor,
                arity,
                valuesAllowed: false,
                out InvocationCursorResult cursorResult,
                out int boundArity,
                out _,
                out RegistrationBindingKind registrationBinding))
        {
            return false;
        }

        shape = new StructuralModel(
            Operation(name),
            cursorResult.Target,
            boundArity,
            registrationBinding: registrationBinding,
            hasValues: false,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation));
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
            || arity < 2
            || (cursorResult.ComponentIdCount != 0 && cursorResult.ComponentIdCount != arity))
        {
            return false;
        }

        for (int index = cursorResult.TailStart; index < cursorResult.TailStart + cursorResult.TailCount; index++)
        {
            ITypeSymbol? valueType = model.GetTypeInfo(invocation.ArgumentList.Arguments[index].Expression).Type;
            if (valueType is null || GeneratorSupport.IsComponentId(valueType))
            {
                return false;
            }
        }

        shape = new StructuralModel(
            StructuralOperation.Add,
            cursorResult.Target,
            arity: arity,
            registrationBinding: cursorResult.ComponentIdCount == 0
                ? RegistrationBindingKind.Primary
                : RegistrationBindingKind.Explicit,
            hasValues: true,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation));
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

        if (isGeneric
            && TryReadCreateValueShape(model, invocation, descriptor, genericArity, out shape))
        {
            return true;
        }

        if (!new InvocationCursor(model, arguments, descriptor).TryRead(-1, out InvocationCursorResult cursorResult))
        {
            return false;
        }

        int? explicitArity = cursorResult.ComponentIdCount == 0 ? null : cursorResult.ComponentIdCount;
        var arityEvidence = new ArityEvidence();
        arityEvidence.Add(isGeneric ? genericArity : null);
        arityEvidence.Add(explicitArity);
        if (!arityEvidence.TryBind(descriptor.MinimumArity, out int arity))
        {
            return false;
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            arity,
            typeBinding: isGeneric ? TypeBindingKind.Generic : TypeBindingKind.None,
            registrationBinding: explicitArity.HasValue
                ? RegistrationBindingKind.Explicit
                : RegistrationBindingKind.Primary,
            hasOutput: cursorResult.HasOutput,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation));
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
            || cursorResult.ComponentIdCount != genericArity
            || cursorResult.TailCount != genericArity
            || genericArity < 2)
        {
            return false;
        }

        GenericNameSyntax genericName = (GenericNameSyntax)((MemberAccessExpressionSyntax)invocation.Expression).Name;
        for (int index = 0; index < cursorResult.TailCount; index++)
        {
            ITypeSymbol? valueType = model.GetTypeInfo(
                invocation.ArgumentList.Arguments[cursorResult.TailStart + index].Expression).Type;
            ITypeSymbol? componentType = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[index]).Type;
            if (valueType is null
                || componentType is null
                || GeneratorSupport.IsComponentId(valueType)
                || !((CSharpCompilation)model.Compilation).ClassifyConversion(valueType, componentType).IsImplicit)
            {
                return false;
            }
        }

        shape = new StructuralModel(
            StructuralOperation.Create,
            TargetKind.World,
            genericArity,
            registrationBinding: RegistrationBindingKind.Explicit,
            hasValues: true,
            namespaceName: GeneratorSupport.ContainingNamespace(model, invocation));
        return true;
    }

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
