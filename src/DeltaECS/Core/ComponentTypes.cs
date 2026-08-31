namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

public readonly struct ComponentId : IEquatable<ComponentId>, IComparable<ComponentId>
{
    public int Value { get; }

    public ComponentId(int value)
    {
        Value = value;
    }

    public bool IsValid => Value >= 0;

    public static ComponentId Invalid => new(-1);

    public int CompareTo(ComponentId other) => Value.CompareTo(other.Value);

    public bool Equals(ComponentId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ComponentId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(ComponentId left, ComponentId right) => left.Equals(right);

    public static bool operator !=(ComponentId left, ComponentId right) => !left.Equals(right);

    public static bool operator <(ComponentId left, ComponentId right) => left.CompareTo(right) < 0;

    public static bool operator <=(ComponentId left, ComponentId right) => left.CompareTo(right) <= 0;

    public static bool operator >(ComponentId left, ComponentId right) => left.CompareTo(right) > 0;

    public static bool operator >=(ComponentId left, ComponentId right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Immutable set of component ids backed by dynamically sized native words.
/// </summary>
/// <remarks>
/// <see cref="Capacity"/> is retained as the legacy four-word baseline for source
/// compatibility. It is not a maximum: masks grow to address every non-negative
/// <see cref="ComponentId"/> value, subject to available native memory.
/// </remarks>
public readonly struct ComponentMask : IEquatable<ComponentMask>
{
    // Kept for source compatibility with the former four-word implementation.
    public const int Capacity = 256;

    private readonly NativeComponentMaskStorage? _storage;

    private ComponentMask(NativeComponentMaskStorage storage)
    {
        _storage = storage;
    }

    public bool IsEmpty => _storage is null;

    public static ComponentMask From(ReadOnlySpan<ComponentId> componentIds)
        => FromCore(componentIds, skipInvalid: false);

    internal static ComponentMask FromValidated(ReadOnlySpan<ComponentId> componentIds)
        => FromCore(componentIds, skipInvalid: true);

    public ComponentMask Set(ComponentId componentId)
    {
        int value = Validate(componentId);
        int wordIndex = value >> 5;
        int requiredLength = wordIndex + 1;
        var storage = new NativeComponentMaskStorage(requiredLength);

        if (_storage is not null)
        {
            _storage.CopyTo(storage);
        }

        storage[wordIndex] |= 1u << (value & 31);
        return new ComponentMask(storage);
    }

    public bool Contains(ComponentId componentId)
    {
        if (!componentId.IsValid || _storage is null)
        {
            return false;
        }

        int wordIndex = componentId.Value >> 5;
        return wordIndex < _storage.Length
            && (_storage[wordIndex] & (1u << (componentId.Value & 31))) != 0;
    }

    public bool ContainsAll(ComponentMask other)
    {
        if (other._storage is null)
        {
            return true;
        }

        if (_storage is null || _storage.Length < other._storage.Length)
        {
            return false;
        }

        for (int index = 0; index < other._storage.Length; index++)
        {
            if ((_storage[index] & other._storage[index]) != other._storage[index])
            {
                return false;
            }
        }

        return true;
    }

    public bool Intersects(ComponentMask other)
    {
        if (_storage is null || other._storage is null)
        {
            return false;
        }

        int length = Math.Min(_storage.Length, other._storage.Length);
        for (int index = 0; index < length; index++)
        {
            if ((_storage[index] & other._storage[index]) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public ComponentMask Or(ComponentMask other)
    {
        if (_storage is null)
        {
            return other;
        }

        if (other._storage is null)
        {
            return this;
        }

        int length = Math.Max(_storage.Length, other._storage.Length);
        var storage = new NativeComponentMaskStorage(length);
        for (int index = 0; index < length; index++)
        {
            storage[index] = GetWord(index) | other.GetWord(index);
        }

        return new ComponentMask(storage);
    }

    public ComponentMask Except(ComponentMask other)
    {
        if (_storage is null || other._storage is null)
        {
            return this;
        }

        int length = _storage.Length;
        while (length > 0 && (_storage[length - 1] & ~other.GetWord(length - 1)) == 0)
        {
            length--;
        }

        if (length == 0)
        {
            return default;
        }

        var storage = new NativeComponentMaskStorage(length);
        for (int index = 0; index < length; index++)
        {
            storage[index] = _storage[index] & ~other.GetWord(index);
        }

        return new ComponentMask(storage);
    }

    public int Rank(ComponentId componentId)
    {
        if (!Contains(componentId))
        {
            return -1;
        }

        int wordIndex = componentId.Value >> 5;
        int rank = 0;
        for (int index = 0; index < wordIndex; index++)
        {
            rank += BitOperations.PopCount(_storage![index]);
        }

        uint lowerBits = _storage![wordIndex] & ((1u << (componentId.Value & 31)) - 1u);
        return rank + BitOperations.PopCount(lowerBits);
    }

    public int Count
    {
        get
        {
            if (_storage is null)
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < _storage.Length; index++)
            {
                count += BitOperations.PopCount(_storage[index]);
            }

            return count;
        }
    }

    /// <summary>
    /// Enumerates the set component ids in ascending numeric order without allocating.
    /// </summary>
    public Enumerator GetEnumerator() => new(_storage);

    public ref struct Enumerator
    {
        private readonly NativeComponentMaskStorage? _storage;
        private int _wordIndex;
        private uint _remaining;

        internal Enumerator(NativeComponentMaskStorage? storage)
        {
            _storage = storage;
            _wordIndex = 0;
            _remaining = 0;
            Current = default;
        }

        public ComponentId Current { get; private set; }

        public bool MoveNext()
        {
            if (_storage is null)
            {
                return false;
            }

            while (_remaining == 0 && _wordIndex < _storage.Length)
            {
                _remaining = _storage[_wordIndex++];
            }

            if (_remaining == 0)
            {
                return false;
            }

            int bit = BitOperations.TrailingZeroCount(_remaining);
            _remaining &= _remaining - 1;
            Current = new ComponentId(((_wordIndex - 1) * 32) + bit);
            return true;
        }
    }

    internal void CopyComponentIds(Span<ComponentId> destination)
    {
        int count = Count;
        if (destination.Length < count)
        {
            ThrowHelper.ThrowComponentDestinationTooSmall(nameof(destination));
        }

        int offset = 0;
        var enumerator = GetEnumerator();
        while (enumerator.MoveNext())
        {
            destination[offset++] = enumerator.Current;
        }
    }

    public bool Equals(ComponentMask other)
    {
        if (ReferenceEquals(_storage, other._storage))
        {
            return true;
        }

        if (_storage is null || other._storage is null || _storage.Length != other._storage.Length)
        {
            return false;
        }

        for (int index = 0; index < _storage.Length; index++)
        {
            if (_storage[index] != other._storage[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        if (_storage is not null)
        {
            hash.Add(_storage.Length);
            for (int index = 0; index < _storage.Length; index++)
            {
                hash.Add(_storage[index]);
            }
        }

        return hash.ToHashCode();
    }

    private uint GetWord(int index)
        => _storage is not null && (uint)index < (uint)_storage.Length
            ? _storage[index]
            : 0;

    private static ComponentMask FromCore(ReadOnlySpan<ComponentId> componentIds, bool skipInvalid)
    {
        int maxValue = -1;
        for (int index = 0; index < componentIds.Length; index++)
        {
            ComponentId componentId = componentIds[index];
            if (!componentId.IsValid)
            {
                if (skipInvalid)
                {
                    continue;
                }

                Validate(componentId);
            }

            maxValue = Math.Max(maxValue, componentId.Value);
        }

        if (maxValue < 0)
        {
            return default;
        }

        var storage = new NativeComponentMaskStorage((maxValue >> 5) + 1);
        for (int index = 0; index < componentIds.Length; index++)
        {
            ComponentId componentId = componentIds[index];
            if (!componentId.IsValid)
            {
                continue;
            }

            storage[componentId.Value >> 5] |= 1u << (componentId.Value & 31);
        }

        return new ComponentMask(storage);
    }

    private static int Validate(ComponentId componentId)
    {
        if (!componentId.IsValid)
        {
            return ThrowHelper.ThrowComponentIdOutOfRange();
        }

        return componentId.Value;
    }

    public static bool operator ==(ComponentMask left, ComponentMask right) => left.Equals(right);

    public static bool operator !=(ComponentMask left, ComponentMask right) => !left.Equals(right);
}

/// <summary>Immutable native word storage owned by a component mask.</summary>
internal sealed class NativeComponentMaskStorage
{
    private NativeMemory<uint> _words;

    internal NativeComponentMaskStorage(int length)
    {
        _words = new NativeMemory<uint>(length);
    }

    internal int Length => _words.Length;

    internal Span<uint> Span => _words.Span;

    internal ref uint this[int index]
    {
        get => ref _words[index];
    }

    internal void CopyTo(NativeComponentMaskStorage destination)
    {
        _words.ReadOnlySpan[..Math.Min(Length, destination.Length)].CopyTo(destination.Span);
    }

    ~NativeComponentMaskStorage()
    {
        _words.Dispose();
    }
}

public readonly struct SchemaId : IEquatable<SchemaId>
{
    public ulong Value { get; }

    public SchemaId(ulong value)
    {
        Value = value;
    }

    public bool Equals(SchemaId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is SchemaId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(SchemaId left, SchemaId right) => left.Equals(right);

    public static bool operator !=(SchemaId left, SchemaId right) => !left.Equals(right);

    public static SchemaId FromUInt64(ulong value) => new(value);
}

public readonly struct ComponentLayout : IEquatable<ComponentLayout>
{
    public ComponentLayout(
        SchemaId schemaId,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type runtimeType,
        int alignment = 1)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);

        if (alignment <= 0)
        {
            alignment = 1;
        }

        SchemaId = schemaId;
        // A Type-backed layout is an ArrayRows layout. Its CLR element size is
        // not a byte-storage contract. Rows may contain value types, managed-field
        // structs, or object references, so keep byte size/stride unavailable
        // instead of guessing with Buffer.ByteLength or Marshal.SizeOf.
        Size = 0;
        Alignment = alignment;
        RuntimeType = runtimeType;
        RuntimeTypeHandle = runtimeType.TypeHandle;
        Stride = 0;
    }

    public ComponentLayout(
        SchemaId schemaId,
        int size,
        int alignment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (alignment <= 0)
        {
            alignment = 1;
        }

        SchemaId = schemaId;
        Size = size;
        Alignment = alignment;
        Stride = Align(size, alignment);
        RuntimeType = null;
        RuntimeTypeHandle = default;
    }

    public SchemaId SchemaId { get; }

    public int Size { get; }

    public int Alignment { get; }

    public int Stride { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type? RuntimeType { get; }

    public RuntimeTypeHandle RuntimeTypeHandle { get; }

    public static int Align(int size, int alignment) => (size + alignment - 1) / alignment * alignment;

    public bool Equals(ComponentLayout other) => SchemaId == other.SchemaId && Size == other.Size && Alignment == other.Alignment && Stride == other.Stride && RuntimeType == other.RuntimeType;

    public override bool Equals(object? obj) => obj is ComponentLayout other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(SchemaId.Value, Size, Alignment, Stride, RuntimeType);
}
