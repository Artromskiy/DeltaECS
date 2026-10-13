using System;
using System.Numerics;

using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.MyriadComponents;

using Myriad.ECS;
using Myriad.ECS.Command;
using Myriad.ECS.Queries;
using Myriad.ECS.Worlds;

namespace Ecs.CSharp.Benchmark;

public partial class SystemWithOneComponent
{

    [Context] private readonly MyriadContext _myriad;

    [BenchmarkCategory(Categories.Myriad, Categories.SingleThread)]
    [Benchmark]
    public int MyriadSingleThread()
    {
        World world = _myriad.World;
        _ = world.Execute<MyriadForEach1, Component1>();
        return EntityCount;
    }

    [BenchmarkCategory(Categories.Myriad, Categories.SingleThread)]
    [Benchmark]
    public int MyriadSingleThreadChunk()
    {
        World world = _myriad.World;
        _ = world.ExecuteChunk<MyriadForEach1, Component1>();
        return EntityCount;
    }
    private struct MyriadForEach1
        : IQuery<Component1>, IChunkQuery<Component1>
    {
        public readonly void Execute(Entity entity, ref Component1 t0) => ++t0.Value;

        public readonly void Execute(ChunkHandle chunk, Span<Component1> t0)
        {
            for (int i = 0; i < t0.Length; i++)
            {
                t0[i].Value++;
            }
        }
    }

    private readonly struct MyriadVectorForEach1
        : IVectorChunkQuery<int>
    {
        private static readonly Vector<int> _one = Vector<int>.One;

        public readonly void Execute(Span<Vector<int>> t0, int offset, int padding)
        {
            for (int i = 0; i < t0.Length; i++)
            {
                t0[i] += _one;
            }
        }
    }

    private sealed class MyriadContext : MyriadBaseContext
    {
        // Myriad stores components as arrays of structs, so all structs of the same type are
        // always sequential in memory no matter what else is attached to the entity. So no need to respect
        // the padding input
        public MyriadContext(int entityCount, int padding)
        {
            CommandBuffer cmd = new CommandBuffer(World);
            for (int i = 0; i < entityCount; i++)
            {
                _ = cmd.Create().Set(new Component1());
            }

            cmd.Playback().Dispose();
        }
    }

#if INCLUDE_SLOW_BENCHMARKS
    [BenchmarkCategory(Categories.Myriad, Categories.SingleThread)]
    [Benchmark]
    public int Myriad_Enumerable()
    {
        World world = _myriad.World;

        foreach ((Entity _, RefT<Component1> c) in world.Query<Component1>())
        {
            c.Ref.Value++;
        }
        return EntityCount;
    }
#endif

    [BenchmarkCategory(Categories.Myriad, Categories.SingleThread)]
    [Benchmark]
    public int MyriadDelegate()
    {
        World world = _myriad.World;

        _ = world.Query((ref Component1 c) =>
        {
            c.Value++;
        });
        return EntityCount;
    }

    [BenchmarkCategory(Categories.Myriad, Categories.SIMD)]
    [Benchmark]
    public int MyriadSingleThreadChunkSIMD()
    {
        World world = _myriad.World;

        _ = world.ExecuteVectorChunk<MyriadVectorForEach1, Component1, int>(new MyriadVectorForEach1());
        return EntityCount;
    }
}
