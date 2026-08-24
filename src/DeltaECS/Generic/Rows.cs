namespace Delta.ECS;

using System.Runtime.CompilerServices;

public ref partial struct ReadRow
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}

public ref partial struct WriteRow
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(in QuerySlots slots) => ref Ref<T>(slots.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}
