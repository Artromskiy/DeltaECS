using System.Diagnostics.CodeAnalysis;
using Metalama.Framework.Aspects;

namespace DeltaECS.Profiling;

/// <summary>Wraps a probe method in a compile-time generated profiler boundary.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ProfileMethodAttribute : OverrideMethodAspect
{
    private readonly int _methodId;

    [SuppressMessage(
        "Design",
        "CA1019:Define accessors for attribute arguments",
        Justification = "The method ID is consumed only by the compile-time Metalama template.")]
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
