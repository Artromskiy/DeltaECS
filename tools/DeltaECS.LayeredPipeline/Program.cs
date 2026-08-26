using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DeltaECS.LayeredPipeline;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Contains("--list", StringComparer.Ordinal))
        {
            Console.WriteLine(nameof(LayeredPipelineBenchmarks.ProcessOrdinaryMathInPlace));
            Console.WriteLine(nameof(LayeredPipelineBenchmarks.ProcessLayeredMathInPlace));
            Console.WriteLine(nameof(LayeredPipelineBenchmarks.ProcessOrdinaryMathChunked));
            Console.WriteLine(nameof(LayeredPipelineBenchmarks.ProcessLayeredMathChunked));
            return;
        }

        if (args.Contains("--smoke", StringComparer.Ordinal))
        {
            LayeredPipelineBenchmarks.VerifyEquivalentResults();
            Console.WriteLine("Layered pipeline smoke check passed.");
            return;
        }

        if (args.Contains("--jit", StringComparer.Ordinal))
        {
            LayeredPipelineBenchmarks.VerifyEquivalentResults();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

[MemoryDiagnoser]
public class LayeredPipelineBenchmarks
{
    [Params(1_000_000, 10_000_000)]
    public int Amount { get; set; }

    private TransformData160[] _stream = [];
    private MathStressPipeline _mathStressPipeline = new();

    [GlobalSetup]
    public void Setup()
    {
        _stream = CreateInput(Amount);
    }

    [Benchmark(Baseline = true)]
    public float ProcessOrdinaryMathInPlace()
    {
        _mathStressPipeline.ProcessOrdinaryMathInPlace(_stream);
        return _stream[Amount - 1].A3.W;
    }

    [Benchmark]
    public float ProcessLayeredMathInPlace()
    {
        _mathStressPipeline.ProcessLayeredMathInPlace(_stream);
        return _stream[Amount - 1].A3.W;
    }

    [Benchmark]
    public float ProcessOrdinaryMathChunked()
    {
        _mathStressPipeline.ProcessOrdinaryMathChunked(_stream);
        return _stream[Amount - 1].A3.W;
    }

    [Benchmark]
    public float ProcessLayeredMathChunked()
    {
        _mathStressPipeline.ProcessLayeredMathChunked(_stream);
        return _stream[Amount - 1].A3.W;
    }

    internal static void VerifyEquivalentResults()
    {
        const int amount = 257;
        TransformData160[] ordinary = CreateInput(amount);
        TransformData160[] layered = (TransformData160[])ordinary.Clone();
        TransformData160[] ordinaryInPlace = (TransformData160[])ordinary.Clone();
        TransformData160[] layeredInPlace = (TransformData160[])ordinary.Clone();
        var pipeline = new TransformPipeline();

        pipeline.ProcessOrdinary(ordinary);
        pipeline.ProcessLayered(layered);
        pipeline.ProcessOrdinaryInPlace(ordinaryInPlace);
        pipeline.ProcessLayeredInPlace(layeredInPlace);

        for (int index = 0; index < amount; index++)
        {
            if (!ordinary[index].Equals(layered[index])
                || !ordinary[index].Equals(ordinaryInPlace[index])
                || !ordinary[index].Equals(layeredInPlace[index]))
            {
                throw new InvalidOperationException($"Layered result differs at element {index}.");
            }
        }

        TransformData160[] manyOrdinary = CreateInput(amount);
        TransformData160[] manyLayered = (TransformData160[])manyOrdinary.Clone();
        TransformData160[] manyChunkedOrdinary = (TransformData160[])manyOrdinary.Clone();
        TransformData160[] manyChunkedLayered = (TransformData160[])manyOrdinary.Clone();
        var manyLayerPipeline = new MathStressPipeline();

        manyLayerPipeline.ProcessOrdinaryMathInPlace(manyOrdinary);
        manyLayerPipeline.ProcessLayeredMathInPlace(manyLayered);
        manyLayerPipeline.ProcessOrdinaryMathChunked(manyChunkedOrdinary);
        manyLayerPipeline.ProcessLayeredMathChunked(manyChunkedLayered);

        for (int index = 0; index < amount; index++)
        {
            if (!manyOrdinary[index].Equals(manyLayered[index])
                || !manyOrdinary[index].Equals(manyChunkedOrdinary[index])
                || !manyOrdinary[index].Equals(manyChunkedLayered[index]))
            {
                throw new InvalidOperationException($"Many-layer result differs at element {index}.");
            }
        }
    }

    private static TransformData160[] CreateInput(int amount)
    {
        var values = new TransformData160[amount];
        for (int index = 0; index < values.Length; index++)
        {
            values[index].A0.X = index;
            values[index].A1.Y = 1;
            values[index].A2.Z = 2;
            values[index].A3.W = 3;
        }

        return values;
    }
}
