using System.Globalization;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Delta.ECS;

namespace ApiChoiceProbe;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "--entities")
        {
            throw new ArgumentException("Pass --entities <count> before BenchmarkDotNet arguments.");
        }

        Environment.SetEnvironmentVariable("DELTAECS_EVAL_ENTITY_COUNT", int.Parse(args[1], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args[2..]);
    }
}

[MemoryDiagnoser]
public class DispatchBenchmarks : IDisposable
{
    private readonly World _world = new();
    private readonly Dictionary<ComponentId, Handler> _handlers = new();
    private HashSet<ComponentId> _configured = new();
    private Visitor _visitor = null!; // GlobalSetup assigns this before any benchmark invocation.
    private Entity[] _entities = [];
    private ComponentId[] _ids = [];

    [GlobalSetup]
    public void Setup()
    {
        int count = int.Parse(Environment.GetEnvironmentVariable("DELTAECS_EVAL_ENTITY_COUNT") ?? throw new InvalidOperationException("Missing workload"), CultureInfo.InvariantCulture);
        ComponentId armorId = _world.Components.Register<Armor>(new SchemaId(1));
        ComponentId energyId = _world.Components.Register<Energy>(new SchemaId(2));
        _world.Components.BindInterface<Armor, IResettable>();
        _world.Components.BindInterface<Energy, IResettable>();
        _configured = new HashSet<ComponentId> { armorId, energyId };
        _visitor = new Visitor(_world);
        var builder = new Builder(_handlers);
        _world.Components.Visit(armorId, builder);
        _world.Components.Visit(energyId, builder);
        _entities = new Entity[count];
        _ids = new ComponentId[count];
        for (int index = 0; index < count; index++)
        {
            ComponentId id = index % 2 == 0 ? armorId : energyId;
            _ids[index] = id;
            _entities[index] = _world.Create(id);
        }

        if (VisitorPerCall() != count || CachedHandler() != count)
        {
            throw new InvalidOperationException("Dispatch checks disagree");
        }
    }

    [Benchmark(Baseline = true)]
    public int VisitorPerCall()
    {
        int success = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            Entity entity = _entities[index];
            ComponentId id = _ids[index];
            if (!_configured.Contains(id) || !_world.IsAlive(entity) || !_world.Has(entity, id))
            {
                continue;
            }

            _visitor.Target = entity;
            if (_world.Components.TryVisit(id, _visitor))
            {
                success++;
            }
        }

        return success;
    }

    [Benchmark]
    public int CachedHandler()
    {
        int success = 0;
        for (int index = 0; index < _entities.Length; index++)
        {
            Entity entity = _entities[index];
            ComponentId id = _ids[index];
            if (_handlers.TryGetValue(id, out Handler? handler) && _world.IsAlive(entity) && _world.Has(entity, id))
            {
                handler.Invoke(_world, entity, id);
                success++;
            }
        }

        return success;
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    public interface IResettable { void Reset(); }
    public struct Armor : IResettable { public int Value; public void Reset() => Value = 0; }
    public struct Energy : IResettable { public int Value; public void Reset() => Value = 0; }

    private sealed class Visitor(World world) : IComponentVisitor<IResettable>
    {
        public Entity Target;
        public void Visit<T>(ComponentId id) where T : IResettable => world.GetRef<T>(Target, id).Reset();
    }

    private sealed class Builder(Dictionary<ComponentId, Handler> handlers) : IComponentVisitor<IResettable>
    {
        public void Visit<T>(ComponentId id) where T : IResettable => handlers.Add(id, new Handler<T>());
    }

    private abstract class Handler
    {
        internal abstract void Invoke(World world, Entity entity, ComponentId id);
    }

    private sealed class Handler<T> : Handler where T : IResettable
    {
        internal override void Invoke(World world, Entity entity, ComponentId id) => world.GetRef<T>(entity, id).Reset();
    }
}
