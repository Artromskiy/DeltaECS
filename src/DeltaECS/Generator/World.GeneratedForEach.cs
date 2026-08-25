namespace Delta.ECS;

using System.ComponentModel;

public sealed partial class World
{
    /// <summary>Executes a compiler-generated dense-query invoker.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ExecuteGeneratedForEach<TInvoker>(in Query handle, ref TInvoker invoker, bool hasWrites)
        where TInvoker : struct, IGeneratedForEachInvoker
    {
        ValidateQuery(in handle);
        var cached = handle.Cached;
        ReadOnlySpan<ArchetypePlan> plans = cached.MatchingPlans();
        uint writeTick = 0;
        Stamp writeStamp = default;
        if (hasWrites)
        {
            for (int planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                if (plans.Ref(planIndex).Chunks.Length == 0)
                {
                    continue;
                }

                writeTick = ReserveQueryWrite(out writeStamp);
                break;
            }
        }

        BeginQueryLease();
        try
        {
            for (int planIndex = 0; planIndex < plans.Length; planIndex++)
            {
                ArchetypePlan plan = plans.Ref(planIndex);
                ReadOnlySpan<ChunkPlan> chunks = plan.Chunks;
                for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
                {
                    var slots = new GeneratedQuerySlots(plan, chunks.Ref(chunkIndex), writeTick, writeStamp);
                    invoker.Invoke(ref slots);
                }
            }
        }
        finally
        {
            EndQueryLease();
        }
    }
}
