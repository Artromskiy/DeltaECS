namespace Delta.ECS;

/// <summary>Shared lazy write version for one query execution.</summary>
internal sealed class QueryWriteSession
{
    private World? _owner;
    private int _generation;
    private bool _active;
    private bool _writeEnabled;
    private uint _writeTick;
    private Stamp _writeStamp;

    internal QueryWriteSession? Next { get; set; }

    internal int Reset(World owner, bool writeEnabled)
    {
        _owner = owner;
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _active = true;
        _writeEnabled = writeEnabled;
        _writeTick = 0;
        _writeStamp = default;
        Next = null;
        return _generation;
    }

    internal int Reset(uint writeTick, Stamp writeStamp)
    {
        _owner = null;
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _active = true;
        _writeEnabled = writeTick != 0;
        _writeTick = writeTick;
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

    internal void Acquire(int generation, out uint writeTick, out Stamp writeStamp)
    {
        EnsureActive(generation);
        if (!_writeEnabled)
        {
            QueryThrowHelper.ThrowMissingWriteIntent();
        }

        if (_writeTick == 0)
        {
            World? owner = _owner;
            if (owner is null)
            {
                QueryThrowHelper.ThrowMissingWriteIntent();
            }

            _writeTick = owner.ReserveQueryWrite(out _writeStamp);
        }

        writeTick = _writeTick;
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
        _writeTick = 0;
        _writeStamp = default;
        return true;
    }
}
