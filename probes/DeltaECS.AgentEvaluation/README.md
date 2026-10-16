# DeltaECS agent evaluation

Executable consumer tasks for checking whether the public documentation helps an independent agent choose an appropriate DeltaECS API. This is an evaluation suite, not a source test project or a collection of recommended solutions.

The tasks supply a created world, registered components, entities and deterministic result checks. Each solution is constructed once and invoked repeatedly; later checks change values, membership and the entity population. A solution cannot pass by processing only the initial fixture array.

## Task catalogue

| Task | Required result |
| --- | --- |
| [01 — Motion](tasks/01-motion/TASK.md) | Move active entities using their current position and velocity |
| [02 — Nearest](tasks/02-nearest/TASK.md) | Find one living, unpaused enemy in range, with a stable distance tie-break |
| [03 — Damage budget](tasks/03-budget/TASK.md) | Spend a limited damage budget on enemies in distance order |
| [04 — Entity output](tasks/04-entity-output/TASK.md) | Update health and retain the affected entity handles |
| [05 — Configured registrations](tasks/05-configured-rows/TASK.md) | Read and write two registrations sharing the same CLR type |
| [06 — Health events](tasks/06-health-events/TASK.md) | Report additions, writes and removals between calls, including index reuse |
| [07 — Tag events](tasks/07-tag-events/TASK.md) | Track tag membership and writable access with the same event semantics |
| [08 — Runtime dispatch](tasks/08-runtime-dispatch/TASK.md) | Apply constrained behavior to one registration whose CLR type is unknown |
| [09 — World snapshot](tasks/09-world-snapshot/TASK.md) | Save every current instance of a configured set of registrations on each call |
| [10 — Structural changes](tasks/10-structural/TASK.md) | Destroy value-filtered entities safely |
| [11 — Entity-only selection](tasks/11-entity-only/TASK.md) | Collect handles using membership filters alone |
| [12 — Existing target list](tasks/12-target-list/TASK.md) | Update only an externally selected list of entities |
| [13 — Group cleanup](tasks/13-group-cleanup/TASK.md) | Obtain two configured groups and remove their registrations through type-erased batch operations |
| [14 — Debuff ticks](tasks/14-debuff-ticks/TASK.md) | Tick all configured interface implementations, including several struct debuffs per entity and alternate registrations |
| [15 — List minimum](tasks/15-filtered-nearest/TASK.md) | Filter an external candidate list before reducing to a minimum relative to a moving player |
| [16 — Damage and cleanup](tasks/16-damage-cleanup/TASK.md) | Complete health writes before selecting and destroying defeated enemies |
| [17 — Observed cleanup](tasks/17-observed-pipeline/TASK.md) | Consume retained health notifications, then apply event-fed membership cleanup outside callbacks |
| [18 — Configured transfer](tasks/18-configured-transfer/TASK.md) | Select configured float rows, order eligible targets and spend a fresh capped transfer budget |
| [19 — Expiring effects](tasks/19-expiring-effects/TASK.md) | Tick unknown struct effects and remove expired registrations safely after traversal |
| [20 — Snapshot then reset](tasks/20-snapshot-reset/TASK.md) | Complete a full multi-registration snapshot before any constrained reset of marked instances |
| [21 — Membership transition](tasks/21-membership-transition/TASK.md) | Update health, then change memberships selected from the updated values |

`common/Fixture.cs` defines the shared data model. Every task has a pristine `Solution.cs` stub and a `Verify.cs` executable checker. `evaluation/rubric.json` contains evaluator-only criteria; it must not be supplied to the solving agents.

The snapshot task deliberately requests a complete snapshot, including unchanged values. A change-only cache is not an equivalent answer. Its sink owns output boxing; that output contract is separate from the efficiency of ECS traversal.

## Prepare independent sessions

Build the runtime and generator through the repository's normal development workflow first. The preparer copies their existing Release binaries; it does not build or publish packages.

From the repository root:

```sh
python3 probes/DeltaECS.AgentEvaluation/prepare.py \
    --output /tmp/deltaecs-agent-evaluation/sessions
```

Use a new output directory for each run. The script creates six consumer workspaces: three with four tasks each, then three with three tasks each. Use `--session d --task 13-group-cleanup` to prepare only the group task. It copies only:

- the library README, API-selection guide, grammar and cookbook;
- the fixture, registration schema declarations, task statements, checkers and solution stubs;
- runtime and generator binaries;
- a standalone consumer project and an offline NuGet configuration.

The default `--registrations generated` mode uses `[ComponentSchema]` partial
component declarations and registers primary components through
`GeneratedComponentCatalog.GetRegistration<T>()`. This applies to shared
components and task-local debuff/effect/reset types. Alternative registrations
of the same CLR type and the three configured `float` registrations remain
explicit; interface binding remains explicit in both modes. The schema files
are supplied consumer declarations, not generated implementation files.

Use `--registrations manual` in a separate output directory for the original
registration path. The selected mode is pinned in every prepared project and
recorded as `registration_mode` in the manifest. Task contracts, solution stubs
and checkers are otherwise shared; record separate agent runs before claiming
that generated registration changes API-selection or compilation success.
The [template validation report](evaluation/2026-10-16-generated-registrations.md)
records the initial 42 consumer checks across both modes.

No repository `AGENTS.md`, implementation sources, private rubric or reference solutions are copied. `manifest.json` records the source commit, document/binary hashes, task assignment and requested model settings. A dirty source tree or an older binary can differ from the commit: the hashes identify the actual inputs, while the commit alone does not prove binary provenance.

For an independent retest of one task after a documentation change:

```sh
python3 probes/DeltaECS.AgentEvaluation/prepare.py \
    --session c --task 09-world-snapshot \
    --output /tmp/deltaecs-agent-evaluation/snapshot-retest
```

Launch each session with `gpt-6-luna`, reasoning effort `medium`, and no inherited conversation. These are also the preparer's manifest defaults; override `--model`/`--reasoning-effort` explicitly for a different experiment. Give the agent only its assigned sandbox command and this instruction:

> Execute every read, write and build through the supplied sandbox launcher. Do not use direct file tools or host shell commands. Inside the sandbox, read SESSION.md and solve every assigned task using only its public documentation and consumer files. Do not read repository sources, general AGENTS.md, memory, other sessions, evaluator criteria, decompiled assemblies or generated implementation files. Preserve the first solution before compiler-driven corrections, record attempts, explain your API choices and run the provided checks. Write and compile in separate commands. Do not ask other agents for guidance or commit changes.

`prepare.py` alone provides a source-free copy, not access isolation. On Linux,
use the mandatory command launcher (requires Bubblewrap):

```sh
python3 probes/DeltaECS.AgentEvaluation/sandbox.py \
    --workspace /tmp/deltaecs-agent-evaluation/sessions --session a \
    --audit artifacts/docs-evaluation/my-run-command-audit \
    --dotnet-root /absolute/path/to/dotnet-sdk -- \
    bash --noprofile --norc -c 'cat SESSION.md; dotnet --version'
```

Give solving agents a copy of the launcher outside the repository, not the
repository path. Inside it the session appears at `/work`; only that session,
system tools and the .NET SDK are mounted. Supplied documents, DLLs, fixtures,
projects and checkers are read-only. Network, PID and filesystem namespaces
hide host source trees, `.git`, other sessions, host processes and Docker
sockets. Environment credentials are not inherited. There is no unsandboxed
fallback. Commands and first non-stub solution snapshots are retained outside
the mounted session, where consumer commands cannot overwrite them.

The collaboration tool itself still exposes a shared host: the launcher
enforces isolation for commands executed through it, not for hypothetical
direct tool calls outside it. Restrict agents to the launcher, retain its
audit, and state this boundary in reports. Do not claim a separate sandbox for
the model's complete tool surface. The negative isolation and evidence checks
can be repeated without running any model:

```sh
python3 -m unittest discover -s probes/DeltaECS.AgentEvaluation -p test_harness.py -v
```

## Preserve and evaluate the answers

Agents edit only `Solution.cs` and create `FIRST-Solution.cs.txt`, `CHOICE.md` and `ATTEMPTS.md`. The first answer is retained even when compilation requires corrections. Keep the supplied fixture and checkers unchanged.

After all sessions finish:

```sh
python3 probes/DeltaECS.AgentEvaluation/evaluate.py \
    --workspace /tmp/deltaecs-agent-evaluation/sessions \
    --audit artifacts/docs-evaluation/my-run-command-audit \
    --sandbox --dotnet-root /absolute/path/to/dotnet-sdk \
    --results artifacts/docs-evaluation/my-run
```

The evaluator verifies immutable input hashes, rejects unexpected compiler
inputs, archives supplied inputs and answer evidence, reruns every consumer
check and saves complete output and `correctness.json`. A failing consumer
makes the evaluator return a failing exit status. First answers are rebuilt
in separate scratch copies; they never replace final solutions. External
first snapshots take precedence over agent-written evidence. Their precise
meaning is the first non-stub source observed at a command boundary, not an
unobserved edit history or proof of a single compiler attempt. Missing or
starter-stub snapshots do not establish first-attempt correctness. Build
output and generated code stay in scratch workspaces.

Review each answer against the private rubric separately from its pass/fail result:

1. **Correctness:** current values and membership, repeat invocation, removals and entity generations.
2. **API choice:** the relevant family and arity, typed access when types are known, visitors when types are unknown, retained change observation when changes are required.
3. **Access intent:** read-only parameters for reads; mutable references only for writes.
4. **Work and lifetime:** preparation outside repeated invocation, no repeated type discovery or avoidable point getters, safe entity/view lifetimes.
5. **Documentation friction:** invented signatures, missing examples, compiler corrections and ambiguity explicitly reported by the agent.

Record first-attempt compilation separately from final correctness. Passing a tiny fixture is not evidence of optimal performance. A correct API choice may still have avoidable overhead, and more than one API can be appropriate for the same task.

## Performance comparisons and documentation changes

When the cost difference is clear from the public execution model, explain it without fabricating a timing. When two valid solutions have comparable work and their real cost is uncertain, compare them on the same larger fixture with BenchmarkDotNet. Validate equivalent results before timing; create worlds and reusable operations outside measurement, pass workload sizes as application arguments, and retain the environment and complete results.

Freeze the first-run evidence before improving documentation. Re-run changed tasks in fresh sessions; do not give a previously coached agent the same task and call that an independent result. A single batch of tasks tests coverage, not statistical reliability or causation. A documentation A/B claim requires separate uncoached sessions for both document sets.

Check API selection independently of executable correctness. A small fixture
can pass despite a full sort instead of a minimum, repeated point getters,
per-call visitor dispatch, temporary arrays or fixture-specific branches.
Conversely, do not demand an API composition the current generator cannot
compile. Record those capability limits, validate a supported alternative,
and distinguish correctness, API family, avoidable overhead, and compilation
friction. Ordering checks must make order observable: exhaust a budget inside
a tied target, move the reference point, or assert a global phase barrier at
the first reset. Passing checks whose outputs are order-independent proves
no ordering property.

Run reports belong in `evaluation/`; raw answers, hashes and compiler/benchmark output belong in `artifacts/docs-evaluation/`. Keep the task templates free of solved answers so the suite can be repeated.

The [first evaluation report](evaluation/2026-10-16.md) records the thirteen-task baseline, two fresh retests, documentation corrections and the limits of the evidence.

The [expanded Luna-medium evaluation](evaluation/2026-10-16-medium-expansion.md) records the 21-task run, stronger order-sensitive checks, fresh retests and remaining API-selection failures.

Private holdout checkers in `evaluation/holdouts/` test a player away from the origin. After freezing the original answers and results, copy a consumer workspace to a separate scratch directory, replace only its matching `Verify.cs` with the holdout checker and run `dotnet run --project Consumer.csproj -c Release`. Keep that output separately; the normal evaluator must continue rejecting modifications to its original checker inputs.

The retained-handler comparison can be repeated explicitly:

```sh
dotnet run --project probes/DeltaECS.AgentEvaluation/evaluation/dispatch-benchmark/DispatchBenchmarks.csproj -c Release -- \
    --entities 131072 --filter '*' --iterationTime 200 --warmupCount 10 --iterationCount 20 \
    --artifacts artifacts/docs-evaluation/dispatch-comparison
```

This benchmark needs a normal BenchmarkDotNet package restore. It measures repeated point commands after preparation, with two registrations and no change subscribers. It does not measure world creation, full snapshots or group cleanup. Consumer task projects stay separate from the solution because their checked-in solutions intentionally remain unimplemented.
