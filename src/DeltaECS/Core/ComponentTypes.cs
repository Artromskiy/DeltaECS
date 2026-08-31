namespace Delta.ECS;

using System;
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

public readonly struct ComponentMask : IEquatable<ComponentMask>
{
    public const int Capacity = 256;

    private readonly ulong _word0;
    private readonly ulong _word1;
    private readonly ulong _word2;
    private readonly ulong _word3;

    private ComponentMask(ulong word0, ulong word1, ulong word2, ulong word3)
    {
        _word0 = word0;
        _word1 = word1;
        _word2 = word2;
        _word3 = word3;
    }

    public bool IsEmpty => (_word0 | _word1 | _word2 | _word3) == 0;

    public static ComponentMask From(ReadOnlySpan<ComponentId> componentIds)
    {
        var mask = default(ComponentMask);
        for (int i = 0; i < componentIds.Length; i++)
        {
            mask = mask.Set(componentIds[i]);
        }

        return mask;
    }

    public ComponentMask Set(ComponentId componentId)
    {
        int value = Validate(componentId);
        int word = value >> 6;
        ulong bit = 1UL << (value & 63);
        return word switch
        {
            0 => new ComponentMask(_word0 | bit, _word1, _word2, _word3),
            1 => new ComponentMask(_word0, _word1 | bit, _word2, _word3),
            2 => new ComponentMask(_word0, _word1, _word2 | bit, _word3),
            _ => new ComponentMask(_word0, _word1, _word2, _word3 | bit)
        };
    }

    public bool Contains(ComponentId componentId)
    {
        if (!componentId.IsValid || componentId.Value >= Capacity)
        {
            return false;
        }

        ulong bit = 1UL << (componentId.Value & 63);
        return (GetWord(componentId.Value >> 6) & bit) != 0;
    }

    public bool ContainsAll(ComponentMask other)
    {
        return (_word0 & other._word0) == other._word0
            && (_word1 & other._word1) == other._word1
            && (_word2 & other._word2) == other._word2
            && (_word3 & other._word3) == other._word3;
    }

    public bool Intersects(ComponentMask other)
    {
        return ((_word0 & other._word0) | (_word1 & other._word1)
            | (_word2 & other._word2) | (_word3 & other._word3)) != 0;
    }

    public ComponentMask Or(ComponentMask other) => new(_word0 | other._word0, _word1 | other._word1, _word2 | other._word2, _word3 | other._word3);

    public ComponentMask Except(ComponentMask other) => new(_word0 & ~other._word0, _word1 & ~other._word1, _word2 & ~other._word2, _word3 & ~other._word3);

    public int Rank(ComponentId componentId)
    {
        if (!Contains(componentId))
        {
            return -1;
        }

        int value = componentId.Value;
        int word = value >> 6;
        int bit = value & 63;
        int rank = word switch
        {
            0 => 0,
            1 => BitOperations.PopCount(_word0),
            2 => BitOperations.PopCount(_word0) + BitOperations.PopCount(_word1),
            _ => BitOperations.PopCount(_word0) + BitOperations.PopCount(_word1) + BitOperations.PopCount(_word2)
        };

        ulong lowerBits = bit == 0 ? 0UL : GetWord(word) & ((1UL << bit) - 1UL);
        return rank + BitOperations.PopCount(lowerBits);
    }

    public int Count => BitOperations.PopCount(_word0) + BitOperations.PopCount(_word1)
        + BitOperations.PopCount(_word2) + BitOperations.PopCount(_word3);

    /// <summary>
    /// Enumerates the set component ids in ascending numeric order without allocating.
    /// </summary>
    public Enumerator GetEnumerator() => new(_word0, _word1, _word2, _word3);

    public ref struct Enumerator
    {
        private ulong _word0;
        private ulong _word1;
        private ulong _word2;
        private ulong _word3;
        private int _wordIndex;

        internal Enumerator(ulong word0, ulong word1, ulong word2, ulong word3)
        {
            _word0 = word0;
            _word1 = word1;
            _word2 = word2;
            _word3 = word3;
            _wordIndex = 0;
            Current = default;
        }

        public ComponentId Current { get; private set; }

        public bool MoveNext()
        {
            while (_wordIndex < 4)
            {
                ulong word = _wordIndex switch
                {
                    0 => _word0,
                    1 => _word1,
                    2 => _word2,
                    _ => _word3
                };
                if (word == 0)
                {
                    _wordIndex++;
                    continue;
                }

                int bit = BitOperations.TrailingZeroCount(word);
                switch (_wordIndex)
                {
                    case 0:
                        _word0 = word & (word - 1);
                        break;
                    case 1:
                        _word1 = word & (word - 1);
                        break;
                    case 2:
                        _word2 = word & (word - 1);
                        break;
                    default:
                        _word3 = word & (word - 1);
                        break;
                }

                Current = new ComponentId((_wordIndex * 64) + bit);
                return true;
            }

            return false;
        }
    }

    internal void CopyComponentIds(Span<ComponentId> destination)
    {
        if (destination.Length < Count)
        {
            ThrowHelper.ThrowComponentDestinationTooSmall(nameof(destination));
        }

        int offset = CopyWord(_word0, 0, destination, 0);
        offset = CopyWord(_word1, 64, destination, offset);
        offset = CopyWord(_word2, 128, destination, offset);
        CopyWord(_word3, 192, destination, offset);
    }

    public bool Equals(ComponentMask other) => _word0 == other._word0 && _word1 == other._word1
        && _word2 == other._word2 && _word3 == other._word3;

    public override bool Equals(object? obj) => obj is ComponentMask other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_word0, _word1, _word2, _word3);

    private ulong GetWord(int index) => index switch
    {
        0 => _word0,
        1 => _word1,
        2 => _word2,
        _ => _word3
    };

    private static int Validate(ComponentId componentId)
    {
        if (!componentId.IsValid || componentId.Value >= Capacity)
        {
            return ThrowHelper.ThrowComponentIdOutOfRange();
        }

        return componentId.Value;
    }

    private static int CopyWord(ulong word, int baseValue, Span<ComponentId> destination, int offset)
    {
        while (word != 0)
        {
            int bit = BitOperations.TrailingZeroCount(word);
            destination[offset++] = new ComponentId(baseValue + bit);
            word &= word - 1;
        }

        return offset;
    }

    public static bool operator ==(ComponentMask left, ComponentMask right) => left.Equals(right);

    public static bool operator !=(ComponentMask left, ComponentMask right) => !left.Equals(right);
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
