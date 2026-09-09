namespace Delta.ECS;

internal static class BitOperationsCompat
{
    internal static int PopCount(uint value)
    {
#if NETSTANDARD2_1
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
#else
        return System.Numerics.BitOperations.PopCount(value);
#endif
    }

    internal static int TrailingZeroCount(uint value)
    {
#if NETSTANDARD2_1
        if (value == 0)
        {
            return 32;
        }

        int count = 0;
        while ((value & 1) == 0)
        {
            value >>= 1;
            count++;
        }

        return count;
#else
        return System.Numerics.BitOperations.TrailingZeroCount(value);
#endif
    }
}
