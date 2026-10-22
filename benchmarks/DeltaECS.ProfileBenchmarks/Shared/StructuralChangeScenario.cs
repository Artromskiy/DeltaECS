namespace DeltaECS.ProfileAdapter;

using Delta.ECS;

/// <summary>Repeated multi-component structural transitions with data and tag observers.</summary>
public sealed class StructuralChangeScenario : IDisposable
{
    private readonly World _world;
    private readonly int _amount;
    private int _invocations;
    private readonly ComponentId[] _values = new ComponentId[12];
    private readonly ComponentId _tag;
    private readonly Entity[] _selected;
    private readonly IOperation[] _observers;
    private readonly Query[] _queries = new Query[64];

    public StructuralChangeScenario(int amount, int subscribers, bool partial)
    {
        _amount = amount;
        _world = new World(initialEntityCapacity: amount);
        for (int index = 0; index < _values.Length; index++)
        {
            _values[index] = _world.Components.Register<StructuralChangeValue>(new SchemaId((ulong)(650_000 + index)));
        }

        _tag = _world.Components.Register<StructuralChangeTag>(new SchemaId(650_100));
        var entities = new Entity[amount];
        _world.Create(_values, entities);
        _world.Add(entities, _tag);
        _selected = partial ? entities.Where((_, index) => (index & 1) == 0).ToArray() : entities;
        for (int mask = 0; mask < _queries.Length; mask++)
        {
            var ids = new List<ComponentId> { _values[0] };
            for (int bit = 0; bit < 6; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    ids.Add(_values[bit + 1]);
                }
            }

            _queries[mask] = _world.WhereAll(ids.ToArray());
        }

        _observers = new IOperation[subscribers];
        for (int index = 0; index < subscribers; index++)
        {
            _observers[index] = index % 7 == 0
                ? _world.ForEachChange<StructuralChangeTag>(_tag, static (Entity _, ChangeKind kind, ref StructuralChangeTag value) => { })
                : _world.ForEachChange<StructuralChangeValue>(_values[index % _values.Length], static (Entity _, ChangeKind kind, ref StructuralChangeValue value) => { });
            _observers[index].Invoke();
        }
    }

    public int RoundTrip()
    {
        for (int index = 0; index < _selected.Length; index += 97)
        {
            _world.GetRef<StructuralChangeValue>(_selected[index], _values[0]).A++;
            _ = _world.GetRef<StructuralChangeTag>(_selected[index], _tag);
        }

        int changed = 0;
        for (int index = _values.Length - 1; index >= 8; index--)
        {
            changed += _world.Remove(_selected, _values[index]);
        }

        for (int index = 8; index < _values.Length; index++)
        {
            changed += _world.Add(_selected, _values[index]);
        }

        foreach (IOperation observer in _observers)
        {
            observer.Invoke();
        }

        _invocations++;
        GC.KeepAlive(_queries);
        return changed;
    }

    public void Validate()
    {
        if (_world.AliveEntityCount != _amount
            || _world.Get<StructuralChangeValue>(_selected[0], _values[0]).A != _invocations)
        {
            throw new InvalidOperationException("Structural transition lost entity identity or component values.");
        }

        foreach (ComponentId id in _values)
        {
            if (!_world.Has(_selected[0], id))
            {
                throw new InvalidOperationException("Structural round trip did not restore the component set.");
            }
        }

        if (!_world.Has(_selected[0], _tag))
        {
            throw new InvalidOperationException("Structural transition lost tag membership.");
        }
    }

    public void Dispose() => _world.Dispose();
}

internal struct StructuralChangeValue
{
    internal long A;
    internal long B;
    internal long C;
}

internal struct StructuralChangeTag;
