using Delta.ECS;

namespace DeltaECS.Profiling;

/// <summary>Profiles the real four-component generated delegate execution path.</summary>
internal static class Movement4DelegateProfile
{
    internal const int EntityCount = 100;

    private static long s_checksum;

    /// <summary>Runs the complete probe, including setup and query execution.</summary>
    /// <remarks>
    /// This method intentionally has no profiler scopes. Metalama generates the
    /// profiling try/finally boundary at compile time.
    /// </remarks>
    [ProfileMethod(0)]
    internal static long Run()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId aId = layouts.Register<Movement4A>(new SchemaId(40_001));
        ComponentId bId = layouts.Register<Movement4B>(new SchemaId(40_002));
        ComponentId cId = layouts.Register<Movement4C>(new SchemaId(40_003));
        ComponentId dId = layouts.Register<Movement4D>(new SchemaId(40_004));
        ComponentId[] components = [aId, bId, cId, dId];
        var entities = new Entity[EntityCount];

        var world = new World(layouts, EntityCount, chunkCapacity: 1024);
        world.Create(components, entities);
        for (int index = 0; index < entities.Length; index++)
        {
            world.Set(entities[index], aId, new Movement4A { Value = 1 });
            world.Set(entities[index], bId, new Movement4B { Value = 2 });
            world.Set(entities[index], cId, new Movement4C { Value = 3 });
            world.Set(entities[index], dId, new Movement4D { Value = 4 });
        }

        QuerySpec spec = QuerySpec.WhereAll(components);
        Query query = world.CreateQuery(in spec);
        s_checksum = 0;

        world.ForEach(
            in query,
            static (ref Movement4A a, ref Movement4B b, ref Movement4C c, in Movement4D d) =>
            {
                a.Value = d.Value + 1;
                b.Value = d.Value + 2;
                c.Value = (a.Value + b.Value) / 2;
                s_checksum += a.Value + b.Value + c.Value + d.Value;
            });

        world.Dispose();
        return s_checksum;
    }

    internal struct Movement4A
    {
        internal int Value;
    }

    internal struct Movement4B
    {
        internal int Value;
    }

    internal struct Movement4C
    {
        internal int Value;
    }

    internal struct Movement4D
    {
        internal int Value;
    }
}
