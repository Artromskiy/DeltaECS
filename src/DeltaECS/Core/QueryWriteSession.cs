namespace Delta.ECS;

/// <summary>Shared lazy mutation stamp for one query execution.</summary>
internal sealed class QueryWriteSession
{
    private World? _owner;
    private int _generation;
    private bool _active;
    private bool _writeEnabled;
    private Stamp _writeStamp;

    internal QueryWriteSession? Next { get; set; }

    internal int Reset(World owner, bool writeEnabled)
    {
        _owner = owner;
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _active = true;
        _writeEnabled = writeEnabled;
        _writeStamp = default;
        Next = null;
        return _generation;
    }

    internal int Reset(bool writeEnabled, Stamp writeStamp)
    {
        _owner = null;
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _active = true;
        _writeEnabled = writeEnabled;
        _writeStamp = writeStamp;
        Next = null;
        return _generation;
    }

    internal void EnsureActive(int generation)
    {
        if (!_active || generation != _generation)
        {
            QueryThrowHelper.ThrowDisposedQueryExecution();
        }
    }

    internal void Acquire(int generation, out Stamp writeStamp)
    {
        EnsureActive(generation);
        if (!_writeEnabled)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        if (_writeStamp == default)
        {
            World? owner = _owner;
            if (owner is null)
            {
                QueryThrowHelper.ThrowMissingWriteIntent();
            }

            owner.ReserveQueryWrite(out _writeStamp);
        }

        writeStamp = _writeStamp;
    }

    internal bool TryRelease(int generation)
    {
        if (!_active || generation != _generation)
        {
            return false;
        }

        _active = false;
        _owner = null;
        _writeEnabled = false;
        _writeStamp = default;
        return true;
    }
}
