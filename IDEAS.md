# DeltaECS ideas

These are not active tasks. Require an explicit decision and measured workload.
Only untested proposals belong here. Accepted, rejected and inconclusive
experiments belong in the [optimization experiment ledger](docs/performance/experiments/README.md).

## Cross-world versioned subscriptions

Status: untested; architectural idea, not a dense hot-loop optimization.

An external projection layer could give each consumer its own query, watched
component set and version cursor. It would coalesce latest `Added`, `Changed`
and `Removed` flags rather than retain an ordered event log.

Keep it outside the storage kernel:

- source and target worlds retain independent component registries;
- stable schema IDs build a cached mapping to local `ComponentId` values;
- a generation-aware entity map translates source entities to target proxies;
- consumers never clear global change state needed by another consumer;
- begin with chunk/row versions and add entity dirty masks only after measuring
  excessive copying.

Possible order if promoted: complete all write-version marking, add changed-row
cursors, add structural version/tombstone state, then build the external world
projection. Do not add per-entity subscriptions to the base world.

## Parallel system graph

Status: untested; architectural scheduling idea, not a replacement for the
current query or delegate API.

Add a higher-level `SystemGraph` that validates system access sets and builds a
cached execution schedule. A system declares its query, the `ComponentId`
values it reads and writes, and whether it performs structural changes. The
graph derives dependencies from those sets:

- read/write and write/write conflicts create ordering edges;
- structural systems create explicit barriers;
- independent systems are grouped into the same parallel level;
- compatible systems may later be fused into one chunk traversal, but only
  after proving equivalent ordering and side-effect semantics.

The intended split is:

```text
SystemGraph    -> nodes, declarations and dependency validation
SystemPlan     -> immutable validated plans and cached query routes
SystemSchedule -> topological levels and worker assignments
SystemContext  -> per-execution state
```

Building the graph is the cold path. It should validate world/query ownership,
component registration, access conflicts, structural barriers and the current
archetype/query-plan version. `Execute()` should only validate the graph
version, select a sequential or parallel schedule, and run already prepared
plans. Small workloads need a sequential fast path so scheduler overhead does
not dominate.

The first useful API shape is conceptually:

```csharp
var graph = world.CreateSystemGraph();
graph.Add("Movement", movementQuery,
    reads: new[] { velocityId },
    writes: new[] { positionId });
graph.Add("Bounds", boundsQuery,
    reads: new[] { positionId },
    writes: new[] { velocityId });
graph.Build();
graph.Execute(deltaSeconds);
```

Keep the graph outside the storage hot loop. Existing `World.ForEach`, query
plans and delegate/functor callbacks remain the execution primitives; graph
nodes wrap them rather than introduce a second entity iteration mechanism.
Immediate structural operations inside parallel systems require a barrier or
command batch. Mutable reference components and hidden aliases must be
treated as conflicts unless the graph can prove disjoint ownership.

Expected benefits are amortized query matching and access preparation,
parallel execution of independent systems, chunk partitioning, and eventual
query fusion. A graph cannot materially speed up one isolated `Movement4`
loop; its value appears when several systems share the same frame and can
reuse traversal or execute concurrently.

Promotion criteria:

1. Add descriptors and dependency validation without changing current query
   APIs.
2. Measure a cached sequential schedule against direct system calls.
3. Add parallel levels only where independent workloads outweigh scheduling
   overhead.
4. Measure chunk partitioning and fusion separately, preserving deterministic
   ordering where required.

Do not promote this idea until dependency validation, structural barriers,
version invalidation, determinism and small-workload fallback have dedicated
tests and benchmark evidence.

## Performance candidates

Evidence and candidate order live in
[docs/performance/README.md](docs/performance/README.md). Promote one candidate
at a time with accumulator parity, JIT capture and an unchanged public API.
Roslyn delegate interception is no longer a candidate: it is accepted and
recorded in the [experiment ledger](docs/performance/experiments/README.md#accepted-evidence-roslyn-delegate-interception).
