# Choosing the DeltaECS API

Choose an API by the work your system needs to do, then choose its callback and registration forms. This guide covers the public core runtime and generated consumer API; the [API grammar](API-GRAMMAR.md) lists the supported signatures and argument order.

**Start with variadic `ForEach` when the component types are known.** Add `EntityRef` only when the handler also needs the entity. Retain a `ForEachChange<T>` operation when the task is to react to changes between calls.

## Reading the tables

Examples in tables are call fragments: `q`, component IDs, callbacks and comparers are assumed to be declared. The complete examples below need the `DeltaECS` runtime and `DeltaECS.Generators` packages.

- `N`: candidate entities; `C`: visited chunks; `A`: archetypes examined during preparation or initial change reporting; `P`: entities passing a value predicate; `L`: entities in an explicit input list.
- `M`: pending component membership changes; `D`: dirty chunks; `B`: mask words examined; `E`: emitted change notifications.
- `H`: entity-index capacity used by a change operation; `S`: component bytes initialized, copied or cleared by a structural operation.

Complexities describe execution with a prepared query, a **fixed generated signature**, fixed filters and fixed ordering keys. They exclude user callback work, allocation growth and cold preparation unless explicitly listed. Add `O(B)` to traversal bounds when tag selection needs rebuilding or mask scanning.

Generated arity is a compile-time shape: `ForEach<Position, Velocity>` does not run a generic loop over a runtime list of component types for every entity. Its dense traversal is `O(C + N)`. More component parameters increase bindings, instructions and memory traffic, but do not introduce another variable into that fixed signature's Big-O.

## Process entities or component data

| Task | API | Example | What it does | Execution complexity | Potential overhead |
| --- | --- | --- | --- | --- | --- |
| Read or update known components on every match | **Variadic `ForEach`** | `world.ForEach(in q, static (ref Health h) => h.Value--).Invoke()` | Passes typed component references directly to the handler | `O(C + N)` | Row preparation per chunk; declared writes also notify active change observers. Default choice for dense component work |
| Process known components and use the entity | **Variadic `ForEachEntity`** | `world.ForEachEntity(in q, static (EntityRef e, ref Health h) => Apply(e, ref h)).Invoke()` | Gives the handler the entity and direct component references | `O(C + N)` | Updates the current entity view as it advances. Use the component parameters rather than fetching those components again |
| Process only entities, or access optional components selected at runtime | **Zero-arity `ForEachEntity`** | `world.ForEachEntity(in q, static (EntityRef e) => Visit(e)).Invoke()` | Supplies an entity view without component parameters | `O(C + N)` plus component accesses | Each `Has`, `GetRef` or other getter resolves the requested component access. This work remains even though entity-handle lookup is skipped |
| Process independent, sufficiently large component workloads in parallel | Variadic `ForEachParallel` | `world.ForEachParallel(in q, update, workerCount: 4).Invoke()` | Distributes typed component work among workers | Total work `O(C + N)` | Scheduling and synchronization; elapsed time is not guaranteed to be sequential time divided by worker count |
| Run independent entity-aware work with known components in parallel | Variadic `ForEachEntityParallel` | `world.ForEachEntityParallel(in q, entityUpdate, workerCount: 4).Invoke()` | Combines typed rows, an entity view and parallel execution | Total work `O(C + N)` | Parallel costs plus entity-view maintenance |
| Run entity-only or genuinely dynamic work in parallel | Zero-arity `ForEachEntityParallel` | `world.ForEachEntityParallel(in q, entityCallback, workerCount: 4).Invoke()` | Parallel entity callbacks without typed component parameters | Total work `O(C + N)` plus accesses | Parallel costs and component access resolution inside the callback |
| Process a list that another system already selected | Entity-list `ForEach` / `ForEachEntity` | `world.ForEach(entities, in q, update).Invoke()` | Visits the supplied handles, optionally checking a query | `O(L)` for a fixed signature | Handle resolution and scattered memory access. Construction saves the input list; it is not a borrowed span held until invocation |
| React to additions, writes and removals since the last call | **Retained `ForEachChange<T>`** | `var changes = world.ForEachChange<Health>(healthId, onChange);` then `changes.Invoke()` | Establishes a baseline and consumes incremental changes for one registration across the world | First: `O(H + A + C + B + N)`; later: `O(M + D + B + E)`; no pending changes: `O(1)` | Observer state and notification costs, including costs in the component's write path. No manual before/after value snapshots are needed |
| Select a generic processor from a registration whose CLR type is unknown | Runtime-selected generic functor | `world.ForEach(in q, id, typeof(SaveHistory<>)).Invoke()` | Closes and caches a typed executor for the selected registration | After preparation: the corresponding typed traversal | First-use type dispatch and executor preparation; not reflection dispatch on each entity |

Zero-arity `ForEach` without an entity or component parameter is a generator anchor, not an executable substitute for these forms: invoking that anchor throws.

**Equal Big-O does not mean equal speed.** A zero-arity entity callback that calls two getters per entity and a variadic callback receiving two references are both linear. The former still performs extra component-access work `N` times. Use the direct component parameters when those types and accesses are already known.

## Filter, select or order results

| Task | API | Example | What it does | Execution complexity | Potential overhead |
| --- | --- | --- | --- | --- | --- |
| Require components, accept alternatives, or exclude paused/dead entities | `WhereAll` / `WhereAny` / `WhereNone` | `world.WhereAll<Health, Enemy>().WhereNone<Paused>()` | Describes component membership; data components filter archetypes, tags filter slots | Query preparation depends on archetypes and filters; subsequent iteration pays for matching chunks/entities and tag masks | Cold plan preparation and selection-cache updates. Prefer this to repeating membership tests in every callback |
| Assemble filters from configuration | `QuerySpec` + `CreateQuery` | `world.CreateQuery(in spec)` | Creates a reusable query from runtime component IDs | Preparation depends on the filter set and current archetypes | Save the query when it is reused; a query describes live matches, not a snapshot of entity handles |
| Filter by component values | `Where` | `world.Where(in q, static (in Health h) => h.Value > 0).ForEach(update).Invoke()` | Evaluates a predicate and runs the terminal on its matches | `O(C + N + P)` | Predicate evaluation on every candidate; prefer membership filters for presence/absence tests |
| Filter by values and entity identity | `WhereEntity` | `world.WhereEntity(in q, entityPredicate).ForEach(update).Invoke()` | Adds the entity handle to the predicate's component inputs | `O(C + N + P)` | Entity access and predicate work. The predicate receives `Entity`; an entity-aware terminal receives `EntityRef` |
| Get any matching entity | `Query.First` | `q.First().Invoke()` | Stops at the first match in traversal order | Up to `O(C + B)` | May inspect chunks and rebuild tag selection; does not sort results |
| Get the nearest, smallest or highest-priority entity | `OrderBy(...).First()` | `q.OrderBy(ref distanceComparer).ThenBy(ref idComparer).First().Invoke()` | Finds the minimum under the specified ordering | `O(C + N)` comparisons for fixed keys | Component-key reads and comparisons. It does **not** sort the whole result |
| Process every result in priority order | Ordered `ForEach` / `ForEachEntity` | `q.OrderBy(ref priorityComparer).ForEach(update).Invoke()` | Collects matching handles, sorts them and processes that order | `O(C + N log N)` | `O(N)` reusable entity buffers, comparisons and less dense component access |
| Order only the entities passing a value predicate | `Where(...).OrderBy(...)` | `world.Where(in q, alive).OrderBy(ref priorityComparer).ForEach(update).Invoke()` | Checks candidates, then sorts only the matches | `O(C + N + P log P)` | Predicate work plus buffers and sorting for `P` matches. Use `.First()` instead when only one result is needed |
| Apply structural changes to value-filtered results | `Where` structural terminal | `world.Where(in q, dead).Remove<Enemy>().Invoke()` | Selects entities and applies a deferred structural operation | Candidate traversal plus structural work | Selection storage and component migration. Express the change through the terminal rather than a structural call inside the traversal callback |

`OrderBy` and `ThenBy` describe ordering; the terminal determines its cost. Do not pay for sorting all results just to select one. Ordered iteration is sequential.

## Choose the pipeline in execution order

Choose these independently before selecting callback syntax:

1. **Input set:** a supplied target list needs list iteration; a live world selection needs a query. Scanning the world and testing list membership changes work from `O(L)` to at least `O(N)`.
2. **Registration knowledge:** runtime IDs with a known CLR type still use typed component parameters and explicit IDs. Unknown CLR types need preparation through visitors or a supported runtime generic functor. Membership-only cleanup consumes IDs directly and needs neither.
3. **Eligibility:** apply membership filters, then read-only value predicates. Choose a minimum only among eligible entities. Include every component needed by a comparison, including tie-break keys.
4. **Terminal:** one result needs a minimum reduction (`First` on an ordering); all results in priority order need ordered iteration. For an external list, a typed entity-list callback can perform a linear minimum reduction without sorting.
5. **Mutation phases:** finish value updates before selecting targets whose eligibility depends on those updates. Complete callbacks before structural changes. If the contract says snapshot **all** registrations before reset, invoke every snapshot operation before invoking any reset operation, rather than snapshot/reset pairs per registration.

Examples of distinct compositions are `Where → OrderBy → First`,
`Where → OrderBy → ForEach`, and `ForEach(write) → Where(updated value) → Destroy`.
An event-fed cleanup instead uses `ForEachChange → retain Entity handles → batch Remove`;
it must not replace incremental observation with a full-world rescan.

For a filtered ordered traversal, the generated comparer-functor form is the
checked composition: `world.Where(in query, predicate).OrderBy(ref comparer)`
followed by `.ThenBy(ref tieComparer).ForEach((ref Health value) => ...)`.
Define the comparers as `IComponentComparer` structs with typed `Invoke(in T
left, in T right)` methods; put the fixed reference point in the comparer
before constructing the operation. If a callback-based ordering chain fails
generation, use this form before replacing ordered traversal with repeated
`First()` calls. Repeated minima cost `O(N*K)` for `K` processed targets,
where a single ordered traversal costs `O(N + P log P)` after filtering.

Value predicates currently select primary registrations; do not invent an
explicit-ID `Where` overload for two registrations sharing a CLR type. When
that composition is unavailable, retain a membership query, order with
explicit component IDs, and check eligibility in the typed ordered terminal
before consuming budget. This sorts all candidates (`O(N log N)`), but preserves
the order of eligible rows. Declare the intermediate receiver as
`OrderedQuery ordered = query.OrderBy(sourceId, stableId, ref comparer);`
before building `ordered.ForEach<float, float>(sourceId, destinationId, callback)`.
This explicit receiver form is supported by the current generator. Prefer a
supported filter-before-sort composition when its predicate selects the intended
registrations; a predicate silently reading the primary registration is incorrect.

The cleanup target list does not exist until observation completes. Do not
construct a deferred entity-list operation over an empty list and expect it
to see later additions: list inputs are copied during operation construction.
Use an immediate batch call after collection. With a reusable `List<Entity>`
on a runtime providing `CollectionsMarshal.AsSpan`, pass its span to the batch
call; otherwise use a reusable array and its populated prefix. A repeated
`ToArray()` introduces an allocation on each nonempty update. Do not retain
the borrowed span or an `EntityRef` beyond the call that uses it.

Keep the predicate and comparer on the same reference point. Distance to an
origin is not distance to a player unless that player is actually at the
origin. A fixed origin may be read during construction; a moving origin must
be refreshed before every invocation. Comparer/context values are copied
when the ordering is built: changing the original struct afterwards does not
update a retained operation. A comparer and predicate can share a reference
state object whose origin is refreshed once before `Invoke()`. Reset
per-call reduction/budget state before invocation, while retaining the query
and operation themselves.

## Access, create or reconfigure data

| Task | API | Example | What it does | Complexity | Potential overhead |
| --- | --- | --- | --- | --- | --- |
| Read one known entity's component | `Get<T>` / `TryGet<T>` / `GetReadRef<T>` | `ref readonly Health h = ref world.GetReadRef<Health>(entity);` | Reads a copy or a read-only reference; `TryGet` handles absence | `O(1)` lookup; copies also depend on value size | Entity validation and component resolution. For a whole row of entities, use typed iteration |
| Write one known entity's component | `GetRef<T>` | `world.GetRef<Health>(entity).Value -= damage;` | Opens a mutable reference | `O(1)` lookup plus observer notification | Declares a write even if the value remains unchanged. Do not use it for read-only work |
| Check liveness, membership or population | `IsAlive`, `Has`, `AliveEntityCount` | `world.IsAlive(entity)`; `world.Has(entity, id)` | Inspects world state | `O(1)` | Repeated checks inside an already filtered typed loop add avoidable work. `Entity.IsValid` alone does not prove liveness |
| Create with default values or known values | `Create` | `world.Create<Health>()`; `world.Create(healthId, new Health(100))` | Creates an entity and initializes its component storage | Amortized storage work proportional to `S`; batches also scale with entity count | Capacity growth, registration validation and structural bookkeeping; batch forms avoid repeated call preparation |
| Add or remove registrations without typed values | Type-erased `Add` / `Remove` | `world.Add(entity, id)`; `world.Remove(in q, ids)` | Changes membership using IDs directly | Selection work plus structural work proportional to affected data | Data-component changes may move/copy rows. Tags update membership masks. A CLR type is unnecessary when only IDs are consumed |
| Add values or select a primary registration by type | Typed `Add` / primary `Remove` | `world.Add(entity, new Health(100))`; `world.Remove<Health>(entity)` | Supplies a value or selects a type's primary registration | Selection plus structural work | Use combined multi-component forms for one combined transition when changing several data components |
| Destroy one entity, a list or query matches | `Destroy` | `world.Destroy(entity)`; `world.Destroy(in q)` | Removes entities and their components | Depends on selection and data removed/moved; an explicit list may also require `O(L log L)` grouping/sorting | Dense query deletion can use chunk work; arbitrary lists and tag-filtered deletion require more selection work |
| Map a stable schema or config name to a runtime ID | Registration, schema lookup and aliases | `world.Components.TryGetComponentId(schemaId, out var id)` | Resolves the registration once for later use | Expected `O(1)` indexed lookup | Resolve configuration names/IDs during loading, then keep `ComponentId` rather than repeating string lookups in hot loops |
| Get a configured category of registrations | Component groups | `world.GetGroup(Groups.History)` | Returns IDs assigned to a group | Cached lookup: expected `O(1)`; rebuilding the member view: `O(group size)` | Subsequent processing scales with the group's size. Group membership does not itself select entities |
| Build typed behavior from a CLR type known only through an ID | Component visitors | `world.Components.TryVisit(id, visitor)` | Dispatches once to a generic `Visit<TComponent>` | Dispatch per registration, plus visitor work | Build and retain typed handlers/operations here rather than dispatching a visitor for every entity |
| Declare component metadata at build time | Schema, alias and group attributes; generated catalog | `[ComponentSchema(10)]`; `[ComponentGroup(Groups.History)]` | Describes registrations and categories for generated metadata | Registration/catalog preparation, outside the entity loop | Optional metadata; ordinary components need no ECS attributes. See the grammar for catalog and interface-binding routes |
| Inspect/edit completely dynamic values in tooling | `IEcsWorld` integration API | `integration.TryRead(entity, id, out var snapshot, out var error)` | Bridges entity/component data to tools through object-based contracts | Lookup plus conversion/copying | Struct values can be boxed. Prefer typed APIs for hot simulation work |

## Choose the callback and registration form

These are independent choices within an iteration family; they do not require switching to zero arity.

| Requirement | Choose | Why |
| --- | --- | --- |
| One ordinary registration per component type | Primary-registration overload | Shortest typed call; the component types choose their primary IDs |
| Multiple registrations share one CLR type, or configuration chooses the registration | Typed callback with explicit `ComponentId`s | A runtime ID selects a typed row. **A dynamic registration is not an unknown CLR type** |
| Build the registration list programmatically | Typed callback with `ReadOnlySpan<ComponentId>` | Keeps typed component parameters; IDs must match their number and types. Construction preserves the inputs for later invocation |
| Only read a component | `in T` or supported `ref readonly T`; `GetReadRef<T>` for individual access | Avoids declaring writes and generating change notifications for read-only work |
| Change a component | `ref T` / `GetRef<T>` | Declares writable access and participates in change tracking when observed |
| Keep a local copy | Value parameter / `Get<T>` | Copying is intentional; avoid copying large structs just to read them |
| Short local behavior | Lambda, preferably `static` when no capture is needed | Easy to read. An ordinary delegate can retain an indirect call; eligible intercepted lambdas can use a generated direct path |
| Reusable typed behavior or mutable processor state | Struct functor | A concrete generic call avoids boxing and permits specialization. Do not erase it to an interface just to invoke it |
| Pass/update external state without a closure | Caller-owned context overload | Explicit ownership; parallel forms accept read-only/value context, not shared mutable `ref` context |

## Example: update known components

Task: move every entity with `Position` and `Velocity`, excluding paused entities. The entity handle is unnecessary, so use variadic `ForEach`:

```csharp
using System;
using Delta.ECS;

using var world = new World();
world.Components.Register<Position>(new SchemaId(1));
world.Components.Register<Velocity>(new SchemaId(2));
world.Components.Register<Paused>(new SchemaId(3));

Entity entity = world.Create<Position>();
world.Add(entity, new Velocity(2));
Query moving = world.WhereAll<Position, Velocity>().WhereNone<Paused>();

var move = world.ForEach(in moving,
    static (ref Position position, in Velocity velocity) => position.X += velocity.X);

move.Invoke();
move.Invoke();
Console.WriteLine(world.Get<Position>(entity).X); // 4

public record struct Position(float X);
public record struct Velocity(float X);
public record struct Paused;
```

If another part of the handler also needs the entity, use `ForEachEntity` with the same component parameters. Do not replace them with `e.GetRef<Position>()` and `e.GetReadRef<Velocity>()`. If configuration selects an alternate position registration, pass its ID to the typed overload; keep the `ref Position` parameter.

## Example: update a cache only when health changes

Task: report current health when it first appears or is opened for writing, and remove cached entries when health disappears. Keep one change operation across calls:

```csharp
using System;
using Delta.ECS;

using var world = new World();
ComponentId healthId = world.Components.Register<Health>(new SchemaId(1));
Entity entity = world.Create(healthId, new Health(100));

var changes = world.ForEachChange<Health>(healthId,
    static (Entity entity, ChangeKind kind, ref Health health) =>
    {
        if (kind == ChangeKind.Removed)
        {
            Console.WriteLine($"{kind}: {entity.Index}");
            return;
        }

        Console.WriteLine($"{kind}: {health.Value}");
    });

changes.Invoke();                             // Added: 100
world.GetRef<Health>(entity, healthId).Value -= 25;
changes.Invoke();                             // Modified: 75
changes.Invoke();                             // No output
world.Remove(entity, healthId);
changes.Invoke();                             // Removed: <entity index>

public record struct Health(int Value);
```

This is **write-access and membership tracking**, not value comparison. `ref` access counts as a modification even if the value is unchanged. Data components and tags follow this rule; tags have no stored payload, so their writable reference is a dummy value.

The first invocation reports existing instances as `Added`. Later invocations coalesce changes since that operation's previous successful call: add then remove can cancel, while remove then add of the same entity/component becomes `Modified`. Reusing an index with a new generation keeps the old entity's removal separate from the new entity's addition. A `Removed` callback receives a null component reference, which must not be read, and the original entity handle, which may now be stale. Writes made through this operation's own callback are not reported again to itself.

Use manual value comparison only when the task specifically needs equality-based differences. For ordinary write/membership notifications, a second cache of component copies duplicates work already provided by `ForEachChange`.

## Preparation, reuse and lifetime

- Build and retain queries and operations used repeatedly. A `ForEach` call creates an operation; `Invoke()` executes it against current matches. Binding preparation and topology changes can cost more than an ordinary warm invocation.
- Use `var` for a local generated operation. For retained ordinary traversal fields, `IOperation` is a supported type; for a result terminal use its `EcsResultOperation<T>` type. Grammar family names and invented `ForEachInvoker` types are not field declarations. Storing a generated operation struct as `IOperation` boxes it once during preparation, not once per entity.
- Creating an operation is not universally allocation-free: closures, captured input lists, ordering descriptions, change subscriptions and cold caches have their own storage costs. Concrete generated operation structs avoid boxing on direct invocation; storing one as `IOperation` boxes it once. A constrained generic call can preserve its concrete type.
- Keep change operations for as long as their baseline is needed. Constructing a new one for every update creates a new subscription and restarts initial reporting.
- Write observation has a cost in the writer as well as the consumer. Dense writes can mark chunks or masks for observers; point writes record selected slots. Value predicates do not turn write tracking into an exact comparison of modified values.
- Borrow an `EntityRef` only within its callback. Convert it to `Entity` when a handle must be retained. Do not perform immediate structural changes from an active traversal; use a structural terminal or apply those changes after traversal.

For repeated commands over a fixed set of registrations, retaining a visitor
object is not the same as preparing typed handlers. A `TryVisit` inside every
command still dispatches the registration on every call. Dispatch each ID in
the constructor and keep the resulting handler instead:

```csharp
// Construction-time visitor callback; ResetHandler<T> is your typed handler.
public void Visit<T>(ComponentId id) where T : IResettable =>
    handlers.Add(id, new ResetHandler<T>(world, id));

// Repeated command: lookup the already prepared handler and invoke it.
return handlers.TryGetValue(id, out var handler) && handler.TryReset(target);
```

This is a preparation sketch, not a complete class. Each typed handler uses
`Has` and `GetRef<T>` for its one target and performs the constrained call on
the stored component. Those point accesses are appropriate here; scanning the
world to update one known entity is not. A one-off visitor call remains useful
when there is no repeated workload to amortize preparation.

For exact overloads use the [API grammar](API-GRAMMAR.md); for more complete scenarios use the [API cookbook](usage-examples.md).

## Several unknown component types implementing one interface

An entity can carry several selected debuffs at once. A fixed list of their
registration IDs describes **separate component-instance traversals**, not a
query requiring every debuff simultaneously. Prepare each registration once;
then execute every prepared traversal against its current matches. Keep
explicit IDs when multiple registrations share one CLR type.

Mutate struct implementations through their actual stored reference using a
constrained generic call. Casting a struct value to its interface boxes a
copy; changing that copy does not update the stored component.

The current generator has two relevant limits. Runtime-selected open generic
functors do not accept user interface/base-class constraints (`DECSGEN008`).
An inline typed lambda mentioning a visitor method's open `T` can also emit
`T` outside its scope (`CS0246`). Do not repeatedly retry those signatures or
erase values to `object` to make them compile. An unconstrained generic
read-only snapshot functor is supported; interface mutation can use a cold
constrained visitor to construct a closed generic handler, with a retained
entity-only operation calling an ordinary constrained helper:

```csharp
public interface ITickable { void Tick(int duration); }

public sealed class TickHandler<T> where T : ITickable
{
    private readonly IOperation operation;

    public TickHandler(World world, ComponentId id, int duration)
    {
        Query query = world.WhereAll(id);
        operation = world.ForEachEntity(in query,
            (EntityRef entity) => TickOne(entity, id, duration));
    }

    private static void TickOne(EntityRef entity, ComponentId id, int duration)
        => entity.GetRef<T>(id).Tick(duration);

    public void Invoke() => operation.Invoke();
}
```

Construct `TickHandler<T>` in the visitor's constrained `Visit<T>` and retain
it through a caller-owned non-generic handler contract. This supported
fallback pays a component lookup for every instance; it is not the direct
typed-row path recommended for statically known components. Keep that cost
visible in API reviews. For expiring effects, retain only expired entity
handles during the callback and remove their registration after traversal.

For snapshot-then-reset workloads, use the supported unconstrained typed
read-only functor for the snapshot phase, so the callback receives both the
entity and the typed value without a second component lookup. Reserve the
constrained entity-handler fallback for the reset phase. For expiration,
batch the collected handles per registration rather than removing each entity's
component with a separate structural call.
