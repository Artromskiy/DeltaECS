namespace Delta.ECS.Systems;

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Delta.ECS;

/// <summary>
/// Compiles world-local system access metadata into deterministic execution
/// batches. Independent batches run concurrently; structural and unknown
/// systems form exclusive phases.
/// </summary>
public sealed class SystemScheduler : IDisposable
{
    private readonly World _world;
    private readonly List<ISystem> _systems = new();
    private readonly int _workerCount;
    private readonly SchedulerWorkers? _workers;
    private ScheduleBatch[] _batches = Array.Empty<ScheduleBatch>();
    private int _executing;
    private bool _built;
    private bool _disposed;

    /// <summary>Creates a scheduler for one world.</summary>
    /// <param name="world">World shared by all registered systems.</param>
    /// <param name="workerCount">
    /// Maximum scheduler workers. Zero selects the processor count; values
    /// above the processor count are clamped.
    /// </param>
    public SystemScheduler(World world, int workerCount = 0)
    {
#if NETSTANDARD2_1
        if (world is null)
        {
            throw new ArgumentNullException(nameof(world));
        }

        if (workerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount));
        }
#else
        ArgumentNullException.ThrowIfNull(world, nameof(world));
        ArgumentOutOfRangeException.ThrowIfNegative(workerCount, nameof(workerCount));
#endif

        _world = world;
        int processorCount = Math.Max(1, Environment.ProcessorCount);
        _workerCount = Math.Min(workerCount == 0 ? processorCount : workerCount, processorCount);
        if (_workerCount > 1)
        {
            _workers = new SchedulerWorkers(_workerCount);
        }
    }

    /// <summary>Gets the world shared by the scheduler's systems.</summary>
    public World World => _world;

    /// <summary>Gets the resolved worker count.</summary>
    public int WorkerCount => _workerCount;

    /// <summary>Gets the number of registered systems.</summary>
    public int Count => _systems.Count;

    /// <summary>Gets whether the current system list has a compiled schedule.</summary>
    public bool IsBuilt => _built;

    /// <summary>Gets a registered system by registration order.</summary>
    public ISystem this[int index] => _systems[index];

    /// <summary>Adds a world-bound system and invalidates the compiled schedule.</summary>
    public void Add(ISystem system)
    {
        EnsureUsable();
        EnsureNotExecuting();
#if NETSTANDARD2_1
        if (system is null)
        {
            throw new ArgumentNullException(nameof(system));
        }
#else
        ArgumentNullException.ThrowIfNull(system, nameof(system));
#endif

        if (!ReferenceEquals(system.World, _world))
        {
            throw new ArgumentException("A system must reference the scheduler world.", nameof(system));
        }

        for (int index = 0; index < _systems.Count; index++)
        {
            if (ReferenceEquals(_systems[index], system))
            {
                throw new ArgumentException("The same system instance cannot be registered twice.", nameof(system));
            }
        }

        _systems.Add(system);
        _built = false;
    }

    /// <summary>Removes a registered system by reference.</summary>
    public bool Remove(ISystem system)
    {
        EnsureUsable();
        EnsureNotExecuting();
        if (system is null)
        {
            return false;
        }

        for (int index = 0; index < _systems.Count; index++)
        {
            if (!ReferenceEquals(_systems[index], system))
            {
                continue;
            }

            _systems.RemoveAt(index);
            _built = false;
            return true;
        }

        return false;
    }

    /// <summary>Removes all registered systems and invalidates the schedule.</summary>
    public void Clear()
    {
        EnsureUsable();
        EnsureNotExecuting();
        _systems.Clear();
        _batches = Array.Empty<ScheduleBatch>();
        _built = false;
    }

    /// <summary>Compiles the current registration list into deterministic batches.</summary>
    public void Build()
    {
        EnsureUsable();
        EnsureNotExecuting();
        BuildCore();
    }

    private void BuildCore()
    {
        int systemCount = _systems.Count;
        if (systemCount == 0)
        {
            _batches = Array.Empty<ScheduleBatch>();
            _workers?.PrepareCapacity(0);
            _built = true;
            return;
        }

        var nodes = new SystemNode[systemCount];
        for (int index = 0; index < systemCount; index++)
        {
            ISystem system = _systems[index];
            if (!ReferenceEquals(system.World, _world))
            {
                throw new InvalidOperationException("A system changed its world after registration.");
            }

            nodes[index] = new SystemNode(system, system.Access);
        }

        var levels = new int[systemCount];
        int maximumLevel = 0;
        for (int current = 0; current < systemCount; current++)
        {
            int level = 0;
            SystemAccess currentAccess = nodes[current].Access;
            for (int previous = 0; previous < current; previous++)
            {
                SystemAccess previousAccess = nodes[previous].Access;
                if (Conflicts(in previousAccess, in currentAccess))
                {
                    level = Math.Max(level, levels[previous] + 1);
                }
            }

            levels[current] = level;
            maximumLevel = Math.Max(maximumLevel, level);
        }

        var systemsByLevel = new List<ISystem>[maximumLevel + 1];
        for (int index = 0; index < systemCount; index++)
        {
            int level = levels[index];
            (systemsByLevel[level] ??= new List<ISystem>()).Add(nodes[index].System);
        }

        _batches = new ScheduleBatch[systemsByLevel.Length];
        for (int level = 0; level < systemsByLevel.Length; level++)
        {
            _batches[level] = new ScheduleBatch(systemsByLevel[level].ToArray());
        }

        _workers?.PrepareCapacity(systemCount);
        _built = true;
    }

    /// <summary>Executes every compiled batch once.</summary>
    public void Tick()
    {
        EnsureUsable();
        if (Interlocked.Exchange(ref _executing, 1) != 0)
        {
            throw new InvalidOperationException("A system scheduler tick is already active.");
        }

        try
        {
            if (!_built)
            {
                BuildCore();
            }

            for (int batchIndex = 0; batchIndex < _batches.Length; batchIndex++)
            {
                ISystem[] systems = _batches[batchIndex].Systems;
                if (systems.Length == 0)
                {
                    continue;
                }

                if (_workers is null)
                {
                    for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                    {
                        systems[systemIndex].Tick();
                    }
                }
                else if (systems.Length == 1)
                {
                    systems[0].Tick();
                }
                else
                {
                    _workers.Run(systems);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _executing, 0);
        }
    }

    /// <summary>Stops scheduler workers and releases their threads.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        EnsureNotExecuting();
        _disposed = true;
        _workers?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void EnsureUsable()
    {
        if (_disposed)
        {
            ThrowDisposed();
        }
    }

    private void EnsureNotExecuting()
    {
        if (Volatile.Read(ref _executing) != 0)
        {
            throw new InvalidOperationException("The scheduler cannot be changed while a tick is active.");
        }
    }

    private static void ThrowDisposed()
    {
#if NETSTANDARD2_1
        throw new ObjectDisposedException(nameof(SystemScheduler));
#else
        ObjectDisposedException.ThrowIf(true, nameof(SystemScheduler));
#endif
    }

    private static bool Conflicts(in SystemAccess left, in SystemAccess right)
    {
        if (left.RequiresExclusiveWorld || right.RequiresExclusiveWorld)
        {
            return true;
        }

        if ((left.ReadsTopology && right.WritesTopology)
            || (right.ReadsTopology && left.WritesTopology))
        {
            return true;
        }

        return Intersects(left.Writes, right.Reads)
            || Intersects(left.Writes, right.Writes)
            || Intersects(right.Writes, left.Reads)
            || Intersects(left.StampReads, right.Writes)
            || Intersects(right.StampReads, left.Writes);
    }

    private static bool Intersects(ReadOnlySpan<ComponentId> left, ReadOnlySpan<ComponentId> right)
    {
        for (int leftIndex = 0; leftIndex < left.Length; leftIndex++)
        {
            for (int rightIndex = 0; rightIndex < right.Length; rightIndex++)
            {
                if (left[leftIndex] == right[rightIndex])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly struct SystemNode
    {
        internal SystemNode(ISystem system, SystemAccess access)
        {
            System = system;
            Access = access;
        }

        internal ISystem System { get; }
        internal SystemAccess Access { get; }
    }

    private readonly struct ScheduleBatch
    {
        internal ScheduleBatch(ISystem[] systems) => Systems = systems;

        internal ISystem[] Systems { get; }
    }

    private sealed class SchedulerWorkers : IDisposable
    {
        private readonly object _gate = new();
        private readonly Thread[] _threads;
        private Queue<ISystem> _pending = new();
        private int _remaining;
        private bool _batchActive;
        private bool _stopping;
        private ExceptionDispatchInfo? _failure;

        internal SchedulerWorkers(int workerCount)
        {
            _threads = new Thread[workerCount];
            for (int index = 0; index < workerCount; index++)
            {
                _threads[index] = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"DeltaECS.Systems.Worker.{index}"
                };
                _threads[index].Start();
            }
        }

        internal void PrepareCapacity(int capacity)
        {
            lock (_gate)
            {
                if (_pending.Count != 0 || _batchActive)
                {
                    throw new InvalidOperationException("Cannot prepare scheduler workers during execution.");
                }

                _pending = new Queue<ISystem>(Math.Max(4, capacity));
            }
        }

        internal void Run(ISystem[] systems)
        {
            ExceptionDispatchInfo? failure;
            lock (_gate)
            {
                if (_stopping)
                {
#if NETSTANDARD2_1
                    throw new ObjectDisposedException(nameof(SchedulerWorkers));
#else
                    ObjectDisposedException.ThrowIf(true, nameof(SchedulerWorkers));
#endif
                }

                _failure = null;
                for (int index = 0; index < systems.Length; index++)
                {
                    _pending.Enqueue(systems[index]);
                }

                _remaining = systems.Length;
                _batchActive = true;
                Monitor.PulseAll(_gate);
                while (_batchActive)
                {
                    Monitor.Wait(_gate);
                }

                failure = _failure;
            }

            failure?.Throw();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stopping)
                {
                    return;
                }

                _stopping = true;
                Monitor.PulseAll(_gate);
            }

            for (int index = 0; index < _threads.Length; index++)
            {
                _threads[index].Join();
            }
        }

        private void WorkerLoop()
        {
            while (true)
            {
                ISystem? system = null;
                lock (_gate)
                {
                    while (!_stopping && (!_batchActive || _pending.Count == 0))
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_stopping)
                    {
                        return;
                    }

                    system = _pending.Dequeue();
                }

                try
                {
                    system.Tick();
                }
                catch (Exception exception)
                {
                    lock (_gate)
                    {
                        _failure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _remaining--;
                        if (_remaining == 0)
                        {
                            _batchActive = false;
                            Monitor.PulseAll(_gate);
                        }
                    }
                }
            }
        }
    }
}
