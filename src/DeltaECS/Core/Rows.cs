namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

public ref partial struct ReadRow
{
    private readonly ref byte _data;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadRow(Array row) => _data = ref ArrayAccess.DataReference(row);
}

public ref partial struct WriteRow
{
    private readonly ref byte _data;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WriteRow(Array row) => _data = ref ArrayAccess.DataReference(row);
}

public readonly ref struct ObjectReadValues
{
    private readonly Array _row;

    internal ObjectReadValues(Array row) => _row = row;

    public object? Get(QuerySlots slots) => _row.GetValue(slots.CurrentIndex);

}

public readonly ref struct ObjectWriteValues
{
    private readonly Array _row;
    private readonly Type _elementType;

    internal ObjectWriteValues(Array row)
    {
        _row = row;
        _elementType = row.GetType().GetElementType()
            ?? throw new ArgumentException("The component row must be a CLR array.", nameof(row));
    }

    public void Set(QuerySlots slots, object? value) => Set(slots.CurrentIndex, value);


    private void Set(int index, object? value)
    {
        Type acceptedType = Nullable.GetUnderlyingType(_elementType) ?? _elementType;
        if (value is null)
        {
            if (_elementType.IsValueType && Nullable.GetUnderlyingType(_elementType) is null)
            {
                throw new ArgumentException($"Component value must be exactly {_elementType}.", nameof(value));
            }
        }
        else if (value.GetType() != acceptedType)
        {
            throw new ArgumentException(
                $"Component value type {value.GetType()} does not match registered type {_elementType}.",
                nameof(value));
        }

        _row.SetValue(value, index);
    }
}
