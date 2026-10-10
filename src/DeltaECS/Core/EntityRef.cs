using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Delta.ECS;

/// <summary>A scoped entity handle for an entity-aware iteration callback.</summary>
/// <remarks>Component access uses trusted world paths while this scoped view is valid.</remarks>
public ref struct EntityRef
{
    private Span<GeneratedEntityRefView> _view;

    internal EntityRef(Span<GeneratedEntityRefView> view)
    {
        _view = view;
    }

    private ref GeneratedEntityRefView View => ref MemoryMarshal.GetReference(_view);

    /// <summary>Converts this scoped view to its stable entity handle.</summary>
    public static implicit operator Entity(EntityRef entity) => entity.View.CurrentEntity;

    /// <summary>Gets the current entity's index.</summary>
    public int Index => View.CurrentEntity.Index;

    /// <summary>Gets the current entity's lifetime generation.</summary>
    public int Generation => View.CurrentEntity.Generation;

    /// <summary>Reports whether the current entity has the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(ComponentId componentId) => View.World!.HasTrusted(ref View, componentId);

    /// <summary>Reports whether the current entity has the primary registration for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() => View.World!.HasTrusted<T>(ref View);

    /// <summary>Reports whether the current entity has the specified registration of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>(ComponentId componentId) => View.World!.HasTrusted<T>(ref View, componentId);

    /// <summary>Attempts to read a component by registration, returning its default value when absent or mismatched.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(ComponentId componentId, out T value) => View.World!.TryGetTrusted(ref View, componentId, out value);

    /// <summary>Attempts to read the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(out T value) => View.World!.TryGetTrusted(ref View, out value);

    /// <summary>Reads the primary component for <typeparamref name="T"/> or throws when it is missing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() => View.World!.GetTrusted<T>(ref View);

    /// <summary>Reads the specified component registration as <typeparamref name="T"/> or throws when it is missing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(ComponentId componentId) => View.World!.GetTrusted<T>(ref View, componentId);

    /// <summary>Gets a writable component reference and marks its change stamp.</summary>
    /// <remarks>For a tag, this returns the shared default placeholder; writes through it are not stored.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(ComponentId componentId) => ref View.World!.GetRefTrusted<T>(ref View, componentId);

    /// <summary>Gets a writable reference to the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>() => ref View.World!.GetRefTrusted<T>(ref View);

    /// <summary>Gets a read-only reference to the primary component for <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetReadRef<T>() => ref View.World!.GetReadRefTrusted<T>(ref View);

    /// <summary>Gets a read-only reference to the specified component registration as <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetReadRef<T>(ComponentId componentId) => ref View.World!.GetReadRefTrusted<T>(ref View, componentId);

    /// <summary>Attempts to read the stamp for the primary component of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp<T>(out Stamp stamp) => View.World!.TryGetComponentStampTrusted<T>(ref View, out stamp);

    /// <summary>Attempts to read the stamp for the specified component registration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp(ComponentId componentId, out Stamp stamp)
        => View.World!.TryGetComponentStampTrusted(ref View, componentId, out stamp);

    /// <summary>Attempts to read the stamp for the specified registration of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComponentStamp<T>(ComponentId componentId, out Stamp stamp)
        => View.World!.TryGetComponentStampTrusted<T>(ref View, componentId, out stamp);
}

/// <summary>Stack-owned backing view for a scoped entity reference.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public struct GeneratedEntityRefView
{
    internal World? World;
    internal Chunk? Chunk;
    internal int SlotIndex;

    internal readonly Entity CurrentEntity => Chunk!.RawEntities.RefAt(SlotIndex);

    internal void SetWorld(World world) => World = world;

    internal void SetChunk(Chunk chunk) => Chunk = chunk;

    internal void SetSlot(int slotIndex) => SlotIndex = slotIndex;

    internal void SetLocation(Chunk chunk, int slotIndex)
    {
        Chunk = chunk;
        SlotIndex = slotIndex;
    }
}
