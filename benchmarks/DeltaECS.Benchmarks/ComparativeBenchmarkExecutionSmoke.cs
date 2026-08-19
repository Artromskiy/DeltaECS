namespace DVG.ECS.Benchmarks;

/// <summary>Runs every supported unified method once at Amount=100 without BDN.</summary>
public static class ComparativeBenchmarkExecutionSmoke
{
    public static void RunAmount100()
    {
        RunIteration();
        RunStructuralList();
        RunStructuralQuery();
        RunStructuralAtomic();
    }

    private static void RunIteration()
    {
        var dense = new ComparativeDenseIterationBenchmarks { Amount = 100 };
        dense.Setup();
        try { Require(dense.DeltaECS_Dense(), 5_050, "Delta dense"); Require(dense.Arch_Dense(), 5_050, "Arch dense"); Require(dense.FrifloEngineECS_Dense(), 5_050, "Friflo dense"); Require(dense.DefaultEcs_Dense(), 5_050, "Default dense"); Require(dense.LeoEcsLite_Dense(), 5_050, "Leo dense"); }
        finally { dense.Cleanup(); }

        var movement = new ComparativeMovementBenchmarks { Amount = 100 };
        movement.Setup();
        try { movement.ResetMovement(); movement.DeltaECS_Movement(); movement.ResetMovement(); movement.Arch_Movement(); movement.ResetMovement(); movement.FrifloEngineECS_Movement(); movement.ResetMovement(); movement.DefaultEcs_Movement(); movement.ResetMovement(); movement.LeoEcsLite_Movement(); }
        finally { movement.Cleanup(); }

        var distinct = new ComparativeDistinctRowsBenchmarks { Amount = 100 };
        distinct.Setup();
        try { Require(distinct.DeltaECS_DistinctRows(), 1_000, "Delta distinct"); Require(distinct.Arch_DistinctRows(), 1_000, "Arch distinct"); Require(distinct.FrifloEngineECS_DistinctRows(), 1_000, "Friflo distinct"); Require(distinct.DefaultEcs_DistinctRows(), 1_000, "Default distinct"); Require(distinct.LeoEcsLite_DistinctRows(), 1_000, "Leo distinct"); }
        finally { distinct.Cleanup(); }

        var wide = new ComparativeWideNarrowBenchmarks { Amount = 100 };
        wide.Setup();
        try { Require(wide.DeltaECS_WideNarrow(), 900, "Delta wide"); Require(wide.Arch_WideNarrow(), 900, "Arch wide"); Require(wide.FrifloEngineECS_WideNarrow(), 900, "Friflo wide"); Require(wide.DefaultEcs_WideNarrow(), 900, "Default wide"); Require(wide.LeoEcsLite_WideNarrow(), 900, "Leo wide"); }
        finally { wide.Cleanup(); }

        var sparse = new ComparativeSparseQueryBenchmarks { Amount = 100 };
        sparse.Setup();
        try { Require(sparse.DeltaECS_SparseCached(), 25, "Delta sparse cached"); Require(sparse.Arch_SparseCached(), 25, "Arch sparse cached"); Require(sparse.FrifloEngineECS_SparseCached(), 25, "Friflo sparse cached"); Require(sparse.DefaultEcs_SparseCached(), 25, "Default sparse cached"); Require(sparse.LeoEcsLite_SparseCached(), 25, "Leo sparse cached"); Require(sparse.DeltaECS_SparseCold(), 25, "Delta sparse cold"); Require(sparse.Arch_SparseCold(), 25, "Arch sparse cold"); Require(sparse.FrifloEngineECS_SparseCold(), 25, "Friflo sparse cold"); Require(sparse.DefaultEcs_SparseCold(), 25, "Default sparse cold"); Require(sparse.LeoEcsLite_SparseCold(), 25, "Leo sparse cold"); }
        finally { sparse.Cleanup(); }
    }

    private static void RunStructuralList()
    {
        var b = new ComparativeStructuralListBenchmarks { Amount = 100, ChangeWidth = 4 };
        b.Setup();
        try { b.PrepareCreate(); Require(b.DeltaECS_List_CreateBatch(), 100, "list create"); b.RestoreAfterIteration(); b.PrepareDestroy(); Require(b.DeltaECS_List_DestroyBatch(), 100, "list destroy"); b.RestoreAfterIteration(); b.PrepareAdd(); Require(b.DeltaECS_List_AddBatch(), 100, "list add"); b.RestoreAfterIteration(); b.PrepareRemove(); Require(b.DeltaECS_List_RemoveBatch(), 100, "list remove"); b.RestoreAfterIteration(); }
        finally { b.RestoreAfterIteration(); }
    }

    private static void RunStructuralQuery()
    {
        var b = new ComparativeStructuralQueryBenchmarks { Amount = 100, ChangeWidth = 4 };
        b.Setup();
        try { b.PrepareCreate(); Require(b.DeltaECS_Query_CreateBatch(), 100, "query create"); b.RestoreAfterIteration(); b.PrepareDestroy(); Require(b.DeltaECS_Query_DestroyBatch(), 25, "query destroy"); b.RestoreAfterIteration(); b.PrepareAdd(); Require(b.DeltaECS_Query_AddBatch(), 25, "query add"); b.RestoreAfterIteration(); b.PrepareRemove(); Require(b.DeltaECS_Query_RemoveBatch(), 25, "query remove"); b.RestoreAfterIteration(); }
        finally { b.RestoreAfterIteration(); }
    }

    private static void RunStructuralAtomic()
    {
        var b = new ComparativeStructuralAtomicBenchmarks { Amount = 100, ChangeWidth = 4 };
        b.Setup();
        try
        {
            b.PrepareAtomic(); Require(b.DeltaECS_Atomic_Create(), 1, "Delta atomic create"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.Arch_Atomic_Create(), 1, "Arch atomic create"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.FrifloEngineECS_Atomic_Create(), 1, "Friflo atomic create"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.DefaultEcs_Atomic_Create(), 1, "Default atomic create"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.LeoEcsLite_Atomic_Create(), 1, "Leo atomic create"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.DeltaECS_Atomic_Destroy(), 1, "Delta atomic destroy"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.Arch_Atomic_Destroy(), 1, "Arch atomic destroy"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.FrifloEngineECS_Atomic_Destroy(), 1, "Friflo atomic destroy"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.DefaultEcs_Atomic_Destroy(), 1, "Default atomic destroy"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.LeoEcsLite_Atomic_Destroy(), 1, "Leo atomic destroy"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.DeltaECS_Atomic_Add(), 4, "Delta atomic add"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.Arch_Atomic_Add(), 4, "Arch atomic add"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.FrifloEngineECS_Atomic_Add(), 4, "Friflo atomic add"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.DefaultEcs_Atomic_Add(), 4, "Default atomic add"); b.RestoreAfterIteration();
            b.PrepareAtomic(); Require(b.LeoEcsLite_Atomic_Add(), 4, "Leo atomic add"); b.RestoreAfterIteration();
            b.PrepareDeltaRemove(); Require(b.DeltaECS_Atomic_Remove(), 4, "Delta atomic remove"); b.RestoreAfterIteration();
            b.PrepareArchRemove(); Require(b.Arch_Atomic_Remove(), 4, "Arch atomic remove"); b.RestoreAfterIteration();
            b.PrepareFrifloRemove(); Require(b.FrifloEngineECS_Atomic_Remove(), 4, "Friflo atomic remove"); b.RestoreAfterIteration();
            b.PrepareDefaultRemove(); Require(b.DefaultEcs_Atomic_Remove(), 4, "Default atomic remove"); b.RestoreAfterIteration();
            b.PrepareLeoRemove(); Require(b.LeoEcsLite_Atomic_Remove(), 4, "Leo atomic remove"); b.RestoreAfterIteration();
        }
        finally { b.RestoreAfterIteration(); b.Cleanup(); }
    }

    private static void Require<T>(T actual, T expected, string name) where T : IEquatable<T>
    {
        if (!actual.Equals(expected)) throw new InvalidOperationException($"{name} returned {actual}, expected {expected}.");
    }
}
