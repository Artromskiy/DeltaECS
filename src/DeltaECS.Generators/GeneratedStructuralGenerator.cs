using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
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

        if (member.Name is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText switch
            {
                "Add" => TryReadValueShape(model, invocation, isAdd: true, out shape),
                "Set" => TryReadValueShape(model, invocation, isAdd: false, out shape),
                "Create" => TryReadExplicitCreateShape(model, invocation, out shape),
                _ => false
            };
        }

        if (member.Name is not GenericNameSyntax genericName
            || genericName.Identifier.ValueText is not ("Add" or "Remove" or "Set" or "Create"))
        {
            return false;
        }

        int arity = genericName.TypeArgumentList.Arguments.Count;
        if (arity < 1)
        {
            return false;
        }

        Receiver receiver = ReadReceiver(model.GetTypeInfo(member.Expression).Type);
        if (receiver == Receiver.None)
        {
            return false;
        }

        if (genericName.Identifier.ValueText == "Create")
        {
            if (receiver != Receiver.World)
            {
                return false;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
            int countIndex = arguments.Count - 1;
            bool hasOutput = countIndex >= 0
                && GeneratorSupport.IsEntityOutput(model.GetTypeInfo(arguments[countIndex].Expression).Type);
            if (hasOutput)
            {
                countIndex--;
            }

            if (countIndex >= 0 && GeneratorSupport.IsInt32(model.GetTypeInfo(arguments[countIndex].Expression).Type))
            {
                int selectorCount = countIndex;
                bool hasExplicitSelectors = selectorCount == arity;
                if (hasExplicitSelectors)
                {
                    for (int index = 0; index < selectorCount; index++)
                    {
                        if (!GeneratorSupport.IsComponentId(model.GetTypeInfo(arguments[index].Expression).Type))
                        {
                            hasExplicitSelectors = false;
                            break;
                        }
                    }
                }

                if (selectorCount != 0 && !hasExplicitSelectors)
                {
                    return false;
                }

                shape = new StructuralModel(
                    receiver,
                    hasOutput ? StructuralMode.CreateOutput : StructuralMode.Create,
                    false,
                    arity,
                    isExplicitIds: hasExplicitSelectors);
                return true;
            }

            if (arguments.Count == 0)
            {
                shape = new StructuralModel(receiver, StructuralMode.CreateSingle, false, arity);
                return true;
            }

            return false;
        }

        if (genericName.Identifier.ValueText is "Add" or "Set"
            && TryReadValueShape(
                model,
                invocation,
                genericName.Identifier.ValueText == "Add",
                out shape))
        {
            return true;
        }

        if (genericName.Identifier.ValueText == "Set")
        {
            return false;
        }

        StructuralMode mode;
        bool explicitIds = false;
        if (receiver == Receiver.World)
        {
            int argumentCount = invocation.ArgumentList.Arguments.Count;
            if (argumentCount != 1 && argumentCount != arity + 1)
            {
                return false;
            }

            ArgumentSyntax argument = invocation.ArgumentList.Arguments[0];
            if (argument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.InKeyword)
                && GeneratorSupport.IsNamedType(model.GetTypeInfo(argument.Expression).Type, "Query"))
            {
                mode = StructuralMode.Query;
            }
            else if (GeneratorSupport.IsEntityBatch(model.GetTypeInfo(argument.Expression).Type))
            {
                mode = StructuralMode.Entities;
            }
            else if (GeneratorSupport.IsEntityType(model.GetTypeInfo(argument.Expression).Type))
            {
                mode = StructuralMode.SingleEntity;
            }
            else
            {
                return false;
            }

            if (argumentCount == arity + 1)
            {
                for (int index = 1; index < argumentCount; index++)
                {
                    if (!GeneratorSupport.IsComponentId(model.GetTypeInfo(invocation.ArgumentList.Arguments[index].Expression).Type))
                    {
                        return false;
                    }
                }

                explicitIds = true;
            }
        }
        else
        {
            return false;
        }

        shape = new StructuralModel(
            receiver,
            mode,
            genericName.Identifier.ValueText == "Add",
            arity,
            isExplicitIds: explicitIds);
        return true;
    }

    private static bool TryReadValueShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool isAdd,
        out StructuralModel? shape)
    {
        shape = null;
        if (ReadReceiver(model.GetTypeInfo(((MemberAccessExpressionSyntax)invocation.Expression).Expression).Type)
            != Receiver.World)
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        int arity = arguments.Count - 1;
        if (arity < 2
            || !GeneratorSupport.IsEntityType(model.GetTypeInfo(arguments[0].Expression).Type))
        {
            return false;
        }

        for (int index = 1; index < arguments.Count; index++)
        {
            ITypeSymbol? valueType = model.GetTypeInfo(arguments[index].Expression).Type;
            if (valueType is null || GeneratorSupport.IsComponentId(valueType))
            {
                return false;
            }
        }

        shape = new StructuralModel(
            Receiver.World,
            StructuralMode.SingleEntity,
            isAdd,
            arity: arity,
            hasValues: true);
        return true;
    }

    private static bool TryReadExplicitCreateShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out StructuralModel? shape)
    {
        shape = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || ReadReceiver(model.GetTypeInfo(member.Expression).Type) != Receiver.World)
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        int countIndex = arguments.Count - 1;
        bool hasOutput = false;
        if (GeneratorSupport.IsEntityOutput(model.GetTypeInfo(arguments[countIndex].Expression).Type))
        {
            hasOutput = true;
            countIndex--;
        }

        if (countIndex < 1
            || !GeneratorSupport.IsInt32(model.GetTypeInfo(arguments[countIndex].Expression).Type))
        {
            return false;
        }

        for (int index = 0; index < countIndex; index++)
        {
            if (!GeneratorSupport.IsComponentId(model.GetTypeInfo(arguments[index].Expression).Type))
            {
                return false;
            }
        }

        shape = new StructuralModel(
            Receiver.World,
            hasOutput ? StructuralMode.ExplicitCreateOutput : StructuralMode.ExplicitCreate,
            isAdd: false,
            countIndex,
            isGeneric: false);
        return true;
    }

    private static Receiver ReadReceiver(ITypeSymbol? type)
    {
        string name = type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
        return name switch
        {
            "global::Delta.ECS.World" => Receiver.World,
            _ => Receiver.None
        };
    }


}
