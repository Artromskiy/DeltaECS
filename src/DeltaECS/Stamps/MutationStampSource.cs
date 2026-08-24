namespace Delta.ECS;

internal struct MutationStampSource
{
    private ulong _value;

    public readonly Stamp Current => new(_value);

    public Stamp Next()
    {
        if (_value == ulong.MaxValue)
        {
            throw new InvalidOperationException("The world mutation stamp space is exhausted.");
        }

        return new Stamp(++_value);
    }
}
