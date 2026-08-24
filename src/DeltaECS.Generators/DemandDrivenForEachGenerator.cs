using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Delta.ECS.Generators;

/// <summary>
/// Generates only the ForEach shapes actually used by a consumer assembly.
/// </summary>
[Generator]
public sealed class DemandDrivenForEachGenerator : IIncrementalGenerator
{
    private const int FirstDemandArity = 1;
    private const int MaxArity = 256;

    private static readonly DiagnosticDescriptor Unsupported = new(
        "DECSGEN001",
        "Unsupported ForEach shape",
        "ForEach shape '{0}' is not supported by the demand-driven generator",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor TooManyComponents = new(
        "DECSGEN002",
        "ForEach arity is too large",
        "ForEach arity {0} exceeds the supported demand-driven limit of {1}",
        "ForEach",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (productionContext, compilation) => Execute(compilation, productionContext));
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var shapes = new Dictionary<string, Shape>(StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (tree.FilePath.EndsWith("ForEach.g.cs", StringComparison.Ordinal)
                || tree.FilePath.Contains("DemandForEach_", StringComparison.Ordinal))
            {
                continue;
            }

            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryReadShape(model, invocation, out Shape? shape, out Diagnostic? diagnostic))
                {
                    if (diagnostic is not null)
                    {
                        context.ReportDiagnostic(diagnostic);
                    }

                    continue;
                }

                if (shape is null)
                {
                    continue;
                }

                Shape candidate = shape;
                string key = candidate.Key;
                if (!shapes.ContainsKey(key))
                {
                    shapes.Add(key, candidate);
                }
            }
        }

        foreach (IGrouping<string, Shape> patternGroup in shapes.Values
            .OrderBy(static value => value.Key, StringComparer.Ordinal)
            .GroupBy(static value => value.Pattern, StringComparer.Ordinal))
        {
            bool renderContracts = true;
            foreach (Shape shape in patternGroup)
            {
                context.AddSource(
                    $"DemandForEach_{StableName(shape.Key)}.g.cs",
                    Render(shape, renderContracts));
                renderContracts = false;
            }
        }
    }

    private static bool TryReadShape(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        out Shape? shape,
        out Diagnostic? diagnostic)
    {
        shape = null;
        diagnostic = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || member.Name.Identifier.ValueText is not ("ForEach" or "ForEachEntity")
            || member.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        ITypeSymbol? receiverType = model.GetTypeInfo(member.Expression).Type;
        ReceiverKind receiver = ReceiverKindFrom(receiverType);
        if (receiver == ReceiverKind.None)
        {
            return false;
        }

        int genericCount = genericName.TypeArgumentList.Arguments.Count;
        var arguments = invocation.ArgumentList.Arguments;
        bool namedEntity = member.Name.Identifier.ValueText == "ForEachEntity";
        bool hasLambda = arguments.Any(static argument => argument.Expression is LambdaExpressionSyntax);
        int refArgumentCount = arguments.Count(static argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
        bool isFunctor = !hasLambda;
        bool hasContext = isFunctor ? refArgumentCount >= 2 : refArgumentCount >= 1;
        int prefixCount = (isFunctor ? (hasContext ? 2 : 1) : (hasContext ? 1 : 0));
        int componentCount = genericCount - prefixCount;
        if (componentCount < FirstDemandArity)
        {
            return false;
        }

        if (componentCount > MaxArity)
        {
            diagnostic = Diagnostic.Create(TooManyComponents, invocation.GetLocation(), componentCount, MaxArity);
            return false;
        }

        string? accessPattern = isFunctor
            ? InferFunctorPattern(
                model,
                genericName.TypeArgumentList.Arguments[hasContext ? 1 : 0],
                componentCount,
                hasContext,
                namedEntity)
            : InferPattern(arguments, componentCount, hasContext);

        if (accessPattern is null || accessPattern.Length != componentCount || accessPattern.Any(static c => c is not ('R' or 'W')))
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        bool explicitIds = CountComponentIds(model, arguments) == componentCount;
        if (!explicitIds && CountComponentIds(model, arguments) != 0)
        {
            diagnostic = Diagnostic.Create(Unsupported, invocation.GetLocation(), invocation);
            return false;
        }

        bool sequence = receiver is ReceiverKind.EntitySequence or ReceiverKind.FilteredEntitySequence;
        var typeArguments = genericName.TypeArgumentList.Arguments
            .Select(static argument => argument.ToString())
            .ToArray();
        int componentStart = prefixCount;
        var components = typeArguments.Skip(componentStart).ToArray();

        shape = new Shape(
            receiver,
            sequence,
            explicitIds,
            namedEntity || LambdaHasEntity(model, arguments, componentCount, hasContext, isFunctor),
            hasContext,
            isFunctor,
            accessPattern,
            components);
        return true;
    }

    private static int CountComponentIds(SemanticModel model, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        int count = 0;
        foreach (ArgumentSyntax argument in arguments)
        {
            ITypeSymbol? type = model.GetTypeInfo(argument.Expression).Type;
            if (type?.Name == "ComponentId" && type.ContainingNamespace.ToDisplayString() == "Delta.ECS")
            {
                count++;
            }
        }

        return count;
    }

    private static string? InferPattern(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int componentCount,
        bool hasContext)
    {
        LambdaExpressionSyntax? lambda = arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
        if (lambda is null)
        {
            return new string('W', componentCount);
        }

        var parameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter },
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.ToArray(),
            _ => Array.Empty<ParameterSyntax>()
        };
        int start = hasContext ? 1 : 0;
        if (parameters.Length == componentCount + start + 1)
        {
            start++;
        }

        var result = new char[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            result[index] = parameters.Length > index + start
                && parameters[index + start].Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InKeyword))
                ? 'R'
                : 'W';
        }

        return new string(result);
    }

    private static string? InferFunctorPattern(
        SemanticModel model,
        TypeSyntax functorType,
        int componentCount,
        bool hasContext,
        bool hasEntity)
    {
        ITypeSymbol? type = model.GetTypeInfo(functorType).Type;
        if (type is null)
        {
            return null;
        }

        int prefixCount = (hasContext ? 1 : 0) + (hasEntity ? 1 : 0);
        IMethodSymbol? invoke = type.GetMembers("Invoke")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => method.Parameters.Length == componentCount + prefixCount);
        if (invoke is null)
        {
            return null;
        }

        var pattern = new char[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            RefKind refKind = invoke.Parameters[index + prefixCount].RefKind;
            if (refKind is not (RefKind.In or RefKind.Ref))
            {
                return null;
            }

            pattern[index] = refKind == RefKind.In ? 'R' : 'W';
        }

        return new string(pattern);
    }

    private static bool LambdaHasEntity(
        SemanticModel model,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int componentCount,
        bool hasContext,
        bool isFunctor)
    {
        if (isFunctor)
        {
            return false;
        }

        LambdaExpressionSyntax? lambda = arguments
            .Select(static argument => argument.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
        if (lambda is not ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            return false;
        }

        int expected = componentCount + (hasContext ? 1 : 0);
        if (parenthesized.ParameterList.Parameters.Count != expected + 1)
        {
            return false;
        }

        int entityIndex = hasContext ? 1 : 0;
        TypeSyntax? entityType = parenthesized.ParameterList.Parameters[entityIndex].Type;
        if (entityType is null)
        {
            return false;
        }

        ITypeSymbol? type = model.GetTypeInfo(entityType).Type;
        return type?.Name == "Entity" && type.ContainingNamespace.ToDisplayString() == "Delta.ECS";
    }

    private static ReceiverKind ReceiverKindFrom(ITypeSymbol? type)
    {
        string name = type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
        return name switch
        {
            "global::Delta.ECS.World" => ReceiverKind.World,
            "global::Delta.ECS.EntitySequence" => ReceiverKind.EntitySequence,
            "global::Delta.ECS.FilteredEntitySequence" => ReceiverKind.FilteredEntitySequence,
            _ => ReceiverKind.None
        };
    }

    private static string Render(Shape shape, bool renderContracts)
    {
        var source = new StringBuilder(32 * 1024);
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using System;");
        source.AppendLine("namespace Delta.ECS;");
        source.AppendLine();
        if (renderContracts)
        {
            RenderContracts(source, shape);
        }
        RenderInvoker(source, shape);
        RenderExtensions(source, shape);
        return source.ToString();
    }

    private static void RenderContracts(StringBuilder source, Shape shape)
    {
        string generic = GenericTypes(shape.Components.Length);
        string parameters = RefParameters(shape.Pattern);
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        source.Append("public delegate void ForEachAction").Append(suffix).Append('<').Append(generic).Append(">(")
            .Append(parameters).AppendLine(");");
        source.Append("public delegate void ForEachEntityAction").Append(suffix).Append('<').Append(generic).Append(">(Entity entity, ")
            .Append(parameters).AppendLine(");");
        source.Append("public delegate void ForEachContextAction").Append(suffix).Append("<TContext, ").Append(generic).Append(">(ref TContext context, ")
            .Append(parameters).AppendLine(");");
        source.Append("public delegate void ForEachContextEntityAction").Append(suffix).Append("<TContext, ").Append(generic).Append(">(ref TContext context, Entity entity, ")
            .Append(parameters).AppendLine(");");
        source.Append("public interface IForEach").Append(suffix).Append('<').Append(generic).Append("> { void Invoke(")
            .Append(parameters).AppendLine("); }");
        source.Append("public interface IForEachEntity").Append(suffix).Append('<').Append(generic).Append("> { void Invoke(Entity entity, ")
            .Append(parameters).AppendLine("); }");
        source.Append("public interface IForEachContext").Append(suffix).Append("<TContext, ").Append(generic).Append("> { void Invoke(ref TContext context, ")
            .Append(parameters).AppendLine("); }");
        source.Append("public interface IForEachContextEntity").Append(suffix).Append("<TContext, ").Append(generic).Append("> { void Invoke(ref TContext context, Entity entity, ")
            .Append(parameters).AppendLine("); }");
    }

    private static void RenderInvoker(StringBuilder source, Shape shape)
    {
        string generic = GenericTypes(shape.Components.Length);
        string name = InvokerName(shape);
        string actionType = ActionType(shape);
        string stateGeneric = shape.HasContext && shape.IsFunctor
            ? $"<TContext, TFunctor, {generic}>"
            : shape.IsFunctor
                ? $"<TFunctor, {generic}>"
                : shape.HasContext
                    ? $"<TContext, {generic}>"
                    : $"<{generic}>";
        source.Append("internal struct ").Append(name).Append(stateGeneric).AppendLine(" : IGeneratedForEachInvoker, IGeneratedSequenceInvoker");
        if (shape.IsFunctor)
        {
            source.Append("    where TFunctor : struct, ").Append(FunctorInterface(shape)).AppendLine();
        }

        source.AppendLine("{");
        if (shape.HasContext)
        {
            source.AppendLine("    private TContext _context;");
        }

        if (shape.IsFunctor)
        {
            source.AppendLine("    private TFunctor _functor;");
        }
        else
        {
            source.Append("    private readonly ").Append(actionType).AppendLine(" _action;");
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("    private readonly ").Append(shape.Pattern[index] == 'R' ? "ReadAccess" : "WriteAccess")
                .Append(" _access").Append(index).AppendLine(";");
        }

        source.Append("    internal ").Append(ConstructorName(name)).Append('(');
        if (shape.HasContext)
        {
            source.Append("TContext context, ");
        }

        if (shape.IsFunctor)
        {
            source.Append("TFunctor functor, ");
        }
        else
        {
            source.Append(actionType).Append(" action, ");
        }

        source.Append(AccessParameters(shape.Pattern)).AppendLine(")");
        source.AppendLine("    {");
        if (shape.HasContext)
        {
            source.AppendLine("        _context = context;");
        }

        if (shape.IsFunctor)
        {
            source.AppendLine("        _functor = functor;");
        }
        else
        {
            source.AppendLine("        _action = action;");
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("        _access").Append(index).Append(" = access").Append(index).AppendLine(";");
        }
        source.AppendLine("    }");

        RenderDenseInvoke(source, shape);
        RenderSequenceInvoke(source, shape);
        if (shape.HasContext)
        {
            source.AppendLine("    internal TContext Context => _context;");
        }

        if (shape.IsFunctor)
        {
            source.AppendLine("    internal TFunctor Functor => _functor;");
        }

        source.AppendLine("}");
    }

    private static void RenderDenseInvoke(StringBuilder source, Shape shape)
    {
        source.AppendLine("    public void Invoke(ref QueryChunkCursor cursor)");
        source.AppendLine("    {");
        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("        var values").Append(index).Append(" = cursor.").Append(shape.Pattern[index] == 'R' ? "GetRead" : "GetWrite")
                .Append("(_access").Append(index).AppendLine(");");
        }
        source.AppendLine("        while (cursor.MoveNext())");
        source.AppendLine("        {");
        source.Append("            ");
        AppendInvocation(source, shape, "values", "cursor", sequence: false);
        source.AppendLine(";");
        source.AppendLine("        }");
        source.AppendLine("    }");
    }

    private static void RenderSequenceInvoke(StringBuilder source, Shape shape)
    {
        source.AppendLine("    public void Invoke(ref GeneratedSequenceCursor cursor)");
        source.AppendLine("    {");
        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            source.Append("        var values").Append(index).Append(" = cursor.Get(_access").Append(index).AppendLine(");");
        }
        source.Append("        ");
        AppendInvocation(source, shape, "values", "cursor.Slot", sequence: true);
        source.AppendLine(";");
        source.AppendLine("    }");
    }

    private static void AppendInvocation(StringBuilder source, Shape shape, string valuesPrefix, string cursor, bool sequence)
    {
        string? context = shape.HasContext ? "ref _context, " : null;
        string entity = shape.HasEntity
            ? (sequence ? "cursor.Entity, " : "cursor.Entities[cursor.CurrentIndex], ")
            : string.Empty;
        if (shape.IsFunctor)
        {
            source.Append("_functor.Invoke(").Append(context ?? string.Empty).Append(entity);
        }
        else
        {
            source.Append("_action(").Append(context ?? string.Empty).Append(entity);
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            if (index > 0)
            {
                source.Append(", ");
            }

            source.Append(shape.Pattern[index] == 'R' ? "in " : "ref ").Append(valuesPrefix).Append(index).Append(".Ref<")
                .Append("T").Append(index + 1).Append(">(").Append(cursor).Append(')');
        }

        source.Append(')');
    }

    private static void RenderExtensions(StringBuilder source, Shape shape)
    {
        string className = "DemandForEachExtensions_" + StableName(shape.Key);
        string generic = GenericTypes(shape.Components.Length);
        string ids = shape.ExplicitIds ? ComponentParameters(shape.Components.Length) : string.Empty;
        string idArguments = shape.ExplicitIds ? ComponentNames(shape.Components.Length) : PrimaryArguments(shape);
        string accessArguments = AccessArguments(shape.Pattern);
        string setup = AccessSetup(shape, idArguments);
        string name = InvokerName(shape);
        string stateGeneric = StateGeneric(shape, generic);
        string callback = ActionType(shape);
        string receiver = shape.Sequence ? (shape.Receiver == ReceiverKind.FilteredEntitySequence ? "FilteredEntitySequence" : "EntitySequence") : "World";
        string prefix = shape.Sequence ? $"this {receiver} sequence" : "this World world";
        string query = shape.Sequence ? string.Empty : ", in Query query";
        string contextParameter = shape.HasContext ? ", ref TContext context" : string.Empty;
        string genericPrefix = shape.IsFunctor
            ? (shape.HasContext ? $"<TContext, TFunctor, {generic}>" : $"<TFunctor, {generic}>")
            : (shape.HasContext ? $"<TContext, {generic}>" : $"<{generic}>");
        source.Append("public static class ").Append(className).AppendLine();
        source.AppendLine("{");
        RenderExtensionMethod(source, shape, prefix, query, contextParameter, ids, callback, name, stateGeneric, genericPrefix, setup, accessArguments);
        source.AppendLine("}");
    }

    private static void RenderExtensionMethod(
        StringBuilder source,
        Shape shape,
        string prefix,
        string query,
        string contextParameter,
        string ids,
        string callback,
        string invokerName,
        string stateGeneric,
        string genericPrefix,
        string setup,
        string accessArguments)
    {
        string componentPart = string.IsNullOrEmpty(ids) ? string.Empty : $", {ids}";
        string methodName = shape.HasEntity ? "ForEachEntity" : "ForEach";
        if (shape.IsFunctor)
        {
            const string functor = ", ref TFunctor functor";
            string signature = $"public static void {methodName}{genericPrefix}({prefix}{query}{contextParameter}{componentPart}{functor})";
            string body = BuildBody(shape, accessArguments, invokerName, stateGeneric, setup, "functor", hasAction: false);
            AppendMethod(source, signature, body, FunctorConstraint(shape));
            return;
        }

        string callbackParameter = $", {callback} action";
        string signatureDelegate = $"public static void {methodName}{genericPrefix}({prefix}{query}{contextParameter}{componentPart}{callbackParameter})";
        string delegateBody = BuildBody(shape, accessArguments, invokerName, stateGeneric, setup, "action", hasAction: true);
        AppendMethod(source, signatureDelegate, delegateBody, null);
    }

    private static string BuildBody(Shape shape, string accessArguments, string invokerName, string stateGeneric, string setup, string callbackName, bool hasAction)
    {
        string[] access = accessArguments.Split(new[] { ", " }, StringSplitOptions.None);
        var accesses = new StringBuilder();
        for (int index = 0; index < access.Length; index++)
        {
            if (index > 0)
            {
                accesses.Append(", ");
            }

            accesses.Append(access[index]);
        }

        string invoke = shape.Sequence
            ? "sequence.GeneratedWorld.ExecuteGeneratedSequence(sequence.GeneratedEntities, in query, ref invoker, hasWrites: " + BoolHasWrites(shape.Pattern) + ");"
            : "world.ExecuteGeneratedForEach(in query, ref invoker, hasWrites: " + BoolHasWrites(shape.Pattern) + ");";
        var body = new StringBuilder();
        body.AppendLine("{");
        if (hasAction)
        {
            body.AppendLine("    ArgumentNullException.ThrowIfNull(action);");
        }

        body.Append("    ").Append(setup);
        body.Append("    ").Append("var invoker = new ").Append(invokerName).Append(stateGeneric).Append('(');
        if (shape.HasContext)
        {
            body.Append("context, ");
        }

        body.Append(callbackName).Append(", ").Append(accesses).AppendLine(");");
        body.Append("    ").Append(invoke).AppendLine();
        if (shape.HasContext)
        {
            body.AppendLine("    context = invoker.Context;");
        }

        if (shape.IsFunctor)
        {
            body.AppendLine("    functor = invoker.Functor;");
        }

        body.AppendLine("}");
        return body.ToString();
    }

    private static void AppendMethod(StringBuilder source, string signature, string body, string? constraint)
    {
        source.AppendLine("    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("    ").AppendLine(signature);
        if (constraint is not null)
        {
            source.Append("        ").AppendLine(constraint);
        }

        foreach (string line in body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            source.Append("    ").AppendLine(line);
        }
    }

    private static string AccessSetup(Shape shape, string ids)
    {
        string query = string.Empty;
        if (shape.Receiver == ReceiverKind.FilteredEntitySequence)
        {
            query = "Query query = sequence.GeneratedQuery;";
        }
        else if (shape.Receiver == ReceiverKind.EntitySequence)
        {
            query = "Query query = GeneratedForEachRuntime.CreateSequenceQuery(sequence.GeneratedWorld, stackalloc ComponentId[] { " + ids + " });";
        }

        string owner = shape.Sequence ? "sequence.GeneratedWorld" : "world";
        var result = new StringBuilder();
        if (query.Length > 0)
        {
            result.Append(query).Append(' ');
        }

        for (int index = 0; index < shape.Pattern.Length; index++)
        {
            result.Append("var access").Append(index).Append(" = GeneratedForEachRuntime.Access")
                .Append(shape.Pattern[index] == 'R' ? "Read" : "Write")
                .Append('(').Append(owner).Append(", in query, ").Append(ComponentArgument(ids, index)).Append(", typeof(T")
                .Append(index + 1).AppendLine(") ); ");
        }

        return result.ToString();
    }

    private static string ComponentArgument(string ids, int index)
    {
        string[] values = ids.Split(new[] { ", " }, StringSplitOptions.None);
        return values[index];
    }

    private static string PrimaryArguments(Shape shape)
    {
        var result = new string[shape.Components.Length];
        string owner = shape.Sequence ? "sequence.GeneratedWorld" : "world";
        for (int index = 0; index < shape.Components.Length; index++)
        {
            result[index] = owner + ".Layouts.GetId(typeof(T" + (index + 1) + "))";
        }

        return string.Join(", ", result);
    }

    private static string AccessArguments(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = "access" + index;
        }

        return string.Join(", ", result);
    }

    private static string ComponentParameters(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "ComponentId component" + index;
        }

        return string.Join(", ", result);
    }

    private static string ComponentNames(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "component" + index;
        }

        return string.Join(", ", result);
    }

    private static string RefParameters(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = (pattern[index] == 'R' ? "in " : "ref ") + "T" + (index + 1) + " component" + index;
        }

        return string.Join(", ", result);
    }

    private static string AccessParameters(string pattern)
    {
        var result = new string[pattern.Length];
        for (int index = 0; index < pattern.Length; index++)
        {
            result[index] = (pattern[index] == 'R' ? "ReadAccess" : "WriteAccess") + " access" + index;
        }

        return string.Join(", ", result);
    }

    private static string ActionType(Shape shape)
    {
        string generic = GenericTypes(shape.Components.Length);
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        if (shape.HasContext)
        {
            return shape.HasEntity ? $"ForEachContextEntityAction{suffix}<TContext, {generic}>" : $"ForEachContextAction{suffix}<TContext, {generic}>";
        }

        return shape.HasEntity ? $"ForEachEntityAction{suffix}<{generic}>" : $"ForEachAction{suffix}<{generic}>";
    }

    private static string FunctorInterface(Shape shape)
    {
        string generic = GenericTypes(shape.Components.Length);
        string suffix = IsAllWrite(shape.Pattern) ? string.Empty : "_" + shape.Pattern;
        if (shape.HasContext)
        {
            return shape.HasEntity ? $"IForEachContextEntity{suffix}<TContext, {generic}>" : $"IForEachContext{suffix}<TContext, {generic}>";
        }

        return shape.HasEntity ? $"IForEachEntity{suffix}<{generic}>" : $"IForEach{suffix}<{generic}>";
    }

    private static string StateGeneric(Shape shape, string generic)
        => shape.HasContext && shape.IsFunctor ? $"<TContext, TFunctor, {generic}>" : shape.IsFunctor ? $"<TFunctor, {generic}>" : shape.HasContext ? $"<TContext, {generic}>" : $"<{generic}>";

    private static string InvokerName(Shape shape) => "DemandForEachInvoker_" + StableName(shape.Key);

    private static string GenericTypes(int arity)
    {
        var result = new string[arity];
        for (int index = 0; index < arity; index++)
        {
            result[index] = "T" + (index + 1);
        }

        return string.Join(", ", result);
    }

    private static string ConstructorName(string name)
    {
        int index = name.IndexOf('<');
        return index < 0 ? name : name.Substring(0, index);
    }

    private static string FunctorConstraint(Shape shape) => $"where TFunctor : struct, {FunctorInterface(shape)}";

    private static bool IsAllWrite(string pattern) => pattern.All(static value => value == 'W');

    private static string BoolHasWrites(string pattern) => pattern.Contains('W') ? "true" : "false";

    private static string StableName(string key)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char value in key)
            {
                hash = (hash ^ value) * 16777619;
            }

            return hash.ToString("X8");
        }
    }

    private enum ReceiverKind
    {
        None,
        World,
        EntitySequence,
        FilteredEntitySequence
    }

    private sealed class Shape
    {
        public Shape(ReceiverKind receiver, bool sequence, bool explicitIds, bool hasEntity, bool hasContext, bool isFunctor, string pattern, string[] components)
        {
            Receiver = receiver;
            Sequence = sequence;
            ExplicitIds = explicitIds;
            HasEntity = hasEntity;
            HasContext = hasContext;
            IsFunctor = isFunctor;
            Pattern = pattern;
            Components = components;
        }

        public ReceiverKind Receiver { get; }
        public bool Sequence { get; }
        public bool ExplicitIds { get; }
        public bool HasEntity { get; }
        public bool HasContext { get; }
        public bool IsFunctor { get; }
        public string Pattern { get; }
        public string[] Components { get; }
        public string Key => $"{Receiver}|{ExplicitIds}|{HasEntity}|{HasContext}|{IsFunctor}|{Pattern}";
    }
}
