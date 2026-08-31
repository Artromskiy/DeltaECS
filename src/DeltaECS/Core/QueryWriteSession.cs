namespace Delta.ECS;

/// <summary>Shared lifetime and write-intent state for one query execution.</summary>
internal sealed class QueryWriteSession
{
    private int _generation;
    private bool _active;
    private bool _writeEnabled;

    internal QueryWriteSession? Next { get; set; }

    internal int Reset(bool writeEnabled)
    {
        _generation = _generation == int.MaxValue ? 1 : _generation + 1;
        _active = true;
        _writeEnabled = writeEnabled;
        Next = null;
        return _generation;
    }

    internal void EnsureActive(int generation)
    {
        if (!_active || generation != _generation)
        {
            ThrowHelper.ThrowDisposedQueryExecution();
        }
    }

    internal void Acquire(int generation)
    {
        EnsureActive(generation);
        if (!_writeEnabled)
        {
            ThrowHelper.ThrowMissingWriteIntent();
        }
    }

    internal bool TryRelease(int generation)
    {
        if (!_active || generation != _generation)
        {
            return false;
        }

        _active = false;
        _writeEnabled = false;
        return true;
    }
}
