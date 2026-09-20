using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Delta.ECS;

namespace Delta.ECS.IterationProbe
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var b = new IterationBenchmarks();
            b.Setup();
            var r = b.OneComponent();
            Console.WriteLine(r);

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
        private Query _query4;
        private Query _query6;
        private Query _query8;

        [GlobalSetup]
        public void Setup()
        {
            var layouts = new ComponentLayoutRegistry();
            ComponentId firstId = layouts.Register<First>(new SchemaId(81_001));
            ComponentId secondId = layouts.Register<Second>(new SchemaId(81_002));
            ComponentId thirdId = layouts.Register<Third>(new SchemaId(81_003));
            ComponentId fourthId = layouts.Register<Fourth>(new SchemaId(81_004));
            ComponentId fifthId = layouts.Register<Fifth>(new SchemaId(81_005));
            ComponentId sixthId = layouts.Register<Sixth>(new SchemaId(81_006));
            ComponentId seventhId = layouts.Register<Seventh>(new SchemaId(81_007));
            ComponentId eighthId = layouts.Register<Eighth>(new SchemaId(81_008));

            _world = new World(layouts, initialEntityCapacity: EntityCount);
            var entities = new Entity[EntityCount];
            _world.Create(
                stackalloc[] { firstId, secondId, thirdId, fourthId, fifthId, sixthId, seventhId, eighthId },
                EntityCount,
                entities);

            for (int index = 0; index < entities.Length; index++)
            {
                _world.Set(entities[index], firstId, new First { Value = index });
                _world.Set(entities[index], secondId, new Second { Value = index + 1 });
                _world.Set(entities[index], thirdId, new Third { Value = index + 2 });
                _world.Set(entities[index], fourthId, new Fourth { Value = index + 3 });
                _world.Set(entities[index], fifthId, new Fifth { Value = index + 4 });
                _world.Set(entities[index], sixthId, new Sixth { Value = index + 5 });
                _world.Set(entities[index], seventhId, new Seventh { Value = index + 6 });
                _world.Set(entities[index], eighthId, new Eighth { Value = index + 7 });
            }

            QuerySpec query2Spec = QuerySpec.WhereAll(firstId, secondId);
            QuerySpec query1Spec = QuerySpec.WhereAll(firstId);
            QuerySpec query4Spec = QuerySpec.WhereAll(firstId, secondId, thirdId, fourthId);
            QuerySpec query6Spec = QuerySpec.WhereAll(firstId, secondId, thirdId, fourthId, fifthId, sixthId);
            QuerySpec query8Spec = QuerySpec.WhereAll(
                firstId,
                secondId,
                thirdId,
                fourthId,
                fifthId,
                sixthId,
                seventhId,
                eighthId);
            _query1 = _world.CreateQuery(in query1Spec);
            _query2 = _world.CreateQuery(in query2Spec);
            _query4 = _world.CreateQuery(in query4Spec);
            _query6 = _world.CreateQuery(in query6Spec);
            _query8 = _world.CreateQuery(in query8Spec);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public long OneComponent()
        {
            var state = new IterationState();
            _world.ForEach(
                in _query1,
                ref state,
                static (ref IterationState state, in First first) =>
                {
                    state.Visited++;
                    state.Checksum += first.Value;
                });
            return state.Result;
        }

        [Benchmark(Baseline = true)]
        public long TwoComponents()
        {
            var state = new IterationState();
            _world.ForEach(
                in _query2,
                ref state,
                static (ref IterationState state, in First first, in Second second) =>
                {
                    state.Visited++;
                    state.Checksum += first.Value + second.Value;
                });
            return state.Result;
        }

        [Benchmark]
        public long FourComponents()
        {
            var state = new IterationState();
            _world.ForEach(
                in _query4,
                ref state,
                static (ref IterationState state, in First first, in Second second, in Third third, in Fourth fourth) =>
                {
                    state.Visited++;
                    state.Checksum += first.Value + second.Value + third.Value + fourth.Value;
                });
            return state.Result;
        }

        [Benchmark]
        public long SixComponents()
        {
            var state = new IterationState();
            _world.ForEach(
                in _query6,
                ref state,
                static (
                    ref IterationState state,
                    in First first,
                    in Second second,
                    in Third third,
                    in Fourth fourth,
                    in Fifth fifth,
                    in Sixth sixth) =>
                {
                    state.Visited++;
                    state.Checksum += first.Value + second.Value + third.Value + fourth.Value + fifth.Value + sixth.Value;
                });
            return state.Result;
        }

        [Benchmark]
        public long EightComponents()
        {
            var state = new IterationState();
            _world.ForEach(
                in _query8,
                ref state,
                static (
                    ref IterationState state,
                    in First first,
                    in Second second,
                    in Third third,
                    in Fourth fourth,
                    in Fifth fifth,
                    in Sixth sixth,
                    in Seventh seventh,
                    in Eighth eighth) =>
                {
                    state.Visited++;
                    state.Checksum += first.Value
                                      + second.Value
                                      + third.Value
                                      + fourth.Value
                                      + fifth.Value
                                      + sixth.Value
                                      + seventh.Value
                                      + eighth.Value;
                });
            return state.Result;
        }

        public void Dispose()
        {
            _world.Dispose();
            GC.SuppressFinalize(this);
        }

        internal struct IterationState
        {
            public int Visited;
            public long Checksum;

            public readonly long Result => Checksum ^ ((long)Visited << 32);
        }
    }

    struct First { public int Value; }
    struct Second { public int Value; }
    struct Third { public int Value; }
    struct Fourth { public int Value; }
    struct Fifth { public int Value; }
    struct Sixth { public int Value; }
    struct Seventh { public int Value; }
    struct Eighth { public int Value; }
// <auto-generated />
#nullable enable
#pragma warning disable CS0436 // Demand-generated callback contracts can also arrive through a referenced consumer assembly.


}
