using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Metalama.Framework.Aspects;

namespace DeltaECS.LayeredPipeline;

[AttributeUsage(AttributeTargets.Method)]
public sealed class EcsLayerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LayeredPipelineAttribute : OverrideMethodAspect
{
    private readonly string _firstLayer;
    private readonly string _secondLayer;

    public LayeredPipelineAttribute(string firstLayer, string secondLayer)
    {
        _firstLayer = firstLayer;
        _secondLayer = secondLayer;
    }

    public override dynamic? OverrideMethod()
    {
        var first = meta.Target.Type.AllMethods
            .Where(method => method.Name == _firstLayer)
            .Single();
        var second = meta.Target.Type.AllMethods
            .Where(method => method.Name == _secondLayer)
            .Single();
        if (meta.Target.Parameters["stream"].Value is not TransformData160[] nonNullStream)
        {
            throw new InvalidOperationException("The layered pipeline requires a TransformData160[] stream parameter.");
        }

        TransformData160[] stream = nonNullStream;

        int total = stream.Length;
        Span<TransformData160> ping = stackalloc TransformData160[LayeredPipelineRuntime.BatchSize];
        Span<TransformData160> pong = stackalloc TransformData160[LayeredPipelineRuntime.BatchSize];

        for (int offset = 0; offset < total; offset += LayeredPipelineRuntime.BatchSize)
        {
            int count = Math.Min(LayeredPipelineRuntime.BatchSize, total - offset);
            stream.AsSpan(offset, count).CopyTo(ping);

            for (int index = 0; index < count; index++)
            {
                pong[index] = first.Invoke(ping[index]);
            }

            for (int index = 0; index < count; index++)
            {
                ping[index] = second.Invoke(pong[index]);
            }

            ping[..count].CopyTo(stream.AsSpan(offset, count));
        }

        return null;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class InPlaceLayeredPipelineAttribute : OverrideMethodAspect
{
    private readonly string[] _layers;

    public InPlaceLayeredPipelineAttribute(params string[] layers)
    {
        _layers = layers;
    }

    public override dynamic? OverrideMethod()
    {
        var layers = _layers.Select(layerName => meta.Target.Type.AllMethods
            .Where(method => method.Name == layerName)
            .Single())
            .ToArray();
        if (meta.Target.Parameters["stream"].Value is not TransformData160[] nonNullStream)
        {
            throw new InvalidOperationException("The in-place layered pipeline requires a TransformData160[] stream parameter.");
        }

        TransformData160[] stream = nonNullStream;

        for (int index = 0; index < stream.Length; index++)
        {
            ref TransformData160 state = ref stream[index];
            foreach (var layer in layers)
            {
                layer.Invoke(state);
            }
        }

        return null;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ChunkedLayeredPipelineAttribute : OverrideMethodAspect
{
    private readonly string[] _layers;

    public ChunkedLayeredPipelineAttribute(params string[] layers)
    {
        _layers = layers;
    }

    public override dynamic? OverrideMethod()
    {
        var layers = _layers.Select(layerName => meta.Target.Type.AllMethods
            .Where(method => method.Name == layerName)
            .Single())
            .ToArray();
        if (meta.Target.Parameters["stream"].Value is not TransformData160[] nonNullStream)
        {
            throw new InvalidOperationException("The chunked layered pipeline requires a TransformData160[] stream parameter.");
        }

        TransformData160[] stream = nonNullStream;
        int total = stream.Length;

        for (int chunkStart = 0; chunkStart < total; chunkStart += LayeredPipelineRuntime.ChunkSize)
        {
            int chunkEnd = Math.Min(chunkStart + LayeredPipelineRuntime.ChunkSize, total);
            foreach (var layer in layers)
            {
                for (int index = chunkStart; index < chunkEnd; index++)
                {
                    ref TransformData160 state = ref stream[index];
                    layer.Invoke(state);
                }
            }
        }

        return null;
    }
}

[StructLayout(LayoutKind.Sequential, Size = 160)]
public struct TransformData160
{
    public System.Numerics.Vector4 A0;
    public System.Numerics.Vector4 A1;
    public System.Numerics.Vector4 A2;
    public System.Numerics.Vector4 A3;
    public System.Numerics.Vector4 A4;
    public System.Numerics.Vector4 A5;
    public System.Numerics.Vector4 A6;
    public System.Numerics.Vector4 A7;
    public System.Numerics.Vector4 A8;
    public System.Numerics.Vector4 A9;
}

public static class LayeredPipelineRuntime
{
    public const int BatchSize = 32;
    public const int ChunkSize = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransformData160 Layer1(TransformData160 data)
    {
        data.A0.X += 1f;
        data.A1.Y = data.A0.X + data.A1.Y;
        return data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TransformData160 Layer2(TransformData160 data)
    {
        data.A2.Z = data.A1.Y + data.A2.Z;
        data.A3.W = data.A2.Z + data.A3.W;
        return data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer1InPlace(ref TransformData160 data)
    {
        data.A0.X += 1f;
        data.A1.Y = data.A0.X + data.A1.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Layer2InPlace(ref TransformData160 data)
    {
        data.A2.Z = data.A1.Y + data.A2.Z;
        data.A3.W = data.A2.Z + data.A3.W;
    }
}

public sealed class TransformPipeline
{
    [EcsLayer]
    public static TransformData160 Layer1Local(TransformData160 data)
        => LayeredPipelineRuntime.Layer1(data);

    [EcsLayer]
    public static TransformData160 Layer2World(TransformData160 data)
        => LayeredPipelineRuntime.Layer2(data);

    [EcsLayer]
    public static void Layer1LocalInPlace(ref TransformData160 data)
        => LayeredPipelineRuntime.Layer1InPlace(ref data);

    [EcsLayer]
    public static void Layer2WorldInPlace(ref TransformData160 data)
        => LayeredPipelineRuntime.Layer2InPlace(ref data);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [LayeredPipeline(nameof(Layer1Local), nameof(Layer2World))]
    public void ProcessLayered(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            TransformData160 data = stream[index];
            data = Layer1Local(data);
            data = Layer2World(data);
            stream[index] = data;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProcessOrdinary(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            TransformData160 data = stream[index];
            data = Layer1Local(data);
            data = Layer2World(data);
            stream[index] = data;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [InPlaceLayeredPipeline(nameof(Layer1LocalInPlace), nameof(Layer2WorldInPlace))]
    public void ProcessLayeredInPlace(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            ref TransformData160 state = ref stream[index];
            Layer1LocalInPlace(ref state);
            Layer2WorldInPlace(ref state);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProcessOrdinaryInPlace(TransformData160[] stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        for (int index = 0; index < stream.Length; index++)
        {
            ref TransformData160 state = ref stream[index];
            Layer1LocalInPlace(ref state);
            Layer2WorldInPlace(ref state);
        }
    }
}
