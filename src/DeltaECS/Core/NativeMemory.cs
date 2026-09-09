namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Trusted native storage owned and disposed by its containing ECS object.</summary>
internal unsafe struct NativeMemory<T> : IDisposable where T : unmanaged
{
    private nint _address;
    private int _length;

    internal NativeMemory(int length)
    {
        ThrowHelper.ThrowIfNegative(length, nameof(length));
        _length = length;
        _address = Allocate(length);
    }

    internal NativeMemory(ReadOnlySpan<T> source)
    {
        _length = source.Length;
        _address = Allocate(_length);
        source.CopyTo(Span);
    }

    internal int Length => _length;

    internal nint Address => _address;

    internal ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref RefAt(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T RefAt(int index) => ref Unsafe.Add(ref *(T*)_address, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetRefAtZero() => ref *(T*)_address;

    internal Span<T> Span => new((void*)_address, _length);

    internal ReadOnlySpan<T> ReadOnlySpan => Span;

    internal void Resize(int length)
    {
        ThrowHelper.ThrowIfNegative(length, nameof(length));
        if (length == _length) return;

        nint replacement = Allocate(length);
        int copied = Math.Min(_length, length);
        if (copied != 0) Span[..copied].CopyTo(new Span<T>((void*)replacement, length));
        ReleaseBuffer();
        _address = replacement;
        _length = length;
    }

    internal void Clear()
    {
        if (_length != 0) NativeMemoryCompat.Clear((void*)_address, ByteLength(_length));
    }

    internal void Dispose()
    {
        if (_address != 0) ReleaseBuffer();
        _length = 0;
    }

    void IDisposable.Dispose() => Dispose();

    private static nint Allocate(int length)
    {
        if (length == 0) return 0;
        nuint bytes = ByteLength(length);
        nint address = NativeMemoryCompat.Alloc(bytes);
        NativeMemoryCompat.Clear((void*)address, bytes);
        return address;
    }

    private static nuint ByteLength(int length) => checked((nuint)length * (nuint)Unsafe.SizeOf<T>());

    private void ReleaseBuffer()
    {
        NativeMemoryCompat.Free((void*)_address);
        _address = 0;
    }
}
