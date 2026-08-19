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

        var movement = new ComparativeMovement2ComponentsBenchmarks { Amount = 100 };
        movement.Setup();
        try { movement.ResetMovement(); movement.DeltaECS_Movement2Components(); movement.ResetMovement(); movement.Arch_Movement2Components(); movement.ResetMovement(); movement.FrifloEngineECS_Movement2Components(); movement.ResetMovement(); movement.DefaultEcs_Movement2Components(); movement.ResetMovement(); movement.LeoEcsLite_Movement2Components(); }
        finally { movement.Cleanup(); }

        var distinct = new ComparativeMovement4ComponentsBenchmarks { Amount = 100 };
        distinct.Setup();
        try
        {
            distinct.ResetMovement4(); Require(distinct.DeltaECS_Movement4Components(), 2_000, "Delta movement four components");
            distinct.ResetMovement4(); Require(distinct.Arch_Movement4Components(), 2_000, "Arch movement four components");
            distinct.ResetMovement4(); Require(distinct.FrifloEngineECS_Movement4Components(), 2_000, "Friflo movement four components");
            distinct.ResetMovement4(); Require(distinct.DefaultEcs_Movement4Components(), 2_000, "Default movement four components");
            distinct.ResetMovement4(); Require(distinct.LeoEcsLite_Movement4Components(), 2_000, "Leo movement four components");
            distinct.ResetMovement4(); Require(distinct.DeltaECS_Movement4Components(), 2_000, "Delta movement four components after reset");
        }
        finally { distinct.Cleanup(); }

        var wide = new ComparativeWideArchetypeNarrowQueryBenchmarks { Amount = 100 };
        wide.Setup();
        try { Require(wide.DeltaECS_WideArchetypeNarrowQuery(), 900, "Delta wide archetype narrow query"); Require(wide.Arch_WideArchetypeNarrowQuery(), 900, "Arch wide archetype narrow query"); Require(wide.FrifloEngineECS_WideArchetypeNarrowQuery(), 900, "Friflo wide archetype narrow query"); Require(wide.DefaultEcs_WideArchetypeNarrowQuery(), 900, "Default wide archetype narrow query"); Require(wide.LeoEcsLite_WideArchetypeNarrowQuery(), 900, "Leo wide archetype narrow query"); }
        finally { wide.Cleanup(); }

        var sparse = new ComparativeSparseQueryBenchmarks { Amount = 100 };
        sparse.Setup();
        try { Require(sparse.DeltaECS_SparseWorldCachedQuery(), 25, "Delta sparse world cached query"); Require(sparse.Arch_SparseWorldCachedQuery(), 25, "Arch sparse world cached query"); Require(sparse.FrifloEngineECS_SparseWorldCachedQuery(), 25, "Friflo sparse world cached query"); Require(sparse.DefaultEcs_SparseWorldCachedQuery(), 25, "Default sparse world cached query"); Require(sparse.LeoEcsLite_SparseWorldCachedQuery(), 25, "Leo sparse world cached query"); Require(sparse.DeltaECS_SparseWorldColdQuery(), 25, "Delta sparse world cold query"); Require(sparse.Arch_SparseWorldColdQuery(), 25, "Arch sparse world cold query"); Require(sparse.FrifloEngineECS_SparseWorldColdQuery(), 25, "Friflo sparse world cold query"); Require(sparse.DefaultEcs_SparseWorldColdQuery(), 25, "Default sparse world cold query"); Require(sparse.LeoEcsLite_SparseWorldColdQuery(), 25, "Leo sparse world cold query"); }
        finally { sparse.Cleanup(); }
    }

    private static void RunStructuralList()
    {
        var b = new ComparativeStructuralListBenchmarks { Amount = 100, ChangeWidth = 4 };
        b.Setup();
        try
        {
            b.PrepareCreate(); Require(b.DeltaECS_List_CreateBatch(), 100, "Delta list create"); b.RestoreAfterIteration();
            b.PrepareDestroy(); Require(b.DeltaECS_List_DestroyBatch(), 100, "Delta list destroy"); b.RestoreAfterIteration();
            b.PrepareAdd(); Require(b.DeltaECS_List_AddBatch(), 100, "Delta list add"); b.RestoreAfterIteration();
            b.PrepareRemove(); Require(b.DeltaECS_List_RemoveBatch(), 100, "Delta list remove"); b.RestoreAfterIteration();

            b.PrepareArchListCreate(); Require(b.Arch_List_CreateBatch(), 100, "Arch list create"); b.RestoreAfterIteration();
            b.PrepareArchListDestroy(); Require(b.Arch_List_DestroyBatch(), 100, "Arch list destroy"); b.RestoreAfterIteration();
            b.PrepareArchListAdd(); Require(b.Arch_List_AddBatch(), 100, "Arch list add"); b.RestoreAfterIteration();
            b.PrepareArchListRemove(); Require(b.Arch_List_RemoveBatch(), 100, "Arch list remove"); b.RestoreAfterIteration();

            b.PrepareFrifloListCreate(); Require(b.FrifloEngineECS_List_CreateBatch(), 100, "Friflo list create"); b.RestoreAfterIteration();
            b.PrepareFrifloListDestroy(); Require(b.FrifloEngineECS_List_DestroyBatch(), 100, "Friflo list destroy"); b.RestoreAfterIteration();
            b.PrepareFrifloListAdd(); Require(b.FrifloEngineECS_List_AddBatch(), 100, "Friflo list add"); b.RestoreAfterIteration();
            b.PrepareFrifloListRemove(); Require(b.FrifloEngineECS_List_RemoveBatch(), 100, "Friflo list remove"); b.RestoreAfterIteration();

            b.PrepareDefaultListCreate(); Require(b.DefaultEcs_List_CreateBatch(), 100, "Default list create"); b.RestoreAfterIteration();
            b.PrepareDefaultListDestroy(); Require(b.DefaultEcs_List_DestroyBatch(), 100, "Default list destroy"); b.RestoreAfterIteration();
            b.PrepareDefaultListAdd(); Require(b.DefaultEcs_List_AddBatch(), 100, "Default list add"); b.RestoreAfterIteration();
            b.PrepareDefaultListRemove(); Require(b.DefaultEcs_List_RemoveBatch(), 100, "Default list remove"); b.RestoreAfterIteration();

            b.PrepareLeoListCreate(); Require(b.LeoEcsLite_List_CreateBatch(), 100, "Leo list create"); b.RestoreAfterIteration();
            b.PrepareLeoListDestroy(); Require(b.LeoEcsLite_List_DestroyBatch(), 100, "Leo list destroy"); b.RestoreAfterIteration();
            b.PrepareLeoListAdd(); Require(b.LeoEcsLite_List_AddBatch(), 100, "Leo list add"); b.RestoreAfterIteration();
            b.PrepareLeoListRemove(); Require(b.LeoEcsLite_List_RemoveBatch(), 100, "Leo list remove"); b.RestoreAfterIteration();
        }
        finally { b.RestoreAfterIteration(); }
    }

    private static void RunStructuralQuery()
    {
        var b = new ComparativeStructuralQueryBenchmarks { Amount = 100, ChangeWidth = 4 };
        b.Setup();
        try
        {
            b.PrepareCreate(); Require(b.DeltaECS_Query_CreateBatch(), 100, "Delta query create"); b.RestoreAfterIteration();
            b.PrepareDestroy(); Require(b.DeltaECS_Query_DestroyBatch(), 25, "Delta query destroy"); b.RestoreAfterIteration();
            b.PrepareAdd(); Require(b.DeltaECS_Query_AddBatch(), 25, "Delta query add"); b.RestoreAfterIteration();
            b.PrepareRemove(); Require(b.DeltaECS_Query_RemoveBatch(), 25, "Delta query remove"); b.RestoreAfterIteration();

            b.PrepareArchQueryCreate(); Require(b.Arch_Query_CreateBatch(), 100, "Arch query create"); b.RestoreAfterIteration();
            b.PrepareArchQueryDestroy(); Require(b.Arch_Query_DestroyBatch(), 25, "Arch query destroy"); b.RestoreAfterIteration();
            b.PrepareArchQueryAdd(); Require(b.Arch_Query_AddBatch(), 25, "Arch query add"); b.RestoreAfterIteration();
            b.PrepareArchQueryRemove(); Require(b.Arch_Query_RemoveBatch(), 25, "Arch query remove"); b.RestoreAfterIteration();

            b.PrepareFrifloQueryCreate(); Require(b.FrifloEngineECS_Query_CreateBatch(), 100, "Friflo query create"); b.RestoreAfterIteration();
            b.PrepareFrifloQueryDestroy(); Require(b.FrifloEngineECS_Query_DestroyBatch(), 25, "Friflo query destroy"); b.RestoreAfterIteration();
            b.PrepareFrifloQueryAdd(); Require(b.FrifloEngineECS_Query_AddBatch(), 25, "Friflo query add"); b.RestoreAfterIteration();
            b.PrepareFrifloQueryRemove(); Require(b.FrifloEngineECS_Query_RemoveBatch(), 25, "Friflo query remove"); b.RestoreAfterIteration();

            b.PrepareDefaultQueryCreate(); Require(b.DefaultEcs_Query_CreateBatch(), 100, "Default query create"); b.RestoreAfterIteration();
            b.PrepareDefaultQueryDestroy(); Require(b.DefaultEcs_Query_DestroyBatch(), 25, "Default query destroy"); b.RestoreAfterIteration();
            b.PrepareDefaultQueryAdd(); Require(b.DefaultEcs_Query_AddBatch(), 25, "Default query add"); b.RestoreAfterIteration();
            b.PrepareDefaultQueryRemove(); Require(b.DefaultEcs_Query_RemoveBatch(), 25, "Default query remove"); b.RestoreAfterIteration();

            b.PrepareLeoQueryCreate(); Require(b.LeoEcsLite_Query_CreateBatch(), 100, "Leo query create"); b.RestoreAfterIteration();
            b.PrepareLeoQueryDestroy(); Require(b.LeoEcsLite_Query_DestroyBatch(), 25, "Leo query destroy"); b.RestoreAfterIteration();
            b.PrepareLeoQueryAdd(); Require(b.LeoEcsLite_Query_AddBatch(), 25, "Leo query add"); b.RestoreAfterIteration();
            b.PrepareLeoQueryRemove(); Require(b.LeoEcsLite_Query_RemoveBatch(), 25, "Leo query remove"); b.RestoreAfterIteration();
        }
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
