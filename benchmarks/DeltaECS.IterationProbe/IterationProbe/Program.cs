using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Delta.ECS;

namespace Delta.ECS.IterationProbe;

internal static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<IterationBenchmarks>(args: args);
    }
}

[MemoryDiagnoser]
public class IterationBenchmarks : IDisposable
{
    private const int EntityCount = 1_048_576;

    private World _world = null!;
    private Query _query1;
    private Query _query2;
    private Query _query3;
    private Query _query4;
    private Query _query8;
    private Query _query16;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId[] ids = new ComponentId[16];
        for (int index = 0; index < ids.Length; index++)
        {
            ids[index] = layouts.Register(ComponentTypes[index], new SchemaId((ulong)(81_001 + index)));
        }

        _world = new World(layouts, initialEntityCapacity: EntityCount);
        var entities = new Entity[EntityCount];
        _world.Create(ids, EntityCount, entities);

        for (int index = 0; index < entities.Length; index++)
        {
            Entity entity = entities[index];
            for (int component = 0; component < ids.Length; component++)
            {
                SetValue(entity, ids[component], component, index + component);
            }
        }

        QuerySpec query1 = QuerySpec.WhereAll(ids[0]);
        QuerySpec query2 = QuerySpec.WhereAll(ids[0], ids[1]);
        QuerySpec query3 = QuerySpec.WhereAll(ids[0], ids[1], ids[2]);
        QuerySpec query4 = QuerySpec.WhereAll(ids[0], ids[1], ids[2], ids[3]);
        QuerySpec query8 = QuerySpec.WhereAll(
            ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7]);
        QuerySpec query16 = QuerySpec.WhereAll(
            ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7],
            ids[8], ids[9], ids[10], ids[11], ids[12], ids[13], ids[14], ids[15]);
        _query1 = _world.CreateQuery(in query1);
        _query2 = _world.CreateQuery(in query2);
        _query3 = _world.CreateQuery(in query3);
        _query4 = _world.CreateQuery(in query4);
        _query8 = _world.CreateQuery(in query8);
        _query16 = _world.CreateQuery(in query16);
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components1()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query1,
            ref state,
            static (ref IterationState state, in C00 c0) =>
            {
                state.Visited++;
                state.Checksum += c0.Value;
            });
        return state.Result;
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components2()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query2,
            ref state,
            static (ref IterationState state, in C00 c0, in C01 c1) =>
            {
                state.Visited++;
                state.Checksum += c0.Value + c1.Value;
            });
        return state.Result;
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components3()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query3,
            ref state,
            static (ref IterationState state, in C00 c0, in C01 c1, in C02 c2) =>
            {
                state.Visited++;
                state.Checksum += c0.Value + c1.Value + c2.Value;
            });
        return state.Result;
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components4()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query4,
            ref state,
            static (ref IterationState state, in C00 c0, in C01 c1, in C02 c2, in C03 c3) =>
            {
                state.Visited++;
                state.Checksum += c0.Value + c1.Value + c2.Value + c3.Value;
            });
        return state.Result;
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components8()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query8,
            ref state,
            static (
                ref IterationState state,
                in C00 c0,
                in C01 c1,
                in C02 c2,
                in C03 c3,
                in C04 c4,
                in C05 c5,
                in C06 c6,
                in C07 c7) =>
            {
                state.Visited++;
                state.Checksum += c0.Value + c1.Value + c2.Value + c3.Value + c4.Value + c5.Value + c6.Value + c7.Value;
            });
        return state.Result;
    }

    [Benchmark]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public long Components16()
    {
        var state = new IterationState();
        _world.ForEach(
            in _query16,
            ref state,
            static (
                ref IterationState state,
                in C00 c0,
                in C01 c1,
                in C02 c2,
                in C03 c3,
                in C04 c4,
                in C05 c5,
                in C06 c6,
                in C07 c7,
                in C08 c8,
                in C09 c9,
                in C10 c10,
                in C11 c11,
                in C12 c12,
                in C13 c13,
                in C14 c14,
                in C15 c15) =>
            {
                state.Visited++;
                state.Checksum += c0.Value + c1.Value + c2.Value + c3.Value
                                  + c4.Value + c5.Value + c6.Value + c7.Value
                                  + c8.Value + c9.Value + c10.Value + c11.Value
                                  + c12.Value + c13.Value + c14.Value + c15.Value;
            });
        return state.Result;
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private void SetValue(Entity entity, ComponentId id, int component, int value)
    {
        switch (component)
        {
            case 0: _world.Set(entity, id, new C00 { Value = value }); break;
            case 1: _world.Set(entity, id, new C01 { Value = value }); break;
            case 2: _world.Set(entity, id, new C02 { Value = value }); break;
            case 3: _world.Set(entity, id, new C03 { Value = value }); break;
            case 4: _world.Set(entity, id, new C04 { Value = value }); break;
            case 5: _world.Set(entity, id, new C05 { Value = value }); break;
            case 6: _world.Set(entity, id, new C06 { Value = value }); break;
            case 7: _world.Set(entity, id, new C07 { Value = value }); break;
            case 8: _world.Set(entity, id, new C08 { Value = value }); break;
            case 9: _world.Set(entity, id, new C09 { Value = value }); break;
            case 10: _world.Set(entity, id, new C10 { Value = value }); break;
            case 11: _world.Set(entity, id, new C11 { Value = value }); break;
            case 12: _world.Set(entity, id, new C12 { Value = value }); break;
            case 13: _world.Set(entity, id, new C13 { Value = value }); break;
            case 14: _world.Set(entity, id, new C14 { Value = value }); break;
            default: _world.Set(entity, id, new C15 { Value = value }); break;
        }
    }

    private static readonly Type[] ComponentTypes =
    [
        typeof(C00), typeof(C01), typeof(C02), typeof(C03),
        typeof(C04), typeof(C05), typeof(C06), typeof(C07),
        typeof(C08), typeof(C09), typeof(C10), typeof(C11),
        typeof(C12), typeof(C13), typeof(C14), typeof(C15),
    ];

    internal struct IterationState
    {
        public int Visited;
        public long Checksum;

        public readonly long Result => Checksum ^ ((long)Visited << 32);
    }
}

internal struct C00 { public int Value; }
internal struct C01 { public int Value; }
internal struct C02 { public int Value; }
internal struct C03 { public int Value; }
internal struct C04 { public int Value; }
internal struct C05 { public int Value; }
internal struct C06 { public int Value; }
internal struct C07 { public int Value; }
internal struct C08 { public int Value; }
internal struct C09 { public int Value; }
internal struct C10 { public int Value; }
internal struct C11 { public int Value; }
internal struct C12 { public int Value; }
internal struct C13 { public int Value; }
internal struct C14 { public int Value; }
internal struct C15 { public int Value; }
