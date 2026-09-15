using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates generic structural-operation façades only for the
/// Add/Remove/Set/Create shapes used by a consumer assembly.
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
        if (name is not ("Add" or "Remove" or "Set" or "Create")
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

        if (name is "Add" or "Set"
            && TryReadValueShape(model, invocation, descriptor, name == "Add", out shape))
        {
            return true;
        }

        if (member.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        int arity = genericName.TypeArgumentList.Arguments.Count;
        return name != "Set"
            && TryReadMutationShape(model, invocation, name, descriptor, arity, out shape);
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
            hasValues: false);
        return true;
    }

    private static bool TryReadValueShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        ApiDescriptor descriptor,
        bool isAdd,
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
            || cursorResult.Target != TargetKind.Entity
            || !hasValues
            || arity < 2
            || cursorResult.ComponentIdCount != 0)
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
            isAdd ? StructuralOperation.Add : StructuralOperation.Set,
            TargetKind.Entity,
            arity: arity,
            hasValues: true);
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
        if (!ApiDescriptor.TryGet("Create", out ApiDescriptor descriptor)
            || !new InvocationCursor(model, arguments, descriptor).TryRead(-1, out InvocationCursorResult cursorResult))
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
            hasOutput: cursorResult.HasOutput);
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
            "Remove" => StructuralOperation.Remove,
            _ => StructuralOperation.Set
        };

}
