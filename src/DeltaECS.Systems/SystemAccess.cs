namespace Delta.ECS.Systems;

using System;
using Delta.ECS;

/// <summary>
/// Immutable, world-bound access metadata used to compile a system schedule.
/// Component ids are copied when the value is created and exposed as read-only
/// spans thereafter.
/// </summary>
public readonly struct SystemAccess
{
    private static readonly ComponentId[] Empty = Array.Empty<ComponentId>();
    private readonly ComponentId[]? _reads;
    private readonly ComponentId[]? _writes;
    private readonly ComponentId[]? _stampReads;
    private readonly ComponentId[]? _adds;
    private readonly ComponentId[]? _removes;

    /// <summary>Creates access metadata from world-local component ids.</summary>
    /// <param name="reads">Components read by the system.</param>
    /// <param name="writes">Components written by the system.</param>
    /// <param name="stampReads">Component stamps observed by the system.</param>
    /// <param name="adds">Components added by structural operations.</param>
    /// <param name="removes">Components removed by structural operations.</param>
    /// <param name="readsTopology">Whether the system observes entity topology.</param>
    /// <param name="writesTopology">Whether the system changes entity topology.</param>
    /// <param name="createsEntities">Whether the system creates entities.</param>
    /// <param name="destroysEntities">Whether the system destroys entities.</param>
    /// <param name="unknownWorldAccess">Whether an access could not be analyzed.</param>
    /// <param name="usesParallelExecutor">Whether the system owns a parallel executor.</param>
    public SystemAccess(
        ComponentId[]? reads = null,
        ComponentId[]? writes = null,
        ComponentId[]? stampReads = null,
        ComponentId[]? adds = null,
        ComponentId[]? removes = null,
        bool readsTopology = false,
        bool writesTopology = false,
        bool createsEntities = false,
        bool destroysEntities = false,
        bool unknownWorldAccess = false,
        bool usesParallelExecutor = false)
    {
        _reads = Copy(reads, nameof(reads));
        _writes = Copy(writes, nameof(writes));
        _stampReads = Copy(stampReads, nameof(stampReads));
        _adds = Copy(adds, nameof(adds));
        _removes = Copy(removes, nameof(removes));
        ReadsTopology = readsTopology;
        WritesTopology = writesTopology;
        CreatesEntities = createsEntities;
        DestroysEntities = destroysEntities;
        UnknownWorldAccess = unknownWorldAccess;
        UsesParallelExecutor = usesParallelExecutor;
    }

    /// <summary>Gets components whose data may be read.</summary>
    public ReadOnlySpan<ComponentId> Reads => _reads ?? Empty;

    /// <summary>Gets components whose data may be written.</summary>
    public ReadOnlySpan<ComponentId> Writes => _writes ?? Empty;

    /// <summary>Gets components whose change stamps may be observed.</summary>
    public ReadOnlySpan<ComponentId> StampReads => _stampReads ?? Empty;

    /// <summary>Gets components added by structural operations.</summary>
    public ReadOnlySpan<ComponentId> Adds => _adds ?? Empty;

    /// <summary>Gets components removed by structural operations.</summary>
    public ReadOnlySpan<ComponentId> Removes => _removes ?? Empty;

    /// <summary>Gets whether the system observes entity topology.</summary>
    public bool ReadsTopology { get; }

    /// <summary>Gets whether the system changes entity topology.</summary>
    public bool WritesTopology { get; }

    /// <summary>Gets whether the system creates entities.</summary>
    public bool CreatesEntities { get; }

    /// <summary>Gets whether the system destroys entities.</summary>
    public bool DestroysEntities { get; }

    /// <summary>Gets whether the analyzer could not determine all world access.</summary>
    public bool UnknownWorldAccess { get; }

    /// <summary>Gets whether the system invokes its own parallel world executor.</summary>
    public bool UsesParallelExecutor { get; }

    /// <summary>Gets whether this access must run as an exclusive world phase.</summary>
    public bool RequiresExclusiveWorld
        => UnknownWorldAccess
            || UsesParallelExecutor
            || WritesTopology
            || CreatesEntities
            || DestroysEntities
            || !Adds.IsEmpty
            || !Removes.IsEmpty;

    /// <summary>Gets whether this access contains a structural operation.</summary>
    public bool HasStructuralEffects
        => CreatesEntities || DestroysEntities || !Adds.IsEmpty || !Removes.IsEmpty;

    /// <summary>Gets metadata with no component or world access.</summary>
    public static SystemAccess None => default;

    private static ComponentId[]? Copy(ComponentId[]? values, string parameterName)
    {
        if (values is null || values.Length == 0)
        {
            return null;
        }

        var copy = new ComponentId[values.Length];
        values.AsSpan().CopyTo(copy);
        for (int index = 0; index < copy.Length; index++)
        {
            if (!copy[index].IsValid)
            {
                ThrowHelper.ThrowInvalidComponentIds(parameterName);
            }
        }

        return copy;
    }
}
