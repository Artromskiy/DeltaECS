namespace Delta.ECS;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

internal interface IGeneratedDenseBinding
{
    void Clear();
}

/// <summary>Compiler-support reference to a bound chunk with a live entity count.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct GeneratedBoundChunk
{
    private readonly Chunk _chunk;

    internal GeneratedBoundChunk(Chunk chunk) => _chunk = chunk;

    /// <summary>Reads the current population, including changes that do not alter chunk topology.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.Count;
    }

    /// <summary>Returns the live entity row inside a validated execution lease.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref Entity GetEntityReference() => ref _chunk.RawEntities.GetRefAtZero();
}

/// <summary>Query-owned compiler-support cache for a generated, typed row signature.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GeneratedDenseBinding<TRows> : IGeneratedDenseBinding
    where TRows : struct
{
    private TRows[] _rows = Array.Empty<TRows>();
    private int _count;
    private int _version = -1;
    private int[] _writes = Array.Empty<int>();

    /// <summary>Resolves the complete component signature once for the owning query.</summary>
    protected abstract void Prepare(in Query query);
    /// <summary>Projects validated rows when the query topology changes.</summary>
    protected abstract TRows BindRows(Array[] rows, GeneratedBoundChunk chunk);

    /// <summary>Stores unique write routes for marking before any callback executes.</summary>
    protected void SetWriteRoutes(ReadOnlySpan<int> routes)
    {
        if (routes.IsEmpty)
        {
            return;
        }

        var unique = new int[routes.Length];
        int count = 0;
        for (int index = 0; index < routes.Length; index++)
        {
            int route = routes[index];
            if (unique.AsSpan(0, count).IndexOf(route) < 0)
            {
                unique[count++] = route;
            }
        }
        Array.Resize(ref unique, count);
        _writes = unique;
    }

    internal void Initialize(in Query query) => Prepare(in query);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<TRows> GetRows(QueryPlan plan)
    {
        if (_version != plan.MatchingVersion)
        {
            Refresh(plan);
        }
        return _rows.AsSpan(0, _count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Refresh(QueryPlan plan)
    {
        ReadOnlySpan<ChunkPlan> chunks = plan.MatchingChunkPlans();
        if (_rows.Length < chunks.Length)
        {
            Array.Resize(ref _rows, Math.Max(chunks.Length, _rows.Length * 2));
        }
        for (int index = 0; index < chunks.Length; index++)
        {
            ref readonly ChunkPlan chunk = ref chunks[index];
            _rows[index] = BindRows(chunk.ComponentRows, new GeneratedBoundChunk(chunk.Chunk));
        }
        if (_count > chunks.Length)
        {
            _rows.AsSpan(chunks.Length, _count - chunks.Length).Clear();
        }
        _count = chunks.Length;
        _version = plan.MatchingVersion;
    }

    internal void MarkWrites(QueryPlan plan)
    {
        if (_writes.Length == 0)
        {
            return;
        }
        foreach (ref readonly ArchetypePlan archetype in plan.MatchingPlans())
        {
            if (archetype.ChunkCount == 0)
            {
                continue;
            }
            foreach (int route in _writes)
            {
                GeneratedForEachRuntime.IncrementArchetypeStamp(archetype.ArchetypeStamps, route);
            }
        }
    }

    void IGeneratedDenseBinding.Clear()
    {
        // Clear the existing array as well: an escaped compiler-support binding must not retain rows.
        _rows.AsSpan().Clear();
        _rows = Array.Empty<TRows>();
        _count = 0;
        _version = -1;
    }
}

/// <summary>Borrowed typed rows, valid only while this compiler-support execution is alive.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct GeneratedBoundExecution<TRows> where TRows : struct
{
    private World? _owner;
    /// <summary>Typed chunk descriptors borrowed until disposal.</summary>
    public ReadOnlySpan<TRows> Rows { get; }

    internal GeneratedBoundExecution(World owner, ReadOnlySpan<TRows> rows)
    {
        _owner = owner;
        Rows = rows;
    }

    /// <summary>Releases the structural lease.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        World? owner = _owner;
        _owner = null;
        owner?.EndQueryLease();
    }
}

public static partial class GeneratedForEachRuntime
{
    /// <summary>Opens a query-owned typed binding; routes are resolved once per signature.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeneratedBoundExecution<TRows> OpenBoundDense<TBinding, TRows>(World world, in Query query)
        where TBinding : GeneratedDenseBinding<TRows>, new()
        where TRows : struct
    {
        QueryPlan plan = ValidateQuery(world, in query);
        TBinding binding = plan.GetDenseBinding<TBinding, TRows>(in query);
        ReadOnlySpan<TRows> rows = binding.GetRows(plan);
        World owner = plan.Owner;
        owner.BeginQueryLease();
        binding.MarkWrites(plan);
        return new GeneratedBoundExecution<TRows>(owner, rows);
    }

    /// <summary>Binds a validated typed array once while building a generated signature.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] GetGeneratedArray<T>(Array[] rows, int route) => Unsafe.As<T[]>(rows.RefAt(route));

    /// <summary>Returns the first element of an already-bound array.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetGeneratedArrayReference<T>(T[] row) => ref row.GetRefAtZero();
}
