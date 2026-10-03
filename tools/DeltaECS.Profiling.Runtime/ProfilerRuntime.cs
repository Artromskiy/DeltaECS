using System.Runtime.CompilerServices;

namespace DeltaECS.Profiling;

/// <summary>Process-local entry point used by instrumented methods.</summary>
public static class ProfilerRuntime
{
    [ThreadStatic]
    private static CallProfiler? s_current;

    /// <summary>Starts collection on the current thread.</summary>
    public static CallProfiler Start(
        int maxDepth = 32,
        int sampleCapacity = 1_048_576,
        ReadOnlySpan<int> rootMethodIds = default)
    {
        var profiler = new CallProfiler(maxDepth, sampleCapacity, rootMethodIds);
        s_current = profiler;
        return profiler;
    }

    /// <summary>Reattaches an existing warmed collector on the current thread.</summary>
    public static void Attach(CallProfiler profiler)
    {
        ArgumentNullException.ThrowIfNull(profiler);
        if (s_current is not null)
        {
            throw new InvalidOperationException("A profiler is already active on this thread.");
        }

        s_current = profiler;
    }

    /// <summary>Stops collection without formatting the captured samples.</summary>
    public static CallProfiler? Detach()
    {
        CallProfiler? profiler = s_current;
        s_current = null;
        return profiler;
    }

    /// <summary>Marks an instrumented method entry. Called only from woven IL.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Enter(int methodId)
    {
        s_current?.EnterMethod(methodId);
    }

    /// <summary>Marks an instrumented method exit. Called only from woven IL.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Leave(int methodId)
    {
        s_current?.ExitMethod(methodId);
    }

}
