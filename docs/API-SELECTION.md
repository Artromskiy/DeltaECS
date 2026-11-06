# Choosing the DeltaECS API

Choose an API by the work your system needs to do, then choose its callback and registration forms. This guide covers the public core runtime and generated consumer API; the [API grammar](API-GRAMMAR.md) lists the supported signatures and argument order.

**Choose the unit of work first.** For a runtime-defined set of component IDs and values on **one entity**, use `EntityData`. For the same known component accesses across **many entities**, start with variadic `ForEach`; add `EntityRef` when the handler also needs the entity. Retain a `ForEachChange<T>` operation when the task is to react to changes between calls.

## Reading the tables

Examples in tables are call fragments: `q`, component IDs, callbacks and comparers are assumed to be declared. The complete examples below need the `DeltaECS` runtime and `DeltaECS.Generators` packages.

- `N`: candidate entities; `C`: visited chunks; `A`: archetypes examined during preparation or initial change reporting; `P`: entities passing a value predicate; `L`: entities in an explicit input list.
- `M`: pending component or query membership changes; `D`: dirty chunks; `B`: mask words examined; `E`: emitted change notifications.
- `H`: entity-index capacity used by a change operation; `S`: component bytes initialized, copied or cleared by a structural operation.
- `K`: entries in an `EntityData` set; `U`: data components on a source entity; `T_c`: tag registrations present anywhere in its chunk.

For generated iteration, complexities describe execution with a prepared query, a **fixed generated signature**, fixed filters and fixed ordering keys. They exclude user callback work, allocation growth and cold preparation unless explicitly listed. Dynamic input lists and data sets retain their explicit `L` and `K` terms. Add `O(B)` to traversal bounds when tag selection needs rebuilding or mask scanning.

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
| React to additions, writes and removals since the last call | **Retained runtime `ForEachChange<T>`** | `var changes = world.ForEachChange<Health>(healthId, onChange);` then `changes.Invoke()` | Establishes a baseline and consumes coalesced changes for one registration; pass `in q` before the ID to restrict tracking to component/tag membership and grouped values. No source generator is needed | First: `O(H + A + C + B + N)`; later: `O(M + D + B + E)`; no pending changes: `O(1)` | Observer state and notification costs, including costs in the component's write path. A writable access counts even if the value is unchanged. With a query, entering reports `Added` and leaving reports `Removed`; no manual before/after value snapshots are needed |
| Select a generic processor from a registration whose CLR type is unknown | Runtime-selected generic functor | `world.ForEach(in q, id, typeof(SaveHistory<>)).Invoke()` | Closes and caches a typed executor for the selected registration | After preparation: the corresponding typed traversal | First-use type dispatch and executor preparation; not reflection dispatch on each entity |

Zero-arity `ForEach` without an entity or component parameter is a generator anchor, not an executable substitute for these forms: invoking that anchor throws.

**Equal Big-O does not mean equal speed.** A zero-arity entity callback that calls two getters per entity and a variadic callback receiving two references are both linear. The former still performs extra component-access work `N` times. Use the direct component parameters when those types and accesses are already known.

## Filter, select or order results

| Task | API | Example | What it does | Execution complexity | Potential overhead |
| --- | --- | --- | --- | --- | --- |
| Require components, accept alternatives, or exclude paused/dead entities | `WhereAll` / `WhereAny` / `WhereNone` | `world.WhereAll<Health, Enemy>().WhereNone<Paused>()` | Describes component membership; data components filter archetypes, tags filter slots | Query preparation depends on archetypes and filters; subsequent iteration pays for matching chunks/entities and tag masks | Cold plan preparation and selection-cache updates. Prefer this to repeating membership tests in every callback |
| Repeatedly select the same squad, team or owner | **Value grouping** | `world.WhereAll(new MemberOf(squad))` | Selects archetypes by an opt-in grouped component value | Normal matching-chunk traversal after regrouping | Writes may relocate entities; use bounded, recurring keys. See [grouping and changes](#choose-grouping-for-recurring-equality-filters) |
| Assemble filters from configuration | `QuerySpec` + `CreateQuery` | `world.CreateQuery(in spec)` | Creates a reusable query from runtime component IDs | Preparation depends on the filter set and current archetypes | Save the query when it is reused; a query describes live matches, not a snapshot of entity handles |
| Filter by ranges or arbitrary component values | `Where` | `world.Where(in q, static (in Health h) => h.Value > 0).ForEach(update).Invoke()` | Evaluates a predicate and runs the terminal on its matches | `O(C + N + P)` | Predicate evaluation on every candidate; prefer membership filters for presence/absence tests |
| Filter by values and entity identity | `WhereEntity` | `world.WhereEntity(in q, entityPredicate).ForEach(update).Invoke()` | Adds the entity handle to the predicate's component inputs | `O(C + N + P)` | Entity access and predicate work. The predicate receives `Entity`; an entity-aware terminal receives `EntityRef` |
| Get any matching entity | `Query.First` | `q.First().Invoke()` | Stops at the first match in traversal order | Up to `O(C + B)` | May inspect chunks and rebuild tag selection; does not sort results |
| Get the nearest, smallest or highest-priority entity | `OrderBy(...).First()` | `q.OrderBy(ref distanceComparer).ThenBy(ref idComparer).First().Invoke()` | Finds the minimum under the specified ordering | `O(C + N)` comparisons for fixed keys | Component-key reads and comparisons. It does **not** sort the whole result |
| Process every result in priority order | Ordered `ForEach` / `ForEachEntity` | `q.OrderBy(ref priorityComparer).ForEach(update).Invoke()` | Collects matching handles, sorts them and processes that order | `O(C + N log N)` | `O(N)` reusable entity buffers, comparisons and less dense component access |
| Order only the entities passing a value predicate | `Where(...).OrderBy(...)` | `world.Where(in q, alive).OrderBy(ref priorityComparer).ForEach(update).Invoke()` | Checks candidates, then sorts only the matches | `O(C + N + P log P)` | Predicate work plus buffers and sorting for `P` matches. Use `.First()` instead when only one result is needed |
| Apply structural changes to value-filtered results | `Where` structural terminal | `world.Where(in q, dead).Remove<Enemy>().Invoke()` | Selects entities and applies a deferred structural operation | Candidate traversal plus structural work | Selection storage and component migration. Express the change through the terminal rather than a structural call inside the traversal callback |

`OrderBy` and `ThenBy` describe ordering; the terminal determines its cost. Do not pay for sorting all results just to select one. Ordered iteration is sequential.

## Compose the work

1. Select a supplied entity list in `O(L)`, or build a live world query.
2. Apply component/tag and grouped-value filters before a read-only `Where` predicate.
3. Use `OrderBy(...).First()` for one best result; ordered iteration for all results in order. Repeated minima repeat the scan.
4. Finish observation or value updates before applying structural changes. Snapshot all registrations before resetting any when that is the task's contract.

Runtime IDs with known CLR types still use typed parameters. For unknown types,
use a visitor during preparation; for a runtime-defined set of values on one
entity, use `EntityData`.

Entity-list operations copy their input at construction: collect handles first,
then invoke a batch operation. Reuse list/array storage instead of calling
`ToArray()` every update. A moving origin or per-call damage budget must be
refreshed before invocation; changing a struct copied into an operation does
not update its stored copy.

Value predicates currently select primary registrations. For repeated CLR types,
use explicit IDs in supported ordering/iteration forms; do not invent an
explicit-ID `Where` overload. The [grammar](API-GRAMMAR.md) specifies each composition.

## Access, create or reconfigure data

| Task | API | Example | What it does | Complexity | Potential overhead |
| --- | --- | --- | --- | --- | --- |
| Read one known entity's component | `Get<T>` / `TryGet<T>` / `GetReadRef<T>` | `ref readonly Health h = ref world.GetReadRef<Health>(entity);` | Reads a copy or a read-only reference; `TryGet` handles absence | `O(1)` lookup; copies also depend on value size | Entity validation and component resolution. For a whole row of entities, use typed iteration |
| Write one known entity's component | `GetRef<T>` | `world.GetRef<Health>(entity).Value -= damage;` | Opens a mutable reference | `O(1)` lookup plus observer notification | Declares a write even if the value remains unchanged. A tag's reference is a placeholder, but writable access still notifies change observers. Do not use `GetRef` for read-only work |
| Check liveness, membership or population | `IsAlive`, `Has`, `AliveEntityCount` | `world.IsAlive(entity)`; `world.Has(entity, id)` | Inspects world state | `O(1)` | Repeated checks inside an already filtered typed loop add avoidable work. `Entity.IsValid` alone does not prove liveness |
| Create with default values or known values | `Create` | `world.Create<Health>()`; `world.Create(healthId, new Health(100))` | Creates an entity and initializes its component storage | Amortized storage work proportional to `S`; batches also scale with entity count | Capacity growth, registration validation and structural bookkeeping; batch forms avoid repeated call preparation |
| Add or remove registrations without typed values | Type-erased `Add` / `Remove` | `world.Add(entity, id)`; `world.Remove(in q, ids)` | Changes membership using IDs directly | Selection work plus structural work proportional to affected data | Data-component changes may move/copy rows. Tags update membership masks. A CLR type is unnecessary when only IDs are consumed |
| Add values or select a primary registration by type | Typed `Add` / primary `Remove` | `world.Add(entity, new Health(100))`; `world.Remove<Health>(entity)` | Supplies a value or selects a type's primary registration | Selection plus structural work | Use combined multi-component forms for one combined transition when changing several data components |
| Create or patch one entity with a runtime-defined component set | **`EntityData` + `World.Create/Add/Set/AddOrSet`** | `world.AddOrSet(entity, data)`; `Entity e = world.Create(data)` | Applies heterogeneous typed values and presence-only IDs together; registrations can share a CLR type | Warm application: `O(K + S)`, plus active observer costs | Construction and first use prepare storage and routes. Reuse the same data instance and component set to reuse world-owned plans; adding data components uses a combined structural transition |
| Copy the current state of one entity into reusable data | `EntityData.Add/Set/AddOrSet(World, Entity)` | `data.Clear(); data.AddOrSet(world, entity);` | Copies matching component values and presence into the container | Warm `Set`: `O(K + S)`; warm `Add`/`AddOrSet`: `O(U + T_c + S)` | Reuses typed readers and row routes; resolves the source once. Full capture reads its data rows and the chunk's active tags, checking tag membership for this entity. First use prepares routes; missing destination entries allocate storage |
| Read, edit or enumerate entries before applying them | `EntityData` accessors + `ComponentIds` | `data.GetRef<float>(healthId) = 75f`; `foreach (ComponentId id in data.ComponentIds) ...` | Works with entries in the container, including tags through the same typed accessors | Expected `O(1)` keyed access; enumeration and removal `O(K)` | Entry construction can allocate. References address stored values; present tags return `default(T)` or a shared dummy reference. `ComponentIds` borrows storage and must not survive modifications |
| Destroy one entity, a list or query matches | `Destroy` | `world.Destroy(entity)`; `world.Destroy(in q)` | Removes entities and their components | Depends on selection and data removed/moved; an explicit list may also require `O(L log L)` grouping/sorting | Dense query deletion can use chunk work; arbitrary lists and tag-filtered deletion require more selection work |
| Map a stable schema or config name to a runtime ID | Registration, schema lookup and aliases | `world.Components.TryGetComponentId(schemaId, out var id)` | Resolves the registration once for later use | Expected `O(1)` indexed lookup | Resolve configuration names/IDs during loading, then keep `ComponentId` rather than repeating string lookups in hot loops |
| Get a configured category of registrations | Component groups | `world.GetGroup(Groups.History)` | Returns IDs assigned to exactly one enum group value | Cached lookup: expected `O(1)`; rebuilding the member array after group mutation: `O(group size)` | Enumerating the returned IDs costs `O(group size)`. A flags value does not union several groups in `GetGroup`; group membership does not itself select entities |
| Build typed behavior from a CLR type known only through an ID | Component visitors | `world.Components.TryVisit(id, visitor)` | Dispatches once to a generic `Visit<TComponent>` | Dispatch per registration, plus visitor work | Build and retain typed handlers/operations here rather than dispatching a visitor for every entity |
| Declare component metadata at build time | Schema, alias and group attributes; generated catalog | `[ComponentSchema(10)]`; `[ComponentGroup(Groups.History)]` | Describes registrations and categories for generated metadata | Registration/catalog preparation, outside the entity loop | Optional metadata; ordinary components need no ECS attributes. See the grammar for catalog and interface-binding routes |
| Inspect/edit completely dynamic values in tooling | `World.TryRead` / `World.TryWrite` | `world.TryRead(entity, id, out object? value)` | Reads and writes dynamically selected component values | Lookup plus conversion/copying | Struct values can be boxed. Prefer typed APIs for hot simulation work |

## Choose EntityData for a component set on one entity

Use `EntityData` for configuration, serialization, entity snapshots and patches
whose component set is decided at runtime. It retains typed values without
boxing them into `object`; several IDs may share one CLR type.

| Intent | Call | Result |
| --- | --- | --- |
| Create from the set | `world.Create(data)` | New entity; at least one ID is required |
| Add only missing entries | `world.Add(entity, data)` | Number of components/tags added; existing values stay unchanged |
| Update only if the entire set exists | `world.Set(entity, data)` | `data.Count`, or zero with no values applied if any entry is missing |
| Ensure membership and apply values | `world.AddOrSet(entity, data)` | Adds missing entries and updates supplied values; returns `void` |
| Capture one entity | `data.Clear(); data.AddOrSet(world, entity);` | Replaces the container contents with shallow copies |
| Inspect the set | `data.ComponentIds`; `data.Get<T>(id)` | Borrowed ID span and typed values; present tags return their default value |

```csharp
var data = new EntityData();
data.Add(healthId, 100f);
data.Add(staminaId, 40f);
Entity entity = world.Create(data);

data.Set(healthId, 75f);
world.Set(entity, data); // Reuses preparation and applies current values
```

An ID without a value supplies membership: new data components get default
values, tags get presence, and existing values are preserved. The consuming
world resolves IDs and caches plans for this data instance and its encountered
archetypes. Replacing values reuses those plans; changing the ID set invalidates
them. Retain the container for repeated applications.

For one known component use direct access. For a fixed known set use generated
multi-component calls. For full-world snapshots prefer prepared typed row
traversals per registration; individual `EntityData` captures resolve each entity
and its entries separately. See [Entity data](API-GRAMMAR.md#entity-data).

## Choose the callback and registration form

These are independent choices within an iteration family; they do not require switching to zero arity.

| Requirement | Choose | Why |
| --- | --- | --- |
| One ordinary registration per component type | Primary-registration overload | Shortest typed call; the component types choose their primary IDs |
| Multiple registrations share one CLR type, or configuration chooses the registration | Typed callback with explicit `ComponentId`s | A runtime ID selects a typed row. **A dynamic registration is not an unknown CLR type** |
| Build IDs programmatically for a fixed component signature | Typed callback with `ReadOnlySpan<ComponentId>` | Keeps typed component parameters; IDs must match their number and types. Construction preserves the inputs for later invocation |
| Only read a component | `in T` or supported `ref readonly T`; `GetReadRef<T>` for individual access | Avoids declaring writes and generating change notifications for read-only work |
| Change a component | `ref T` / `GetRef<T>` | Declares writable access and participates in change tracking when observed |
| Keep a local copy | Value parameter / `Get<T>` | Copying is intentional; avoid copying large structs just to read them |
| Short local behavior | Lambda, preferably `static` when no capture is needed | Easy to read. An ordinary delegate can retain an indirect call; eligible intercepted lambdas can use a generated direct path |
| Reusable typed behavior or mutable processor state | Struct functor | A concrete generic call avoids boxing and permits specialization. Do not erase it to an interface just to invoke it |
| Pass/update external state without a closure | Caller-owned context overload | Explicit ownership; parallel forms accept read-only/value context, not shared mutable `ref` context |

## Choose grouping for recurring equality filters

Value grouping is an experimental storage policy: equal **whole component
values** share an archetype. The component remains ordinary per-entity data with
one `ComponentId` and `SchemaId`; changing that value changes the group. Multiple
grouped components split storage by their combinations of values.

| Need | Choose | Trade-off |
| --- | --- | --- |
| Repeated squad, team, owner or region selection with recurring keys | `Register<T>(schema, group: true)` + `WhereAll(value)` | Reads skip other groups; changing a key may move component rows |
| A range, distance, or a frequently changing mostly unique value | Ordinary registration + `Where` | Tests candidate values without fragmenting storage into groups |
| Added/modified/removed instances inside one group | Retained `ForEachChange<T>(in groupQuery, id, callback)` | Tracks the selected set and writes to the observed registration |
| Categories of registrations such as `History` or `Temp` | `[ComponentGroup(...)]` + `world.GetGroup(...)` | Returns component IDs; does not group or select entities |

Grouped types must be structs implementing `IEquatable<T>`; a record struct
already does. Register explicitly or use `[ComponentSchema(10, group: true)]`
with the generated catalog. `WhereAll(value)` requires a value,
`WhereAny(value)` adds to the shared OR alternatives, and `WhereNone(value)`
excludes that value, allowing entities without the component. For several
registrations of the same CLR type, pass the ID before the value.

### Observe health within one squad

This complete runtime-only example reports entry, writes and exit. Registering
`MemberOf` with grouping lets the query select squad 1 without a per-entity
predicate. Retain both the query and the change operation.

```csharp
using System;
using Delta.ECS;

using var world = new World();
world.Components.Register<MemberOf>(new SchemaId(10), group: true);
ComponentId healthId = world.Components.Register<Health>(new SchemaId(11));
Entity unit = world.Create(healthId, new Health(100));
world.Add(unit, new MemberOf(1));
Query squad = world.WhereAll(new MemberOf(1));

var changes = world.ForEachChange<Health>(in squad, healthId,
    static (Entity entity, ChangeKind kind, ref Health health) =>
        Console.WriteLine(kind == ChangeKind.Removed
            ? $"{entity.Index}: left the observed set"
            : $"{entity.Index}: {kind}, health={health.Value}"));

changes.Invoke();                             // Added, health=100
world.GetRef<Health>(unit).Value -= 25;
changes.Invoke();                             // Modified, health=75
world.GetRef<MemberOf>(unit) = new MemberOf(2);
changes.Invoke();                             // Removed; Health still exists
world.GetRef<MemberOf>(unit) = new MemberOf(1);
changes.Invoke();                             // Added, health=75
changes.Invoke();                             // No output

public readonly record struct MemberOf(int Squad);
public record struct Health(int Value);
```

Add ordinary membership filters, for example `.WhereNone<Paused>()`, to the
same query. The observed component must be present, even if omitted from the
query. A `Where` predicate view cannot be passed to `ForEachChange`.

Writable access to a grouped component marks its chunk dirty, even if its value
stays equal. The **next world data operation**, including `changes.Invoke()`,
resolves pending regrouping before using locations or query bindings. Movement
waits while an iteration is active: nested work sees that operation's existing
group placement. Do not reuse a borrowed component reference after another
operation can relocate it; reference lifetime is the caller's responsibility.

Stable reads visit matching chunks normally. Resolving dirty groups costs
`O(Cw + Nd × G)` plus relocation: world chunk-slot capacity `Cw`, entities in dirty
chunks `Nd`, and grouped rows `G`. Chunks grow from 16 slots up to 1024 rather
than allocating 1024 slots for every small group; they do not automatically
shrink. Distinct keys and value combinations retain metadata for the registry/world
lifetime, so an unbounded ID stream is still a poor grouping key.

## Understand change notifications

| Since this operation's last successful call | Notification |
| --- | --- |
| First invocation, or entry into its filtered set | `Added` |
| Writable access to its component while still included | `Modified`, even if the value is unchanged |
| Component removal, destruction, or leaving the filter | `Removed`; null component ref, original possibly stale entity |
| Only another filter component changes, but the entity remains included | None |
| Exit and reentry between invocations | Coalesced; final membership and writes to the tracked component determine the result |

`ForEachChange<T>` tracks **one registration**, including tags, across the world
or a query; it does not compare old/new values or observe every component in the
query. To observe the grouping value itself, track `memberOfId`; to maintain a
squad's health cache, track `healthId` with the squad query as above.

Events coalesce per entity generation: add then remove can cancel; remove then
add for an already observed instance becomes `Modified`. Reusing an entity index
keeps the old removal separate from the new addition. Never read a `Removed`
component ref. Writes through an operation's own callback do not notify itself
again. Build a change operation once: recreating it restarts initial reporting.

## When a visitor helps

Use `world.Components.Visit(id, visitor)` when an ID selects an unknown CLR type
and your code needs `T`. Use `IUnconstrainedComponentVisitor` for any type, or
`IComponentVisitor<TInterface>` after `BindInterface<T, TInterface>()` for custom
behavior. The constraint type is supplied automatically.

For a one-off command, the visitor can call `GetRef<T>` directly. For repeated
commands, prepare typed handlers once; for a full-world pass, prepare typed
operations per registration. Do not dispatch a visitor for each entity or cast
a struct component to its interface, which mutates a boxed copy. See the
[compact visitor example](API-EXAMPLES.md#visit-a-runtime-selected-component).
Runtime-selected open generic functors currently do not accept user-defined
interface/base-class constraints; use a closed constrained handler for that case.
An inline generated lambda mentioning a visitor method's open `T` is also
unsupported; use a typed functor or a closed handler.

## Preparation, reuse and lifetime

- Retain queries and operations; `Invoke()` sees current matches, including new chunks and groups. Rebuild when the selected group value/configuration changes.
- Use `var` locally, `IOperation` for a retained traversal, `IResultOperation<T>` for a result, and `IOperation<TState>` for caller-owned mutable state. Assigning a generated operation struct to an interface boxes it once.
- A functor avoids a per-entity delegate call; closures, lists, ordering and subscriptions can still allocate during preparation. Caching is not a promise of allocation-free construction.
- Use `in`/`GetReadRef` for reads. Writes cost both the writer and active observers; grouped writes also schedule regrouping.
- `EntityRef` is borrowed only for its callback. Retain an `Entity` handle instead. Apply structural changes after traversal or through a structural terminal.

See the [API grammar](API-GRAMMAR.md) for exact signatures and the
[API examples](API-EXAMPLES.md) for complete code.
