namespace Delta.ECS;

using System.Runtime.CompilerServices;

/// <summary>Trusted native storage owned and disposed by its containing ECS object.</summary>
internal struct NativeMemory<T> : IDisposable where T : unmanaged
{

    internal int Length { get; private set; }

    internal nint Address { get; private set; }

    internal readonly Span<T> Span => ArrayAccess.AsSpan<T>(Address, Length);

    internal readonly ReadOnlySpan<T> ReadOnlySpan => Span;
    internal NativeMemory(int length)
    {
        ThrowHelper.ThrowIfNegative(length, nameof(length));
        Length = length;
        Address = Allocate(length);
    }

    internal NativeMemory(ReadOnlySpan<T> source)
    {
        Length = source.Length;
        Address = Allocate(Length);
        source.CopyTo(Span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ref T RefAt(int index) => ref ArrayAccess.RefAt<T>(Address, index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ref T GetRefAtZero() => ref ArrayAccess.GetRefAtZero<T>(Address);

    internal void Resize(int length)
    {
        ThrowHelper.ThrowIfNegative(length, nameof(length));
        if (length == Length)
        {
            return;
        }

        nint replacement = Allocate(length);
        int copied = Math.Min(Length, length);
        if (copied != 0)
        {
            Span[..copied].CopyTo(ArrayAccess.AsSpan<T>(replacement, length));
        }

        ReleaseBuffer();
        Address = replacement;
        Length = length;
    }

    internal readonly void Clear()
    {
        if (Length != 0)
        {
            NativeMemoryCompat.Clear(Address, ByteLength(Length));
        }
    }

    internal void Dispose()
    {
        if (Address != 0)
        {
            ReleaseBuffer();
        }

        Length = 0;
    }

    private static nint Allocate(int length)
    {
        if (length == 0)
        {
            return 0;
        }

        nuint bytes = ByteLength(length);
        nint address = NativeMemoryCompat.Alloc(bytes);
        NativeMemoryCompat.Clear(address, bytes);
        return address;
    }

    private static nuint ByteLength(int length) => checked((nuint)length * (nuint)Unsafe.SizeOf<T>());

    void IDisposable.Dispose() => Dispose();

    private void ReleaseBuffer()
    {
        NativeMemoryCompat.Free(Address);
        Address = 0;
    }
}
