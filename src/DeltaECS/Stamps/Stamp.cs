namespace Delta.ECS;

using System;
using System.Runtime.CompilerServices;

/// <summary>Opaque 64-bit equality token used for component mutation revision tracking.</summary>
public readonly struct Stamp : IEquatable<Stamp>
{
    /// <summary>Creates a stamp from its numeric revision value.</summary>
    public Stamp(ulong value) => Value = value;

    /// <summary>Gets the numeric revision value.</summary>
    public ulong Value { get; }

    /// <summary>Determines whether this stamp equals another stamp.</summary>
    public bool Equals(Stamp other) => Value == other.Value;

    /// <summary>Determines whether this stamp equals the specified object.</summary>
    public override bool Equals(object? obj) => obj is Stamp other && Equals(other);

    /// <summary>Returns a hash code for this stamp.</summary>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Determines whether two stamps are equal.</summary>
    public static bool operator ==(Stamp left, Stamp right) => left.Equals(right);

    /// <summary>Determines whether two stamps are different.</summary>
    public static bool operator !=(Stamp left, Stamp right) => !left.Equals(right);

    /// <summary>Returns the numeric revision value using invariant formatting.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Stamp Next() => new(unchecked(Value + 1));
}
