using System;
using System.Collections.Immutable;
using System.Linq;

namespace Delta.ECS.Generators;

/// <summary>Consumer-side file model produced after semantic API analysis.</summary>
internal sealed class GeneratedFileModel
{
    internal GeneratedFileModel(
        string namespaceName,
        ImmutableArray<string> usings,
        ImmutableArray<string> members)
    {
        Namespace = namespaceName;
        Usings = usings
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();
        Members = members;
    }

    internal GeneratedFileModel(
        string namespaceName,
        ImmutableArray<string> usings,
        ImmutableArray<RenderModel> members)
        : this(namespaceName, usings, members.Select(static member => member.Text).ToImmutableArray())
    {
    }

    internal string Namespace { get; }
    internal ImmutableArray<string> Usings { get; }
    internal ImmutableArray<string> Members { get; }
}

/// <summary>One renderable member and the semantic API model that produced it.</summary>
internal sealed class RenderModel
{
    internal RenderModel(ApiModel api, string text)
    {
        Api = api;
        Text = text;
    }

    internal ApiModel Api { get; }
    internal string Text { get; }
}

/// <summary>Render-only invoker description kept separate from semantic models.</summary>
internal sealed class InvokerModel
{
    internal InvokerModel(
        ApiModel api,
        string source)
    {
        Api = api;
        Source = source;
        Name = string.Empty;
        GenericParameters = string.Empty;
        Contract = string.Empty;
        Fields = ImmutableArray<string>.Empty;
        ConstructorParameters = ImmutableArray<string>.Empty;
        Assignments = ImmutableArray<string>.Empty;
        AccessInitializers = ImmutableArray<string>.Empty;
        ExecuteBody = string.Empty;
        InitializerBody = string.Empty;
    }

    internal InvokerModel(
        ApiModel api,
        string name,
        string genericParameters,
        string contract,
        ImmutableArray<string> fields,
        ImmutableArray<string> constructorParameters,
        ImmutableArray<string> assignments,
        ImmutableArray<string> accessInitializers,
        string executeBody,
        string initializerBody)
    {
        Api = api;
        Source = null;
        Name = name;
        GenericParameters = genericParameters;
        Contract = contract;
        Fields = fields;
        ConstructorParameters = constructorParameters;
        Assignments = assignments;
        AccessInitializers = accessInitializers;
        ExecuteBody = executeBody;
        InitializerBody = initializerBody;
    }

    internal ApiModel Api { get; }
    internal string? Source { get; }
    internal string Name { get; }
    internal string GenericParameters { get; }
    internal string Contract { get; }
    internal ImmutableArray<string> Fields { get; }
    internal ImmutableArray<string> ConstructorParameters { get; }
    internal ImmutableArray<string> Assignments { get; }
    internal ImmutableArray<string> AccessInitializers { get; }
    internal string ExecuteBody { get; }
    internal string InitializerBody { get; }
}
