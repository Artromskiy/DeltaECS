using Metalama.Framework.Aspects;

namespace DeltaECS.Profiling;

/// <summary>Wraps a probe method in a compile-time generated profiler boundary.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ProfileMethodAttribute : OverrideMethodAspect
{
    private readonly int _methodId;

    public ProfileMethodAttribute(int methodId)
    {
        _methodId = methodId;
    }

    public override dynamic? OverrideMethod()
    {
        ProfilerRuntime.Enter(_methodId);
        try
        {
            return meta.Proceed();
        }
        finally
        {
            ProfilerRuntime.Leave(_methodId);
        }
    }
}
