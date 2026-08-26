using System.Runtime.CompilerServices;
using Delta.Maths;

namespace DeltaECS.LayeredPipeline;

/// <summary>
/// Deliberately large no-inline layer chain used to measure instruction-cache working-set effects.
/// The chain contains ordinary, floating-point trigonometric, and fixed-point stages.
/// </summary>
public sealed class MathStressPipeline
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer001(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00012700f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00012700f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00012700f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer002(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00014400f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00014400f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00014400f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer003(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00016100f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00016100f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00016100f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer004(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00017800f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00017800f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00017800f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer005(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00019500f + 0.325000f);
        float y = Maths.Saturate(data.A6.Z * 0.00019500f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00019500f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer006(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00021200f + 0.356000f);
        float y = Maths.Saturate(data.A1.W * 0.00021200f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00021200f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer007(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00022900f + 0.170000f);
        float y = Maths.Saturate(data.A6.X * 0.00022900f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00022900f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer008(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00024600f + 0.201000f);
        float y = Maths.Saturate(data.A1.Y * 0.00024600f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00024600f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer009(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00026300f + 0.232000f);
        float y = Maths.Saturate(data.A6.Z * 0.00026300f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00026300f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer010(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00028000f + 0.263000f);
        float y = Maths.Saturate(data.A1.W * 0.00028000f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00028000f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer011(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00011000f + 0.294000f);
        float y = Maths.Saturate(data.A6.X * 0.00011000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00011000f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer012(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00012700f + 0.325000f);
        float y = Maths.Saturate(data.A1.Y * 0.00012700f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00012700f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer013(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00014400f + 0.356000f);
        float y = Maths.Saturate(data.A6.Z * 0.00014400f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00014400f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer014(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00016100f + 0.170000f);
        float y = Maths.Saturate(data.A1.W * 0.00016100f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00016100f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer015(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00017800f + 0.201000f);
        float y = Maths.Saturate(data.A6.X * 0.00017800f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00017800f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer016(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00019500f + 0.232000f);
        float y = Maths.Saturate(data.A1.Y * 0.00019500f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00019500f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer017(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00021200f + 0.263000f);
        float y = Maths.Saturate(data.A6.Z * 0.00021200f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00021200f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer018(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00022900f + 0.294000f);
        float y = Maths.Saturate(data.A1.W * 0.00022900f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00022900f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer019(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00024600f + 0.325000f);
        float y = Maths.Saturate(data.A6.X * 0.00024600f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00024600f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer020(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00026300f + 0.356000f);
        float y = Maths.Saturate(data.A1.Y * 0.00026300f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00026300f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer021(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00028000f + 0.170000f);
        float y = Maths.Saturate(data.A6.Z * 0.00028000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00028000f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer022(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00011000f + 0.201000f);
        float y = Maths.Saturate(data.A1.W * 0.00011000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00011000f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer023(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00012700f + 0.232000f);
        float y = Maths.Saturate(data.A6.X * 0.00012700f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00012700f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer024(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00014400f + 0.263000f);
        float y = Maths.Saturate(data.A1.Y * 0.00014400f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00014400f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer025(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00016100f + 0.294000f);
        float y = Maths.Saturate(data.A6.Z * 0.00016100f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00016100f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer026(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00017800f + 0.325000f);
        float y = Maths.Saturate(data.A1.W * 0.00017800f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00017800f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer027(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00019500f + 0.356000f);
        float y = Maths.Saturate(data.A6.X * 0.00019500f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00019500f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer028(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00021200f + 0.170000f);
        float y = Maths.Saturate(data.A1.Y * 0.00021200f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00021200f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer029(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00022900f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00022900f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00022900f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer030(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00024600f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00024600f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00024600f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer031(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00026300f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00026300f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00026300f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer032(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00028000f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00028000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00028000f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer033(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00011000f + 0.325000f);
        float y = Maths.Saturate(data.A6.Z * 0.00011000f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00011000f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer034(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00012700f + 0.356000f);
        float y = Maths.Saturate(data.A1.W * 0.00012700f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00012700f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer035(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00014400f + 0.170000f);
        float y = Maths.Saturate(data.A6.X * 0.00014400f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00014400f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer036(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00016100f + 0.201000f);
        float y = Maths.Saturate(data.A1.Y * 0.00016100f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00016100f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer037(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00017800f + 0.232000f);
        float y = Maths.Saturate(data.A6.Z * 0.00017800f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00017800f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer038(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00019500f + 0.263000f);
        float y = Maths.Saturate(data.A1.W * 0.00019500f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00019500f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer039(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00021200f + 0.294000f);
        float y = Maths.Saturate(data.A6.X * 0.00021200f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00021200f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer040(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00022900f + 0.325000f);
        float y = Maths.Saturate(data.A1.Y * 0.00022900f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00022900f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer041(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00024600f + 0.356000f);
        float y = Maths.Saturate(data.A6.Z * 0.00024600f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00024600f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer042(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00026300f + 0.170000f);
        float y = Maths.Saturate(data.A1.W * 0.00026300f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00026300f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer043(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00028000f + 0.201000f);
        float y = Maths.Saturate(data.A6.X * 0.00028000f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00028000f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer044(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00011000f + 0.232000f);
        float y = Maths.Saturate(data.A1.Y * 0.00011000f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00011000f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer045(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00012700f + 0.263000f);
        float y = Maths.Saturate(data.A6.Z * 0.00012700f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00012700f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer046(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00014400f + 0.294000f);
        float y = Maths.Saturate(data.A1.W * 0.00014400f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00014400f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer047(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00016100f + 0.325000f);
        float y = Maths.Saturate(data.A6.X * 0.00016100f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00016100f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer048(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00017800f + 0.356000f);
        float y = Maths.Saturate(data.A1.Y * 0.00017800f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00017800f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer049(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00019500f + 0.170000f);
        float y = Maths.Saturate(data.A6.Z * 0.00019500f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00019500f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer050(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00021200f + 0.201000f);
        float y = Maths.Saturate(data.A1.W * 0.00021200f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00021200f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer051(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00022900f + 0.232000f);
        float y = Maths.Saturate(data.A6.X * 0.00022900f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00022900f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer052(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00024600f + 0.263000f);
        float y = Maths.Saturate(data.A1.Y * 0.00024600f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00024600f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer053(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00026300f + 0.294000f);
        float y = Maths.Saturate(data.A6.Z * 0.00026300f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00026300f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer054(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00028000f + 0.325000f);
        float y = Maths.Saturate(data.A1.W * 0.00028000f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00028000f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer055(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00011000f + 0.356000f);
        float y = Maths.Saturate(data.A6.X * 0.00011000f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00011000f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer056(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00012700f + 0.170000f);
        float y = Maths.Saturate(data.A1.Y * 0.00012700f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00012700f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer057(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00014400f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00014400f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00014400f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer058(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00016100f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00016100f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00016100f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer059(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00017800f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00017800f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00017800f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer060(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00019500f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00019500f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00019500f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer061(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00021200f + 0.325000f);
        float y = Maths.Saturate(data.A6.Z * 0.00021200f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00021200f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer062(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00022900f + 0.356000f);
        float y = Maths.Saturate(data.A1.W * 0.00022900f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00022900f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer063(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00024600f + 0.170000f);
        float y = Maths.Saturate(data.A6.X * 0.00024600f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00024600f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer064(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00026300f + 0.201000f);
        float y = Maths.Saturate(data.A1.Y * 0.00026300f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00026300f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer065(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00028000f + 0.232000f);
        float y = Maths.Saturate(data.A6.Z * 0.00028000f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00028000f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer066(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00011000f + 0.263000f);
        float y = Maths.Saturate(data.A1.W * 0.00011000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00011000f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer067(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00012700f + 0.294000f);
        float y = Maths.Saturate(data.A6.X * 0.00012700f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00012700f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer068(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00014400f + 0.325000f);
        float y = Maths.Saturate(data.A1.Y * 0.00014400f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00014400f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer069(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00016100f + 0.356000f);
        float y = Maths.Saturate(data.A6.Z * 0.00016100f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00016100f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer070(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00017800f + 0.170000f);
        float y = Maths.Saturate(data.A1.W * 0.00017800f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00017800f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer071(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00019500f + 0.201000f);
        float y = Maths.Saturate(data.A6.X * 0.00019500f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00019500f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer072(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00021200f + 0.232000f);
        float y = Maths.Saturate(data.A1.Y * 0.00021200f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00021200f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer073(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00022900f + 0.263000f);
        float y = Maths.Saturate(data.A6.Z * 0.00022900f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00022900f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer074(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00024600f + 0.294000f);
        float y = Maths.Saturate(data.A1.W * 0.00024600f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00024600f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer075(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00026300f + 0.325000f);
        float y = Maths.Saturate(data.A6.X * 0.00026300f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00026300f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer076(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00028000f + 0.356000f);
        float y = Maths.Saturate(data.A1.Y * 0.00028000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00028000f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer077(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00011000f + 0.170000f);
        float y = Maths.Saturate(data.A6.Z * 0.00011000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00011000f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer078(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00012700f + 0.201000f);
        float y = Maths.Saturate(data.A1.W * 0.00012700f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00012700f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer079(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00014400f + 0.232000f);
        float y = Maths.Saturate(data.A6.X * 0.00014400f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00014400f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer080(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00016100f + 0.263000f);
        float y = Maths.Saturate(data.A1.Y * 0.00016100f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00016100f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer081(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00017800f + 0.294000f);
        float y = Maths.Saturate(data.A6.Z * 0.00017800f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00017800f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer082(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00019500f + 0.325000f);
        float y = Maths.Saturate(data.A1.W * 0.00019500f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00019500f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer083(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00021200f + 0.356000f);
        float y = Maths.Saturate(data.A6.X * 0.00021200f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00021200f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer084(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00022900f + 0.170000f);
        float y = Maths.Saturate(data.A1.Y * 0.00022900f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00022900f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer085(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00024600f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00024600f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00024600f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer086(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00026300f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00026300f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00026300f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer087(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00028000f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00028000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00028000f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer088(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00011000f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00011000f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00011000f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer089(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00012700f + 0.325000f);
        float y = Maths.Saturate(data.A6.Z * 0.00012700f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00012700f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer090(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00014400f + 0.356000f);
        float y = Maths.Saturate(data.A1.W * 0.00014400f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00014400f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer091(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00016100f + 0.170000f);
        float y = Maths.Saturate(data.A6.X * 0.00016100f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00016100f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer092(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00017800f + 0.201000f);
        float y = Maths.Saturate(data.A1.Y * 0.00017800f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00017800f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer093(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00019500f + 0.232000f);
        float y = Maths.Saturate(data.A6.Z * 0.00019500f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00019500f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer094(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00021200f + 0.263000f);
        float y = Maths.Saturate(data.A1.W * 0.00021200f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00021200f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer095(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00022900f + 0.294000f);
        float y = Maths.Saturate(data.A6.X * 0.00022900f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00022900f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer096(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00024600f + 0.325000f);
        float y = Maths.Saturate(data.A1.Y * 0.00024600f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00024600f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer097(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00026300f + 0.356000f);
        float y = Maths.Saturate(data.A6.Z * 0.00026300f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00026300f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer098(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00028000f + 0.170000f);
        float y = Maths.Saturate(data.A1.W * 0.00028000f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00028000f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer099(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00011000f + 0.201000f);
        float y = Maths.Saturate(data.A6.X * 0.00011000f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00011000f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer100(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00012700f + 0.232000f);
        float y = Maths.Saturate(data.A1.Y * 0.00012700f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00012700f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer101(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00014400f + 0.263000f);
        float y = Maths.Saturate(data.A6.Z * 0.00014400f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00014400f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer102(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00016100f + 0.294000f);
        float y = Maths.Saturate(data.A1.W * 0.00016100f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00016100f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer103(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00017800f + 0.325000f);
        float y = Maths.Saturate(data.A6.X * 0.00017800f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00017800f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer104(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00019500f + 0.356000f);
        float y = Maths.Saturate(data.A1.Y * 0.00019500f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00019500f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer105(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00021200f + 0.170000f);
        float y = Maths.Saturate(data.A6.Z * 0.00021200f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00021200f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer106(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00022900f + 0.201000f);
        float y = Maths.Saturate(data.A1.W * 0.00022900f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00022900f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer107(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00024600f + 0.232000f);
        float y = Maths.Saturate(data.A6.X * 0.00024600f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00024600f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer108(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00026300f + 0.263000f);
        float y = Maths.Saturate(data.A1.Y * 0.00026300f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00026300f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer109(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00028000f + 0.294000f);
        float y = Maths.Saturate(data.A6.Z * 0.00028000f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00028000f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer110(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00011000f + 0.325000f);
        float y = Maths.Saturate(data.A1.W * 0.00011000f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00011000f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer111(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00012700f + 0.356000f);
        float y = Maths.Saturate(data.A6.X * 0.00012700f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00012700f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer112(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00014400f + 0.170000f);
        float y = Maths.Saturate(data.A1.Y * 0.00014400f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00014400f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer113(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00016100f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00016100f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00016100f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer114(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00017800f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00017800f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00017800f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer115(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00019500f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00019500f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00019500f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer116(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00021200f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00021200f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00021200f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer117(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00022900f + 0.325000f);
        float y = Maths.Saturate(data.A6.Z * 0.00022900f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00022900f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer118(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00024600f + 0.356000f);
        float y = Maths.Saturate(data.A1.W * 0.00024600f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00024600f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer119(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00026300f + 0.170000f);
        float y = Maths.Saturate(data.A6.X * 0.00026300f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00026300f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer120(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00028000f + 0.201000f);
        float y = Maths.Saturate(data.A1.Y * 0.00028000f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00028000f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer121(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00011000f + 0.232000f);
        float y = Maths.Saturate(data.A6.Z * 0.00011000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00011000f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer122(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00012700f + 0.263000f);
        float y = Maths.Saturate(data.A1.W * 0.00012700f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00012700f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer123(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00014400f + 0.294000f);
        float y = Maths.Saturate(data.A6.X * 0.00014400f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00014400f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer124(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00016100f + 0.325000f);
        float y = Maths.Saturate(data.A1.Y * 0.00016100f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00016100f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer125(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.Y * 0.00017800f + 0.356000f);
        float y = Maths.Saturate(data.A6.Z * 0.00017800f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.W * 0.00017800f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Z = Maths.Lerp(data.A4.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer126(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.Z * 0.00019500f + 0.170000f);
        float y = Maths.Saturate(data.A1.W * 0.00019500f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.X * 0.00019500f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.W = Maths.Lerp(data.A6.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer127(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.W * 0.00021200f + 0.201000f);
        float y = Maths.Saturate(data.A6.X * 0.00021200f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.Y * 0.00021200f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.X = Maths.Lerp(data.A8.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer128(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.X * 0.00022900f + 0.232000f);
        float y = Maths.Saturate(data.A1.Y * 0.00022900f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.Z * 0.00022900f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Y = Maths.Lerp(data.A0.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer129(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.Y * 0.00024600f + 0.263000f);
        float y = Maths.Saturate(data.A6.Z * 0.00024600f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.W * 0.00024600f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Z = Maths.Lerp(data.A2.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer130(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.Z * 0.00026300f + 0.294000f);
        float y = Maths.Saturate(data.A1.W * 0.00026300f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.X * 0.00026300f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.W = Maths.Lerp(data.A4.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer131(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.W * 0.00028000f + 0.325000f);
        float y = Maths.Saturate(data.A6.X * 0.00028000f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.Y * 0.00028000f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.X = Maths.Lerp(data.A6.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer132(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.X * 0.00011000f + 0.356000f);
        float y = Maths.Saturate(data.A1.Y * 0.00011000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.Z * 0.00011000f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Y = Maths.Lerp(data.A8.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer133(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.Y * 0.00012700f + 0.170000f);
        float y = Maths.Saturate(data.A6.Z * 0.00012700f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.W * 0.00012700f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.Z = Maths.Lerp(data.A0.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer134(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.Z * 0.00014400f + 0.201000f);
        float y = Maths.Saturate(data.A1.W * 0.00014400f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.X * 0.00014400f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer135(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A5.W * 0.00016100f + 0.232000f);
        float y = Maths.Saturate(data.A6.X * 0.00016100f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A7.Y * 0.00016100f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A8.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.X = Maths.Lerp(data.A4.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer136(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A8.X * 0.00017800f + 0.263000f);
        float y = Maths.Saturate(data.A1.Y * 0.00017800f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A4.Z * 0.00017800f + 0.25f), 0.083000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A7.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Y = Maths.Lerp(data.A6.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer137(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A1.Y * 0.00019500f + 0.294000f);
        float y = Maths.Saturate(data.A6.Z * 0.00019500f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A1.W * 0.00019500f + 0.25f), 0.096000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A6.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.Z = Maths.Lerp(data.A8.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer138(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A4.Z * 0.00021200f + 0.325000f);
        float y = Maths.Saturate(data.A1.W * 0.00021200f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A8.X * 0.00021200f + 0.25f), 0.109000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A5.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.W = Maths.Lerp(data.A0.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer139(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A7.W * 0.00022900f + 0.356000f);
        float y = Maths.Saturate(data.A6.X * 0.00022900f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A5.Y * 0.00022900f + 0.25f), 0.122000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A4.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.X = Maths.Lerp(data.A2.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer140(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A0.X * 0.00024600f + 0.170000f);
        float y = Maths.Saturate(data.A1.Y * 0.00024600f + 0.230000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A2.Z * 0.00024600f + 0.25f), 0.135000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A3.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A4.Y = Maths.Lerp(data.A4.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer141(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A3.Y * 0.00026300f + 0.201000f);
        float y = Maths.Saturate(data.A6.Z * 0.00026300f + 0.277000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A9.W * 0.00026300f + 0.25f), 0.148000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A2.X = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A6.Z = Maths.Lerp(data.A6.Z, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer142(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A6.Z * 0.00028000f + 0.232000f);
        float y = Maths.Saturate(data.A1.W * 0.00028000f + 0.324000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A6.X * 0.00028000f + 0.25f), 0.161000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A1.Y = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A8.W = Maths.Lerp(data.A8.W, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer143(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A9.W * 0.00011000f + 0.263000f);
        float y = Maths.Saturate(data.A6.X * 0.00011000f + 0.371000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A3.Y * 0.00011000f + 0.25f), 0.174000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A0.Z = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A0.X = Maths.Lerp(data.A0.X, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer144(ref TransformData160 data)
    {
        float x = Maths.Saturate(data.A2.X * 0.00012700f + 0.294000f);
        float y = Maths.Saturate(data.A1.Y * 0.00012700f + 0.418000f);
        float z = Maths.Fma(x, y, 0.125f);
        float q = Maths.Lerp(z, Maths.Saturate(data.A0.Z * 0.00012700f + 0.25f), 0.070000f);
        float r = Maths.SmoothStep(0f, 1f, Maths.Fract(q + 0.03125f));
        float s = Maths.MoveTowards(r, x, 0.0625f);
        float t = Maths.Clamp(Maths.Remap(s, 0f, 1f, -0.5f, 0.5f), -1f, 1f);
        float u = Maths.Abs(t) + Maths.Fract(y * 0.75f);
        data.A9.W = Maths.Clamp(Maths.Fma(u, 0.5f, q), -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, Maths.Saturate(s + u * 0.03125f), 0.375f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer145(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A1.X) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, normalized, 0.125f);
        data.A3.Z = Maths.Fma(sine, cosine, data.A3.Z * 0.0001f);
        data.A4.W = Maths.Clamp(data.A4.W + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer146(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A4.Y) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A7.Z = Maths.Lerp(data.A7.Z, normalized, 0.125f);
        data.A0.W = Maths.Fma(sine, cosine, data.A0.W * 0.0001f);
        data.A3.X = Maths.Clamp(data.A3.X + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer147(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A7.Z) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, normalized, 0.125f);
        data.A7.X = Maths.Fma(sine, cosine, data.A7.X * 0.0001f);
        data.A2.Y = Maths.Clamp(data.A2.Y + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer148(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A0.W) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A7.X = Maths.Lerp(data.A7.X, normalized, 0.125f);
        data.A4.Y = Maths.Fma(sine, cosine, data.A4.Y * 0.0001f);
        data.A1.Z = Maths.Clamp(data.A1.Z + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer149(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A3.X) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A2.Y = Maths.Lerp(data.A2.Y, normalized, 0.125f);
        data.A1.Z = Maths.Fma(sine, cosine, data.A1.Z * 0.0001f);
        data.A0.W = Maths.Clamp(data.A0.W + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer150(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A6.Y) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A7.Z = Maths.Lerp(data.A7.Z, normalized, 0.125f);
        data.A8.W = Maths.Fma(sine, cosine, data.A8.W * 0.0001f);
        data.A9.X = Maths.Clamp(data.A9.X + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer151(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A9.Z) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A2.W = Maths.Lerp(data.A2.W, normalized, 0.125f);
        data.A5.X = Maths.Fma(sine, cosine, data.A5.X * 0.0001f);
        data.A8.Y = Maths.Clamp(data.A8.Y + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer152(ref TransformData160 data)
    {
        float angle = Maths.Fract(Maths.Abs(data.A2.W) * 0.001f);
        float sine = Maths.Sin(angle);
        float cosine = Maths.Cos(angle);
        float tangent = Maths.Atan2(sine, Maths.Max(cosine, 0.001f));
        float radius = Maths.Sqrt(Maths.Fma(sine, sine, Maths.Fma(cosine, cosine, 0.0001f)));
        float normalized = Maths.Clamp(Maths.Fma(tangent, 0.125f, 0.5f) / radius, -1f, 1f);
        data.A7.X = Maths.Lerp(data.A7.X, normalized, 0.125f);
        data.A2.Y = Maths.Fma(sine, cosine, data.A2.Y * 0.0001f);
        data.A7.Z = Maths.Clamp(data.A7.Z + normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer153(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A2.Y) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A3.Z = (float)Maths.Lerp((fix)data.A3.Z, normalized, (fix)0.125f) * 0.001f;
        data.A4.W = (float)Maths.Fma(sine, cosine, (fix)data.A4.W * (fix)0.0001f) * 0.001f;
        data.A5.X = Maths.Clamp(data.A5.X + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer154(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A5.Z) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A8.W = (float)Maths.Lerp((fix)data.A8.W, normalized, (fix)0.125f) * 0.001f;
        data.A1.X = (float)Maths.Fma(sine, cosine, (fix)data.A1.X * (fix)0.0001f) * 0.001f;
        data.A4.Y = Maths.Clamp(data.A4.Y + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer155(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A8.W) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A3.X = (float)Maths.Lerp((fix)data.A3.X, normalized, (fix)0.125f) * 0.001f;
        data.A8.Y = (float)Maths.Fma(sine, cosine, (fix)data.A8.Y * (fix)0.0001f) * 0.001f;
        data.A3.Z = Maths.Clamp(data.A3.Z + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer156(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A1.X) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A8.Y = (float)Maths.Lerp((fix)data.A8.Y, normalized, (fix)0.125f) * 0.001f;
        data.A5.Z = (float)Maths.Fma(sine, cosine, (fix)data.A5.Z * (fix)0.0001f) * 0.001f;
        data.A2.W = Maths.Clamp(data.A2.W + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer157(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A4.Y) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A3.Z = (float)Maths.Lerp((fix)data.A3.Z, normalized, (fix)0.125f) * 0.001f;
        data.A2.W = (float)Maths.Fma(sine, cosine, (fix)data.A2.W * (fix)0.0001f) * 0.001f;
        data.A1.X = Maths.Clamp(data.A1.X + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer158(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A7.Z) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A8.W = (float)Maths.Lerp((fix)data.A8.W, normalized, (fix)0.125f) * 0.001f;
        data.A9.X = (float)Maths.Fma(sine, cosine, (fix)data.A9.X * (fix)0.0001f) * 0.001f;
        data.A0.Y = Maths.Clamp(data.A0.Y + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer159(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A0.W) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A3.X = (float)Maths.Lerp((fix)data.A3.X, normalized, (fix)0.125f) * 0.001f;
        data.A6.Y = (float)Maths.Fma(sine, cosine, (fix)data.A6.Y * (fix)0.0001f) * 0.001f;
        data.A9.Z = Maths.Clamp(data.A9.Z + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer160(ref TransformData160 data)
    {
        fix angle = (fix)Maths.Fract(Maths.Abs(data.A3.X) * 0.03125f);
        fix sine = Maths.Sin(angle);
        fix cosine = Maths.Cos(angle);
        fix arc = Maths.Atan2(sine, fix.One);
        fix radius = Maths.Sqrt(Maths.Fma(sine, sine, fix.One));
        fix normalized = Maths.Clamp(Maths.Fma(arc, (fix)0.125f, (fix)0.5f) / radius, -fix.One, fix.One);
        data.A8.Y = (float)Maths.Lerp((fix)data.A8.Y, normalized, (fix)0.125f) * 0.001f;
        data.A3.Z = (float)Maths.Fma(sine, cosine, (fix)data.A3.Z * (fix)0.0001f) * 0.001f;
        data.A8.W = Maths.Clamp(data.A8.W + (float)normalized * 0.03125f, -1f, 1f);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [InPlaceLayeredPipeline(
        nameof(Layer001), nameof(Layer002), nameof(Layer003), nameof(Layer004), nameof(Layer005), nameof(Layer006), nameof(Layer007), nameof(Layer008),
        nameof(Layer009), nameof(Layer010), nameof(Layer011), nameof(Layer012), nameof(Layer013), nameof(Layer014), nameof(Layer015), nameof(Layer016),
        nameof(Layer017), nameof(Layer018), nameof(Layer019), nameof(Layer020), nameof(Layer021), nameof(Layer022), nameof(Layer023), nameof(Layer024),
        nameof(Layer025), nameof(Layer026), nameof(Layer027), nameof(Layer028), nameof(Layer029), nameof(Layer030), nameof(Layer031), nameof(Layer032),
        nameof(Layer033), nameof(Layer034), nameof(Layer035), nameof(Layer036), nameof(Layer037), nameof(Layer038), nameof(Layer039), nameof(Layer040),
        nameof(Layer041), nameof(Layer042), nameof(Layer043), nameof(Layer044), nameof(Layer045), nameof(Layer046), nameof(Layer047), nameof(Layer048),
        nameof(Layer049), nameof(Layer050), nameof(Layer051), nameof(Layer052), nameof(Layer053), nameof(Layer054), nameof(Layer055), nameof(Layer056),
        nameof(Layer057), nameof(Layer058), nameof(Layer059), nameof(Layer060), nameof(Layer061), nameof(Layer062), nameof(Layer063), nameof(Layer064),
        nameof(Layer065), nameof(Layer066), nameof(Layer067), nameof(Layer068), nameof(Layer069), nameof(Layer070), nameof(Layer071), nameof(Layer072),
        nameof(Layer073), nameof(Layer074), nameof(Layer075), nameof(Layer076), nameof(Layer077), nameof(Layer078), nameof(Layer079), nameof(Layer080),
        nameof(Layer081), nameof(Layer082), nameof(Layer083), nameof(Layer084), nameof(Layer085), nameof(Layer086), nameof(Layer087), nameof(Layer088),
        nameof(Layer089), nameof(Layer090), nameof(Layer091), nameof(Layer092), nameof(Layer093), nameof(Layer094), nameof(Layer095), nameof(Layer096),
        nameof(Layer097), nameof(Layer098), nameof(Layer099), nameof(Layer100), nameof(Layer101), nameof(Layer102), nameof(Layer103), nameof(Layer104),
        nameof(Layer105), nameof(Layer106), nameof(Layer107), nameof(Layer108), nameof(Layer109), nameof(Layer110), nameof(Layer111), nameof(Layer112),
        nameof(Layer113), nameof(Layer114), nameof(Layer115), nameof(Layer116), nameof(Layer117), nameof(Layer118), nameof(Layer119), nameof(Layer120),
        nameof(Layer121), nameof(Layer122), nameof(Layer123), nameof(Layer124), nameof(Layer125), nameof(Layer126), nameof(Layer127), nameof(Layer128),
        nameof(Layer129), nameof(Layer130), nameof(Layer131), nameof(Layer132), nameof(Layer133), nameof(Layer134), nameof(Layer135), nameof(Layer136),
        nameof(Layer137), nameof(Layer138), nameof(Layer139), nameof(Layer140), nameof(Layer141), nameof(Layer142), nameof(Layer143), nameof(Layer144),
        nameof(Layer145), nameof(Layer146), nameof(Layer147), nameof(Layer148), nameof(Layer149), nameof(Layer150), nameof(Layer151), nameof(Layer152),
        nameof(Layer153), nameof(Layer154), nameof(Layer155), nameof(Layer156), nameof(Layer157), nameof(Layer158), nameof(Layer159), nameof(Layer160)
    )]
    public void ProcessLayeredMathInPlace(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            ref TransformData160 state = ref stream[index];
            Layer001(ref state);
            Layer002(ref state);
            Layer003(ref state);
            Layer004(ref state);
            Layer005(ref state);
            Layer006(ref state);
            Layer007(ref state);
            Layer008(ref state);
            Layer009(ref state);
            Layer010(ref state);
            Layer011(ref state);
            Layer012(ref state);
            Layer013(ref state);
            Layer014(ref state);
            Layer015(ref state);
            Layer016(ref state);
            Layer017(ref state);
            Layer018(ref state);
            Layer019(ref state);
            Layer020(ref state);
            Layer021(ref state);
            Layer022(ref state);
            Layer023(ref state);
            Layer024(ref state);
            Layer025(ref state);
            Layer026(ref state);
            Layer027(ref state);
            Layer028(ref state);
            Layer029(ref state);
            Layer030(ref state);
            Layer031(ref state);
            Layer032(ref state);
            Layer033(ref state);
            Layer034(ref state);
            Layer035(ref state);
            Layer036(ref state);
            Layer037(ref state);
            Layer038(ref state);
            Layer039(ref state);
            Layer040(ref state);
            Layer041(ref state);
            Layer042(ref state);
            Layer043(ref state);
            Layer044(ref state);
            Layer045(ref state);
            Layer046(ref state);
            Layer047(ref state);
            Layer048(ref state);
            Layer049(ref state);
            Layer050(ref state);
            Layer051(ref state);
            Layer052(ref state);
            Layer053(ref state);
            Layer054(ref state);
            Layer055(ref state);
            Layer056(ref state);
            Layer057(ref state);
            Layer058(ref state);
            Layer059(ref state);
            Layer060(ref state);
            Layer061(ref state);
            Layer062(ref state);
            Layer063(ref state);
            Layer064(ref state);
            Layer065(ref state);
            Layer066(ref state);
            Layer067(ref state);
            Layer068(ref state);
            Layer069(ref state);
            Layer070(ref state);
            Layer071(ref state);
            Layer072(ref state);
            Layer073(ref state);
            Layer074(ref state);
            Layer075(ref state);
            Layer076(ref state);
            Layer077(ref state);
            Layer078(ref state);
            Layer079(ref state);
            Layer080(ref state);
            Layer081(ref state);
            Layer082(ref state);
            Layer083(ref state);
            Layer084(ref state);
            Layer085(ref state);
            Layer086(ref state);
            Layer087(ref state);
            Layer088(ref state);
            Layer089(ref state);
            Layer090(ref state);
            Layer091(ref state);
            Layer092(ref state);
            Layer093(ref state);
            Layer094(ref state);
            Layer095(ref state);
            Layer096(ref state);
            Layer097(ref state);
            Layer098(ref state);
            Layer099(ref state);
            Layer100(ref state);
            Layer101(ref state);
            Layer102(ref state);
            Layer103(ref state);
            Layer104(ref state);
            Layer105(ref state);
            Layer106(ref state);
            Layer107(ref state);
            Layer108(ref state);
            Layer109(ref state);
            Layer110(ref state);
            Layer111(ref state);
            Layer112(ref state);
            Layer113(ref state);
            Layer114(ref state);
            Layer115(ref state);
            Layer116(ref state);
            Layer117(ref state);
            Layer118(ref state);
            Layer119(ref state);
            Layer120(ref state);
            Layer121(ref state);
            Layer122(ref state);
            Layer123(ref state);
            Layer124(ref state);
            Layer125(ref state);
            Layer126(ref state);
            Layer127(ref state);
            Layer128(ref state);
            Layer129(ref state);
            Layer130(ref state);
            Layer131(ref state);
            Layer132(ref state);
            Layer133(ref state);
            Layer134(ref state);
            Layer135(ref state);
            Layer136(ref state);
            Layer137(ref state);
            Layer138(ref state);
            Layer139(ref state);
            Layer140(ref state);
            Layer141(ref state);
            Layer142(ref state);
            Layer143(ref state);
            Layer144(ref state);
            Layer145(ref state);
            Layer146(ref state);
            Layer147(ref state);
            Layer148(ref state);
            Layer149(ref state);
            Layer150(ref state);
            Layer151(ref state);
            Layer152(ref state);
            Layer153(ref state);
            Layer154(ref state);
            Layer155(ref state);
            Layer156(ref state);
            Layer157(ref state);
            Layer158(ref state);
            Layer159(ref state);
            Layer160(ref state);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [ChunkedLayeredPipeline(
        nameof(Layer001), nameof(Layer002), nameof(Layer003), nameof(Layer004), nameof(Layer005), nameof(Layer006), nameof(Layer007), nameof(Layer008),
        nameof(Layer009), nameof(Layer010), nameof(Layer011), nameof(Layer012), nameof(Layer013), nameof(Layer014), nameof(Layer015), nameof(Layer016),
        nameof(Layer017), nameof(Layer018), nameof(Layer019), nameof(Layer020), nameof(Layer021), nameof(Layer022), nameof(Layer023), nameof(Layer024),
        nameof(Layer025), nameof(Layer026), nameof(Layer027), nameof(Layer028), nameof(Layer029), nameof(Layer030), nameof(Layer031), nameof(Layer032),
        nameof(Layer033), nameof(Layer034), nameof(Layer035), nameof(Layer036), nameof(Layer037), nameof(Layer038), nameof(Layer039), nameof(Layer040),
        nameof(Layer041), nameof(Layer042), nameof(Layer043), nameof(Layer044), nameof(Layer045), nameof(Layer046), nameof(Layer047), nameof(Layer048),
        nameof(Layer049), nameof(Layer050), nameof(Layer051), nameof(Layer052), nameof(Layer053), nameof(Layer054), nameof(Layer055), nameof(Layer056),
        nameof(Layer057), nameof(Layer058), nameof(Layer059), nameof(Layer060), nameof(Layer061), nameof(Layer062), nameof(Layer063), nameof(Layer064),
        nameof(Layer065), nameof(Layer066), nameof(Layer067), nameof(Layer068), nameof(Layer069), nameof(Layer070), nameof(Layer071), nameof(Layer072),
        nameof(Layer073), nameof(Layer074), nameof(Layer075), nameof(Layer076), nameof(Layer077), nameof(Layer078), nameof(Layer079), nameof(Layer080),
        nameof(Layer081), nameof(Layer082), nameof(Layer083), nameof(Layer084), nameof(Layer085), nameof(Layer086), nameof(Layer087), nameof(Layer088),
        nameof(Layer089), nameof(Layer090), nameof(Layer091), nameof(Layer092), nameof(Layer093), nameof(Layer094), nameof(Layer095), nameof(Layer096),
        nameof(Layer097), nameof(Layer098), nameof(Layer099), nameof(Layer100), nameof(Layer101), nameof(Layer102), nameof(Layer103), nameof(Layer104),
        nameof(Layer105), nameof(Layer106), nameof(Layer107), nameof(Layer108), nameof(Layer109), nameof(Layer110), nameof(Layer111), nameof(Layer112),
        nameof(Layer113), nameof(Layer114), nameof(Layer115), nameof(Layer116), nameof(Layer117), nameof(Layer118), nameof(Layer119), nameof(Layer120),
        nameof(Layer121), nameof(Layer122), nameof(Layer123), nameof(Layer124), nameof(Layer125), nameof(Layer126), nameof(Layer127), nameof(Layer128),
        nameof(Layer129), nameof(Layer130), nameof(Layer131), nameof(Layer132), nameof(Layer133), nameof(Layer134), nameof(Layer135), nameof(Layer136),
        nameof(Layer137), nameof(Layer138), nameof(Layer139), nameof(Layer140), nameof(Layer141), nameof(Layer142), nameof(Layer143), nameof(Layer144),
        nameof(Layer145), nameof(Layer146), nameof(Layer147), nameof(Layer148), nameof(Layer149), nameof(Layer150), nameof(Layer151), nameof(Layer152),
        nameof(Layer153), nameof(Layer154), nameof(Layer155), nameof(Layer156), nameof(Layer157), nameof(Layer158), nameof(Layer159), nameof(Layer160)
    )]
    public void ProcessLayeredMathChunked(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProcessOrdinaryMathChunked(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        int total = stream.Length;
        for (int chunkStart = 0; chunkStart < total; chunkStart += LayeredPipelineRuntime.ChunkSize)
        {
            int chunkEnd = Math.Min(chunkStart + LayeredPipelineRuntime.ChunkSize, total);
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer001(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer002(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer003(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer004(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer005(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer006(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer007(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer008(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer009(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer010(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer011(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer012(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer013(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer014(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer015(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer016(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer017(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer018(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer019(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer020(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer021(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer022(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer023(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer024(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer025(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer026(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer027(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer028(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer029(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer030(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer031(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer032(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer033(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer034(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer035(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer036(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer037(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer038(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer039(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer040(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer041(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer042(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer043(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer044(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer045(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer046(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer047(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer048(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer049(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer050(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer051(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer052(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer053(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer054(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer055(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer056(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer057(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer058(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer059(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer060(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer061(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer062(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer063(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer064(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer065(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer066(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer067(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer068(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer069(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer070(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer071(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer072(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer073(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer074(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer075(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer076(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer077(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer078(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer079(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer080(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer081(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer082(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer083(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer084(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer085(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer086(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer087(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer088(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer089(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer090(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer091(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer092(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer093(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer094(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer095(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer096(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer097(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer098(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer099(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer100(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer101(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer102(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer103(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer104(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer105(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer106(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer107(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer108(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer109(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer110(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer111(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer112(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer113(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer114(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer115(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer116(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer117(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer118(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer119(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer120(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer121(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer122(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer123(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer124(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer125(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer126(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer127(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer128(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer129(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer130(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer131(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer132(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer133(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer134(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer135(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer136(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer137(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer138(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer139(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer140(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer141(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer142(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer143(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer144(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer145(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer146(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer147(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer148(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer149(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer150(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer151(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer152(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer153(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer154(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer155(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer156(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer157(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer158(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer159(ref state);
            }
            for (int index = chunkStart; index < chunkEnd; index++)
            {
                ref TransformData160 state = ref stream[index];
                Layer160(ref state);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProcessOrdinaryMathInPlace(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            ref TransformData160 state = ref stream[index];
            Layer001(ref state);
            Layer002(ref state);
            Layer003(ref state);
            Layer004(ref state);
            Layer005(ref state);
            Layer006(ref state);
            Layer007(ref state);
            Layer008(ref state);
            Layer009(ref state);
            Layer010(ref state);
            Layer011(ref state);
            Layer012(ref state);
            Layer013(ref state);
            Layer014(ref state);
            Layer015(ref state);
            Layer016(ref state);
            Layer017(ref state);
            Layer018(ref state);
            Layer019(ref state);
            Layer020(ref state);
            Layer021(ref state);
            Layer022(ref state);
            Layer023(ref state);
            Layer024(ref state);
            Layer025(ref state);
            Layer026(ref state);
            Layer027(ref state);
            Layer028(ref state);
            Layer029(ref state);
            Layer030(ref state);
            Layer031(ref state);
            Layer032(ref state);
            Layer033(ref state);
            Layer034(ref state);
            Layer035(ref state);
            Layer036(ref state);
            Layer037(ref state);
            Layer038(ref state);
            Layer039(ref state);
            Layer040(ref state);
            Layer041(ref state);
            Layer042(ref state);
            Layer043(ref state);
            Layer044(ref state);
            Layer045(ref state);
            Layer046(ref state);
            Layer047(ref state);
            Layer048(ref state);
            Layer049(ref state);
            Layer050(ref state);
            Layer051(ref state);
            Layer052(ref state);
            Layer053(ref state);
            Layer054(ref state);
            Layer055(ref state);
            Layer056(ref state);
            Layer057(ref state);
            Layer058(ref state);
            Layer059(ref state);
            Layer060(ref state);
            Layer061(ref state);
            Layer062(ref state);
            Layer063(ref state);
            Layer064(ref state);
            Layer065(ref state);
            Layer066(ref state);
            Layer067(ref state);
            Layer068(ref state);
            Layer069(ref state);
            Layer070(ref state);
            Layer071(ref state);
            Layer072(ref state);
            Layer073(ref state);
            Layer074(ref state);
            Layer075(ref state);
            Layer076(ref state);
            Layer077(ref state);
            Layer078(ref state);
            Layer079(ref state);
            Layer080(ref state);
            Layer081(ref state);
            Layer082(ref state);
            Layer083(ref state);
            Layer084(ref state);
            Layer085(ref state);
            Layer086(ref state);
            Layer087(ref state);
            Layer088(ref state);
            Layer089(ref state);
            Layer090(ref state);
            Layer091(ref state);
            Layer092(ref state);
            Layer093(ref state);
            Layer094(ref state);
            Layer095(ref state);
            Layer096(ref state);
            Layer097(ref state);
            Layer098(ref state);
            Layer099(ref state);
            Layer100(ref state);
            Layer101(ref state);
            Layer102(ref state);
            Layer103(ref state);
            Layer104(ref state);
            Layer105(ref state);
            Layer106(ref state);
            Layer107(ref state);
            Layer108(ref state);
            Layer109(ref state);
            Layer110(ref state);
            Layer111(ref state);
            Layer112(ref state);
            Layer113(ref state);
            Layer114(ref state);
            Layer115(ref state);
            Layer116(ref state);
            Layer117(ref state);
            Layer118(ref state);
            Layer119(ref state);
            Layer120(ref state);
            Layer121(ref state);
            Layer122(ref state);
            Layer123(ref state);
            Layer124(ref state);
            Layer125(ref state);
            Layer126(ref state);
            Layer127(ref state);
            Layer128(ref state);
            Layer129(ref state);
            Layer130(ref state);
            Layer131(ref state);
            Layer132(ref state);
            Layer133(ref state);
            Layer134(ref state);
            Layer135(ref state);
            Layer136(ref state);
            Layer137(ref state);
            Layer138(ref state);
            Layer139(ref state);
            Layer140(ref state);
            Layer141(ref state);
            Layer142(ref state);
            Layer143(ref state);
            Layer144(ref state);
            Layer145(ref state);
            Layer146(ref state);
            Layer147(ref state);
            Layer148(ref state);
            Layer149(ref state);
            Layer150(ref state);
            Layer151(ref state);
            Layer152(ref state);
            Layer153(ref state);
            Layer154(ref state);
            Layer155(ref state);
            Layer156(ref state);
            Layer157(ref state);
            Layer158(ref state);
            Layer159(ref state);
            Layer160(ref state);
        }
    }
}
