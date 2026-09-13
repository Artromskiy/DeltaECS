namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

internal ref partial struct ReadRow
{
    private readonly ref byte _data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadRow(Array row) => _data = ref ArrayAccess.DataReference(row);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref readonly T Ref<T>(int slotIndex)
        => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}

internal ref partial struct WriteRow
{
    private readonly ref byte _data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WriteRow(Array row) => _data = ref ArrayAccess.DataReference(row);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T Ref<T>(int slotIndex)
        => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}
