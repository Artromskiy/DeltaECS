using System.Runtime.InteropServices;

namespace DVG.ECS.Tests;

[StructLayout(LayoutKind.Sequential)]
internal struct Position
{
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Velocity
{
    public float X;
    public float Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Health
{
    public int Value;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NamedRef
{
    public string? Name;
    public int Id;
}

internal sealed class RefPayload
{
    public int Id { get; }

    public RefPayload(int id)
    {
        Id = id;
    }
}

internal sealed class ReferenceComponent
{
    public int Value { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RefMarker
{
    public RefPayload? Payload;
    public int Value;
}
