# Parallel iteration

`World.ForEachParallel` is an opt-in parallel terminal for a prepared
`Query`. The generated typed overload keeps the normal delegate syntax and
assigns disjoint active chunks to a reusable worker pool:

```csharp
var query = world.CreateQuery(QuerySpec.WhereAll(positionId, velocityId));

world.ForEachParallel(
    in query,
    static (ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X;
    },
    workerCount: 4);
```

The generated callback is invoked inside the worker-owned chunk range. The
callback may mutate its declared write rows, but it must not retain component
references, a `QueryChunk`, or a row view after returning. Captured mutable
state remains the caller's responsibility; use a per-worker result or another
explicit synchronization strategy when the callback shares state.

`workerCount: 0` selects the runtime default. The default keeps small workloads
on the sequential generated path because waking workers costs more than the
work. An explicit count greater than one requests parallel execution. Context
and functor forms are currently serialized when their state cannot be safely
merged between worker-local invoker copies.

## Coordination model

The executor owns persistent workers and reusable arrays per `World`. Once the
worker pool, flattened chunk list, and ranges are warm, a stable query topology
does not allocate on the caller thread. The flattened chunk list and static
ranges are rebuilt only when `QueryPlan.MatchingVersion` changes or the worker
count changes.

Each worker has a padded state slot. The caller publishes `StartChunk` and
`EndChunk` with a release `Volatile.Write(PublishedRun, run)`. The worker
observes that value with an acquire read, processes its fixed range, and
publishes `CompletedRun` with a release write. The caller waits on each worker's
own completion value. There is no lock in a warmed frame, no per-chunk
`Interlocked.Increment`, no global work-stealing counter, and no full
`MemoryBarrier` in the execution path. `Interlocked.CompareExchange` is used
once per public call only to reject overlapping executions on one `World`.

The low-level overload
`World.ForEachParallel(in Query, QueryChunkAction, int)` uses the same static
range protocol. It exists for code that intentionally owns the chunk-level
loop; the generated typed overload is the preferred user-facing form.

Structural changes are rejected until the call returns. The first call may
grow caches and create worker threads; disposal joins those workers. This is a
cold-path cost and is not part of the steady-state allocation guarantee.

## Benchmark

`ParallelMovement4IterationBenchmarks` compares the same four-component
`Movement4` update through the generated sequential and generated parallel
terminals. Setup, worker creation, route preparation, range construction and
entity creation happen before the measured method. The callback has no
checksum or extra synchronization.

```bash
dotnet build benchmarks/DeltaECS.Benchmarks/DeltaECS.Benchmarks.csproj \
  -c Release --disable-build-servers -m:1 /p:UseSharedCompilation=false

dotnet benchmarks/DeltaECS.Benchmarks/bin/Release/net10.0/DeltaECS.Benchmarks.dll \
  parallel --filter '*ParallelMovement4IterationBenchmarks*' \
  --job Default --iterationTime 100 --warmupCount 5 \
  --iterationCount 10 --launchCount 1 --exporters csv \
  --artifacts artifacts/benchmarks/parallel-iteration
```

The bounded smoke run in this experiment used two warmups and five 100 ms
measurements on Apple M4 Pro / .NET 10.0.9. It showed the parallel terminal
behind the sequential terminal at 100 and 1,000 entities, ahead at 10,000 and
100,000, and a noisy 1,000,000 result because two samples included worker
scheduling delays. Both paths reported zero GC collections during measured
operations. This is evidence that the protocol works and that its benefit is
workload-dependent; a longer isolated run is required before merging it as a
performance improvement.
