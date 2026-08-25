namespace Delta.ECS;
using System;

internal readonly partial struct ComponentRowOperations
{
    private ComponentRowOperations(bool containsReferences)
        => ContainsReferences = containsReferences;

    internal bool ContainsReferences { get; }

    internal static ComponentRowOperations ForRuntimeType(bool containsReferences)
        => new(containsReferences);
}
