namespace DeltaECS;

internal struct MutationStampSource
{
    private ulong _value;

    internal readonly Stamp Current => new(_value);

    internal Stamp Next()
    {
        if (_value == ulong.MaxValue)
        {
            throw new InvalidOperationException("The world mutation stamp space is exhausted.");
        }

        return new Stamp(++_value);
    }
}
