namespace Delta.ECS;

using System.Runtime.CompilerServices;

public ref partial struct ReadValues
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(QuerySlots slots) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slots.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}

public ref partial struct WriteValues
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QueryChunkCursor cursor) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), cursor.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(QuerySlots slots) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slots.CurrentIndex);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(int slotIndex) => ref Unsafe.Add(ref Unsafe.As<byte, T>(ref _data), slotIndex);
}
