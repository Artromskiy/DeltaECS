namespace Delta.ECS;

using System;

public readonly struct Entity
{
    public int Index { get; }
}

public readonly struct ComponentId
{
}

public readonly struct Query
{
}

public readonly struct ReadAccess
{
}

public readonly struct WriteAccess
{
}

public sealed class ComponentLayoutRegistry
{
    public bool TryGetPrimary<T>(out ComponentId componentId)
    {
        componentId = default;
        return true;
    }

    public ComponentId GetPrimary<T>() => default;
}

public ref struct ReadValues
{
    public ref T Ref<T>(QueryChunkCursor cursor) => throw new NotImplementedException();
    public ref T Ref<T>(int index) => throw new NotImplementedException();
}

public ref struct QueryChunkCursor
{
    public ReadOnlySpan<Entity> Entities => default;
    public int CurrentIndex => 0;
    public bool MoveNext() => false;
    public ReadValues GetRead(ReadAccess access) => default;
    public ReadValues GetWrite(WriteAccess access) => default;
}

internal ref struct SequenceElementCursor
{
    public Entity Entity => default;
    public int Slot => 0;
    public ReadValues Get(ReadAccess access) => default;
    public ReadValues Get(WriteAccess access) => default;
}

internal interface IForEachInvoker
{
    void Invoke(ref QueryChunkCursor cursor);
}

internal interface ISequenceInvoker
{
    void Invoke(ref SequenceElementCursor cursor);
}

internal static class ForEachRuntime
{
    internal static ReadAccess AccessRead<T>(World world, in Query query, ComponentId component) => default;
    internal static WriteAccess AccessWrite<T>(World world, in Query query, ComponentId component) => default;
    internal static void ResolveComponentIds(in Query query, Span<ComponentId> destination) { }
    internal static void Execute<TInvoker>(World world, in Query query, ref TInvoker invoker, bool hasWrites)
        where TInvoker : struct, IForEachInvoker { }
}

public sealed partial class World
{
    public ComponentLayoutRegistry Layouts { get; } = new();
    public EntitySequence Entities(ReadOnlySpan<Entity> entities) => default;
    internal Query CreateSequenceQuery(ReadOnlySpan<ComponentId> components) => default;
    internal void ExecuteSequenceComponents<TInvoker>(ReadOnlySpan<Entity> entities, in Query query, ref TInvoker invoker, bool hasWrites)
        where TInvoker : struct, ISequenceInvoker { }
}

public readonly ref partial struct EntitySequence
{
    private readonly World _world;
    private readonly ReadOnlySpan<Entity> _entities;

    public FilteredEntitySequence Where(in Query query) => default;
}

public readonly ref partial struct FilteredEntitySequence
{
    private readonly World _world;
    private readonly ReadOnlySpan<Entity> _entities;
    private readonly Query _query;
}
