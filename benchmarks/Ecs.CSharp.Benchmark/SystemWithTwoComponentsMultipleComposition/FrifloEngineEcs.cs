using System;
using System.Runtime.Intrinsics;

using BenchmarkDotNet.Attributes;

using Ecs.CSharp.Benchmark.Contexts;
using Ecs.CSharp.Benchmark.Contexts.FrifloEngineComponents;

using Friflo.Engine.ECS;

namespace Ecs.CSharp.Benchmark;

public partial class SystemWithTwoComponentsMultipleComposition
{

    [Context]
    private readonly FrifloEngineEcsContext _frifloEngineEcs;

    [BenchmarkCategory(Categories.FrifloEngineEcs, Categories.SingleThread)]
    [Benchmark]
    public int FrifloEngineEcsMonoThread()
    {
        foreach ((Chunk<Component1> component1, Chunk<Component2> component2, ChunkEntities _)
                 in _frifloEngineEcs.queryTwo.Chunks)
        {
            Span<Component1> component1Span = component1.Span;
            Span<Component2> component2Span = component2.Span;
            for (int n = 0; n < component1Span.Length; n++)
            {
                Update(ref component1Span[n], ref component2Span[n]);
            }
        }
        return EntityCount;
    }

    [BenchmarkCategory(Categories.FrifloEngineEcs, Categories.MultiThread)]
    [Benchmark]
    public int FrifloEngineEcsMultiThread()
    {
        _frifloEngineEcs.jobTwoWithComposition.RunParallel();
        return EntityCount;
    }

    [BenchmarkCategory(Categories.FrifloEngineEcs, Categories.SIMD)]
    [Benchmark]
    public int FrifloEngineEcsSIMDMonoThread()
    {
        foreach ((Chunk<Component1> component1, Chunk<Component2> component2, ChunkEntities _)
                 in _frifloEngineEcs.queryTwo.Chunks)
        {
            Span<int> component1Span = component1.AsSpan256<int>();     // Length - multiple of 8
            Span<int> component2Span = component2.AsSpan256<int>();     // Length - multiple of 8
            int step = component1.StepSpan256;                          // step = 8
            for (int n = 0; n < component1Span.Length; n += step)
            {
                Span<int> component1Slice = component1Span.Slice(n, step);
                Vector256<int> value1 = Vector256.Create<int>(component1Slice);
                Vector256<int> value2 = Vector256.Create<int>(component2Span.Slice(n, step));
                Vector256<int> result = Vector256.Add(value1, value2);  // execute 8 add instructions at once
                result.CopyTo(component1Slice);
            }
        }
        return EntityCount;
    }

    private static void Update(ref Component1 c1, ref Component2 c2) => c1.Value += c2.Value;
    internal sealed class FrifloEngineEcsContext : FrifloEngineEcsBaseContext
    {

        public FrifloEngineEcsContext(int entityCount)
        {
            for (int i = 0; i < entityCount; ++i)
            {
                Entity entity = EntityStore.CreateEntity();
                _ = entity.AddComponent<Component1>();
                _ = entity.AddComponent(new Component2 { Value = 1 });

                switch (i % 4)
                {
                    case 0:
                        _ = entity.AddComponent<Padding1>();
                        break;

                    case 1:
                        _ = entity.AddComponent<Padding2>();
                        break;

                    case 2:
                        _ = entity.AddComponent<Padding3>();
                        break;

                    case 3:
                        _ = entity.AddComponent<Padding4>();
                        break;
                    default:
                        break;
                }
            }
        }

        internal static void ForEach(Chunk<Component1> component1, Chunk<Component2> component2, ChunkEntities entities)
        {
            Span<Component1> component1Span = component1.Span;
            Span<Component2> component2Span = component2.Span;
            for (int n = 0; n < component1Span.Length; n++)
            {
                Update(ref component1Span[n], ref component2Span[n]);
            }
        }
        private record struct Padding1 : IComponent { }

        private record struct Padding2 : IComponent { }

        private record struct Padding3 : IComponent { }

        private record struct Padding4 : IComponent { }
    }
}
