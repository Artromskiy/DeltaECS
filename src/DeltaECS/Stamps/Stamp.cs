namespace Delta.ECS;

using System;

/// <summary>Opaque equality-only revision of a successful world mutation.</summary>
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
}
