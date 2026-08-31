namespace Delta.ECS;

using System.Runtime.CompilerServices;

internal struct StampCounter
{
    private ulong _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp Next()
        => new(unchecked(++_value));
}
