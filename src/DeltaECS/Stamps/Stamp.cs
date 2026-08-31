namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Opaque 64-bit equality token used for mutation revision tracking.</summary>
public readonly struct Stamp : IEquatable<Stamp>
{
    public Stamp(ulong value) => Value = value;

    public ulong Value { get; }

    public bool Equals(Stamp other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is Stamp other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(Stamp left, Stamp right) => left.Equals(right);

    public static bool operator !=(Stamp left, Stamp right) => !left.Equals(right);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp Next()
        => new(unchecked(Value + 1));
}

internal static class StampMath
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static Stamp Sum(Stamp entity, Stamp chunk, Stamp archetype)
        => new(unchecked(entity.Value + chunk.Value + archetype.Value));
}
