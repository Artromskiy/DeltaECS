namespace Delta.ECS;

using System.Buffers;
using System.Runtime.CompilerServices;
using RuntimeNativeMemory = System.Runtime.InteropServices.NativeMemory;

internal unsafe struct NativeMemory<T> : IDisposable where T : unmanaged
{
    private NativeMemoryManager<T>? _owner;
    private int _length;
    public NativeMemory(int length) { ArgumentOutOfRangeException.ThrowIfNegative(length); _length = length; _owner = new(length); }
    public NativeMemory(ReadOnlySpan<T> source) { _length = source.Length; _owner = new(source.Length); source.CopyTo(_owner.Memory.Span); }
    public int Length => _length;
    public ref T this[int index] { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref Span[index]; }
    public Span<T> Span => _owner!.Memory.Span;
    public ReadOnlySpan<T> ReadOnlySpan => Span;
    public void Resize(int length) { ArgumentOutOfRangeException.ThrowIfNegative(length); if (length == _length) return; var replacement = new NativeMemoryManager<T>(length); Span[..Math.Min(_length, length)].CopyTo(replacement.Memory.Span); _owner!.Release(); _owner = replacement; _length = length; }
    public void Clear() => RuntimeNativeMemory.Clear((void*)_owner!.Address, ByteLength(_length));
    public void Dispose() { _owner?.Release(); _owner = null; _length = 0; }
    private static nuint ByteLength(int length) => checked((nuint)length * (nuint)Unsafe.SizeOf<T>());
}

/// <remarks>GetSpan intentionally has no disposed branch; World owns lifetime.</remarks>
internal unsafe sealed class NativeMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private nint _address;
    private readonly int _length;
    public NativeMemoryManager(int length) { _length = length; if (length != 0) { _address = (nint)RuntimeNativeMemory.Alloc(ByteLength(length)); RuntimeNativeMemory.Clear((void*)_address, ByteLength(length)); } }
    public nint Address => _address;
    public void Release() => Dispose(true);
    public override Span<T> GetSpan() => new((void*)_address, _length);
    public override MemoryHandle Pin(int elementIndex = 0) { ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, _length); return new MemoryHandle((byte*)_address + (nuint)elementIndex * (nuint)Unsafe.SizeOf<T>()); }
    public override void Unpin() { }
    protected override void Dispose(bool disposing) { if (_address != 0) { RuntimeNativeMemory.Free((void*)_address); _address = 0; } }
    private static nuint ByteLength(int length) => checked((nuint)length * (nuint)Unsafe.SizeOf<T>());
}
