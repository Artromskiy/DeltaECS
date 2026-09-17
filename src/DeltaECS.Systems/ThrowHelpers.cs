namespace Delta.ECS.Systems;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfNull([NotNull] object? value, string parameterName)
    {
        if (value is null)
        {
            ThrowNull(parameterName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            ThrowNegative(value, parameterName);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNull(string parameterName)
    {
        throw new ArgumentNullException(parameterName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNegative(int value, string parameterName)
    {
#if NETSTANDARD2_1
        throw new ArgumentOutOfRangeException(parameterName);
#else
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowInvalidComponentIds(string parameterName)
        => throw new ArgumentException("Component ids must be valid.", parameterName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowWorldMismatch()
        => throw new ArgumentException("A system must reference the scheduler world.", "system");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDuplicateSystem()
        => throw new ArgumentException("The same system instance cannot be registered twice.", "system");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSystemWorldChanged()
        => throw new InvalidOperationException("A system changed its world after registration.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSchedulerAlreadyExecuting()
        => throw new InvalidOperationException("A system scheduler tick is already active.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowSchedulerChangeDuringExecution()
        => throw new InvalidOperationException("The scheduler cannot be changed while a tick is active.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDisposedScheduler()
    {
#if NETSTANDARD2_1
        throw new ObjectDisposedException(nameof(SystemScheduler));
#else
        ObjectDisposedException.ThrowIf(true, nameof(SystemScheduler));
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowWorkersPrepareDuringExecution()
        => throw new InvalidOperationException("Cannot prepare scheduler workers during execution.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDisposedWorkers()
    {
#if NETSTANDARD2_1
        throw new ObjectDisposedException("SchedulerWorkers");
#else
        ObjectDisposedException.ThrowIf(true, "SchedulerWorkers");
#endif
    }
}
