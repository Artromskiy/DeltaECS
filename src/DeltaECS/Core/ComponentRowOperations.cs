namespace Delta.ECS;
using System;

internal readonly partial struct ComponentRowOperations
{
    private ComponentRowOperations(bool containsReferences)
        => ContainsReferences = containsReferences;

    public bool ContainsReferences { get; }

    public static ComponentRowOperations ForRuntimeType(bool containsReferences)
        => new(containsReferences);
}
