namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public ref partial struct ReadRow
{
    private readonly Array _row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadRow(Array row) => _row = row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(int slotIndex)
        => ref ArrayAccess.RefAt<T>(_row, slotIndex);
}

public ref partial struct WriteRow
{
    private readonly Array _row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WriteRow(Array row) => _row = row;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(int slotIndex)
        => ref ArrayAccess.RefAt<T>(_row, slotIndex);
}
