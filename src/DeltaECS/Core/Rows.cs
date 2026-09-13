namespace Delta.ECS;

using System;
internal readonly ref struct ObjectReadValues
{
    private readonly Array _row;

    internal ObjectReadValues(Array row) => _row = row;

    internal object? Get(QuerySlots slots) => _row.GetValue(slots.CurrentIndex);

}

internal readonly ref struct ObjectWriteValues
{
    private readonly Array _row;
    private readonly Type _elementType;

    internal ObjectWriteValues(Array row)
    {
        _row = row;
        _elementType = row.GetType().GetElementType()
            ?? ThrowHelper.ThrowNonArrayComponentRow(nameof(row));
    }

    internal void Set(QuerySlots slots, object? value) => Set(slots.CurrentIndex, value);


    private void Set(int index, object? value)
    {
        Type acceptedType = Nullable.GetUnderlyingType(_elementType) ?? _elementType;
        if (value is null)
        {
            if (_elementType.IsValueType && Nullable.GetUnderlyingType(_elementType) is null)
            {
                ThrowHelper.ThrowExactComponentValueType(_elementType, nameof(value));
            }
        }
        else if (value.GetType() != acceptedType)
        {
            ThrowHelper.ThrowComponentValueTypeMismatch(value.GetType(), _elementType, nameof(value));
        }

        _row.SetValue(value, index);
    }
}
