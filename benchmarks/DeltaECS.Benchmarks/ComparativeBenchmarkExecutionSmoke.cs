namespace Delta.ECS.Benchmarks;

/// <summary>Runs the unified iteration matrix once without BenchmarkDotNet.</summary>
public static class ComparativeBenchmarkExecutionSmoke
{
    public static void RunAmount100()
    {
        RunDense();
        RunMovement2();
        RunMovement4();
        RunWide();
        RunSparse();
        RunWidePayloadPartialRead();
    }

    private static void RunDense()
    {
        var benchmark = new ComparativeDenseIterationBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            Require(benchmark.DeltaECSDense(), 5_050, "Delta dense");
            Require(benchmark.ArchDense(), 5_050, "Arch dense");
            Require(benchmark.FrifloEngineECSDense(), 5_050, "Friflo dense");
            Require(benchmark.DefaultEcsDense(), 5_050, "Default dense");
            Require(benchmark.LeoEcsLiteDense(), 5_050, "Leo dense");
        }
        finally { benchmark.Cleanup(); }
    }

    private static void RunMovement2()
    {
        var benchmark = new ComparativeMovement2ComponentsBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            foreach (var run in new Func<double>[]
            {
                benchmark.DeltaECSMovement2Components,
                benchmark.ArchMovement2Components,
                benchmark.FrifloEngineECSMovement2Components,
                benchmark.DefaultEcsMovement2Components,
                benchmark.LeoEcsLiteMovement2Components
            })
            {
                benchmark.ResetMovement();
                RequireApproximately(run(), Movement2Expected(benchmark.Amount), "Movement2");
            }
        }
        finally { benchmark.Cleanup(); }
    }

    private static void RunMovement4()
    {
        var benchmark = new ComparativeMovement4ComponentsBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            foreach (var run in new Func<int>[]
            {
                benchmark.DeltaECSMovement4Components,
                benchmark.ArchMovement4Components,
                benchmark.FrifloEngineECSMovement4Components,
                benchmark.DefaultEcsMovement4Components,
                benchmark.LeoEcsLiteMovement4Components
            })
            {
                benchmark.ResetMovement4();
                Require(run(), 2_000, "Movement4");
            }
        }
        finally { benchmark.Cleanup(); }
    }

    private static void RunWide()
    {
        var benchmark = new ComparativeWideArchetypeNarrowQueryBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            Require(benchmark.DeltaECSWideArchetypeNarrowQuery(), 900, "Delta wide");
            Require(benchmark.ArchWideArchetypeNarrowQuery(), 900, "Arch wide");
            Require(benchmark.FrifloEngineECSWideArchetypeNarrowQuery(), 900, "Friflo wide");
            Require(benchmark.DefaultEcsWideArchetypeNarrowQuery(), 900, "Default wide");
            Require(benchmark.LeoEcsLiteWideArchetypeNarrowQuery(), 900, "Leo wide");
        }
        finally { benchmark.Cleanup(); }
    }

    private static void RunSparse()
    {
        var benchmark = new ComparativeSparseQueryBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            Require(benchmark.DeltaECSSparseWorldQueryPlan(), 75, "Delta sparse");
            Require(benchmark.ArchSparseWorldQueryPlan(), 75, "Arch sparse");
            Require(benchmark.FrifloEngineECSSparseWorldQueryPlan(), 75, "Friflo sparse");
            Require(benchmark.DefaultEcsSparseWorldQueryPlan(), 75, "Default sparse");
            Require(benchmark.LeoEcsLiteSparseWorldQueryPlan(), 75, "Leo sparse");
        }
        finally { benchmark.Cleanup(); }
    }

    private static void RunWidePayloadPartialRead()
    {
        var benchmark = new WidePayloadPartialReadIterationBenchmarks { Amount = 100 };
        benchmark.Setup();
        try
        {
            Require(benchmark.DeltaECSWidePayloadPartialRead(), 900, "Delta wide payload");
        }
        finally { benchmark.Cleanup(); }
    }

    private static void Require<T>(T actual, T expected, string name) where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
        {
            throw new InvalidOperationException($"{name} returned {actual}, expected {expected}.");
        }
    }

    private static void RequireApproximately(double actual, double expected, string name)
    {
        if (Math.Abs(actual - expected) > 0.0001)
        {
            throw new InvalidOperationException($"{name} returned {actual}, expected {expected}.");
        }
    }

    private static double Movement2Expected(int amount) => amount * ((1f + 3f / 60f) + (2f + 4f / 60f));
}
