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

`common/Fixture.cs` defines the shared data model. Every task has a pristine `Solution.cs` stub and a `Verify.cs` executable checker. `evaluation/rubric.json` contains evaluator-only criteria; it must not be supplied to the solving agents.

The snapshot task deliberately requests a complete snapshot, including unchanged values. A change-only cache is not an equivalent answer. Its sink owns output boxing; that output contract is separate from the efficiency of ECS traversal.

## Prepare independent sessions

Build the runtime and generator through the repository's normal development workflow first. The preparer copies their existing Release binaries; it does not build or publish packages.

From the repository root:

```sh
python3 probes/DeltaECS.AgentEvaluation/prepare.py \
    --output /tmp/deltaecs-agent-evaluation/sessions
```

Use a new output directory for each run. The script creates three consumer workspaces with four tasks each and a fourth with the group task. Use `--session d` to prepare only the group session. It copies only:

- the library README, API-selection guide, grammar and cookbook;
- the fixture, task statements, checkers and solution stubs;
- runtime and generator binaries;
- a standalone consumer project and an offline NuGet configuration.

No repository `AGENTS.md`, implementation sources, private rubric or reference solutions are copied. `manifest.json` records the source commit, document/binary hashes, task assignment and requested model settings. A dirty source tree or an older binary can differ from the commit: the hashes identify the actual inputs, while the commit alone does not prove binary provenance.

For an independent retest of one task after a documentation change:

```sh
python3 probes/DeltaECS.AgentEvaluation/prepare.py \
    --session c --task 09-world-snapshot \
    --output /tmp/deltaecs-agent-evaluation/snapshot-retest
```

Launch each session with `gpt-6-luna`, reasoning effort `high`, and no inherited conversation. Give the agent only its session path and this instruction:

> Work only in this consumer workspace. Read SESSION.md and solve every assigned task. Use only the supplied public documentation and consumer files. Do not read repository sources, general AGENTS.md, memory, other sessions, evaluator criteria, decompiled assemblies or generated implementation files. Preserve the first solution before compiler-driven corrections, record attempts, explain your API choices and run the provided checks. Do not ask other agents for guidance or commit changes.

The independent context and file whitelist are procedural isolation. The collaboration tool does not enforce a separate operating-system filesystem sandbox. Do not report a stronger isolation guarantee without an actual sandbox.

## Preserve and evaluate the answers

Agents edit only `Solution.cs` and create `FIRST-Solution.cs.txt`, `CHOICE.md` and `ATTEMPTS.md`. The first answer is retained even when compilation requires corrections. Keep the supplied fixture and checkers unchanged.

After all sessions finish:

```sh
python3 probes/DeltaECS.AgentEvaluation/evaluate.py \
    --workspace /tmp/deltaecs-agent-evaluation/sessions \
    --results artifacts/docs-evaluation/my-run
```

The evaluator verifies immutable input hashes, archives solution evidence, reruns every consumer check and saves complete output and `correctness.json`. It flags a missing first-answer snapshot or one that is still the starter stub; these do not establish first-attempt correctness. The agents restore/build their projects first; the evaluator uses `--no-restore`. Build output and generated code stay in the scratch workspaces.

Review each answer against the private rubric separately from its pass/fail result:

1. **Correctness:** current values and membership, repeat invocation, removals and entity generations.
2. **API choice:** the relevant family and arity, typed access when types are known, visitors when types are unknown, retained change observation when changes are required.
3. **Access intent:** read-only parameters for reads; mutable references only for writes.
4. **Work and lifetime:** preparation outside repeated invocation, no repeated type discovery or avoidable point getters, safe entity/view lifetimes.
5. **Documentation friction:** invented signatures, missing examples, compiler corrections and ambiguity explicitly reported by the agent.

Record first-attempt compilation separately from final correctness. Passing a tiny fixture is not evidence of optimal performance. A correct API choice may still have avoidable overhead, and more than one API can be appropriate for the same task.

## Performance comparisons and documentation changes

When the cost difference is clear from the public execution model, explain it without fabricating a timing. When two valid solutions have comparable work and their real cost is uncertain, compare them on the same larger fixture with BenchmarkDotNet. Validate equivalent results before timing; create worlds and reusable operations outside measurement, pass workload sizes as application arguments, and retain the environment and complete results.

Freeze the first-run evidence before improving documentation. Re-run changed tasks in fresh sessions; do not give a previously coached agent the same task and call that an independent result. A single batch of thirteen tasks tests coverage, not statistical reliability or causation. A documentation A/B claim requires separate uncoached sessions for both document sets.

Run reports belong in `evaluation/`; raw answers, hashes and compiler/benchmark output belong in `artifacts/docs-evaluation/`. Keep the task templates free of solved answers so the suite can be repeated.

The [first evaluation report](evaluation/2026-10-16.md) records the thirteen-task baseline, two fresh retests, documentation corrections and the limits of the evidence.

Private holdout checkers in `evaluation/holdouts/` test a player away from the origin. After freezing the original answers and results, copy a consumer workspace to a separate scratch directory, replace only its matching `Verify.cs` with the holdout checker and run `dotnet run --project Consumer.csproj -c Release`. Keep that output separately; the normal evaluator must continue rejecting modifications to its original checker inputs.

The retained-handler comparison can be repeated explicitly:

```sh
dotnet run --project probes/DeltaECS.AgentEvaluation/evaluation/dispatch-benchmark/DispatchBenchmarks.csproj -c Release -- \
    --entities 131072 --filter '*' --iterationTime 200 --warmupCount 10 --iterationCount 20 \
    --artifacts artifacts/docs-evaluation/dispatch-comparison
```

This benchmark needs a normal BenchmarkDotNet package restore. It measures repeated point commands after preparation, with two registrations and no change subscribers. It does not measure world creation, full snapshots or group cleanup. Consumer task projects stay separate from the solution because their checked-in solutions intentionally remain unimplemented.
