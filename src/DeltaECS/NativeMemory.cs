namespace Delta.ECS;

using System.Runtime.CompilerServices;
using RuntimeNativeMemory = System.Runtime.InteropServices.NativeMemory;

/// <summary>Trusted native storage owned and disposed by its containing ECS object.</summary>
internal unsafe struct NativeMemory<T> : IDisposable where T : unmanaged
{
    private nint _address;
    private int _length;

    public NativeMemory(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _length = length;
        _address = Allocate(length);
    }

    public NativeMemory(ReadOnlySpan<T> source)
    {
        _length = source.Length;
        _address = Allocate(_length);
        source.CopyTo(Span);
    }

    public int Length => _length;

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref *(T*)_address, index);
    }

    public Span<T> Span => new((void*)_address, _length);

    public ReadOnlySpan<T> ReadOnlySpan => Span;

    public void Resize(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length == _length) return;

        nint replacement = Allocate(length);
        int copied = Math.Min(_length, length);
        if (copied != 0) Span[..copied].CopyTo(new Span<T>((void*)replacement, length));
        ReleaseBuffer();
        _address = replacement;
        _length = length;
    }

    public void Clear()
    {
        if (_length != 0) RuntimeNativeMemory.Clear((void*)_address, ByteLength(_length));
    }

    public void Dispose()
    {
        if (_address != 0) ReleaseBuffer();
        _length = 0;
    }

    private static nint Allocate(int length)
    {
        if (length == 0) return 0;
        nuint bytes = ByteLength(length);
        nint address = (nint)RuntimeNativeMemory.Alloc(bytes);
        RuntimeNativeMemory.Clear((void*)address, bytes);
        return address;
    }

    private static nuint ByteLength(int length) => checked((nuint)length * (nuint)Unsafe.SizeOf<T>());

    private void ReleaseBuffer()
    {
        RuntimeNativeMemory.Free((void*)_address);
        _address = 0;
    }
}
