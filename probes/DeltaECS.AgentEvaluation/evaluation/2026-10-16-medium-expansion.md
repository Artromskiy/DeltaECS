# Documentation-only API selection: expanded tasks, Luna medium

The suite now contains 21 tasks, including eight new compositions in which input selection, ordering, mutation phases or component-type knowledge affect the API choice. Runtime and generator implementation were not changed. All solving sessions used `gpt-6-luna`, reasoning effort `medium`, fresh context and public consumer inputs only.

This run found both documentation friction and inadequate original checks. It does **not** establish that an agent always chooses the optimal API. Executable correctness and manual API/cost review are separate results.

## Inputs and isolation

The initial source revision was `13df9a1b88d7883df912014e5b47f3a0fa5165fe`. Documents and task templates were dirty while expanded, so each prepared manifest records SHA-256 hashes of the actual supplied files; the commit alone is not their provenance. No changes to ECS runtime code were made during this evaluation.

Binary SHA-256 hashes supplied to consumers:

| Input | SHA-256 |
| --- | --- |
| DeltaECS.dll | `911b490bc3d4f83df79b6de37467f1e7b81311869f808eebcc6bd8a0e8f7d759` |
| DeltaECS.Generators.dll | `c9b39fc119d61d4f812a604f255125ace996873be78eb016401c4c62f7f39ab6` |

Each consumer receives the public README, API selection guide, grammar, cookbook, fixture, task statement, immutable checker and offline project. Implementation sources, PDBs, private rubric and reference answers are excluded. Authoring helpers were separate from solving agents; their private reference solutions are feasibility evidence, not independent evaluation answers.

Every observed solver file/shell operation used the Bubblewrap launcher. Its filesystem exposes one consumer session at `/work`, system tools and the SDK. Source trees, other sessions, host process namespace, credentials, Docker sockets and networking are absent. Supplied inputs are read-only. Regression probes verify those boundaries. The model's collaboration tool surface still shares the host: this is enforced command isolation plus an instruction forbidding direct tools, **not** a claim that every hypothetical model tool is independently sandboxed. Binary decompilation, reflection and generated-source inspection are forbidden by the session contract; the compiler naturally consumes the provided binaries.

External command audits capture the first changed solution observed at a command boundary. This is not an unobserved edit history or proof of one compiler attempt. First and final sources are rebuilt separately, and original evidence is never overwritten.

## Initial independent sessions

| Session | Tasks | Final base checks | First observed source checks |
| --- | --- | --- | --- |
| a | 01–04 | 4/4 | 0/4 |
| b | 05–07, 11 | 4/4 | 4/4 |
| c | 08–10, 12 | 4/4 | 1/4 |
| d | 13–15 | 3/3 | 0/3 |
| e | 16–18 | 2/3 | 1/3 |
| f | 19–21 | 3/3 | 0/3 |
| Total | 21 | 20/21 | 6/21 |

The base checks initially missed an origin assumption in 02 and 03. Both frozen final answers failed separate nonzero-player holdouts. Accounting for those known failures leaves **18/21 behaviorally passing answers**, before considering API-efficiency or task-contract violations. Those holdouts have now been added to the normal task templates. Existing frozen evidence keeps its original checks.

## New task review

| Task | Order/API property | Initial result and review |
| --- | --- | --- |
| 14 Debuffs | Prepare each configured registration independently; mutate stored structs through constrained dispatch | Pass after compiler corrections. Per-instance point-lookup fallback is supported; open-interface runtime functors are not. |
| 15 List minimum | Filter supplied handles before minimum; moving origin; stable tie | Pass. Linear external-list reduction, current values, stale/missing-component handling. |
| 16 Damage/cleanup | Write first, then select updated dead entities and destroy through a structural terminal | Pass; appropriate two-stage API. |
| 17 Observed cleanup | Retained change observation, then event-fed membership removal | Pass, but `ToArray()` allocated per nonempty call. Public span batch alternative was separately compiled and checked. |
| 18 Configured transfer | Explicit same-CLR registrations, descending key, stable tie, fresh budget/cap | Failed compilation after several attempted terminal compositions. Supported retained typed ordered alternative was privately compiled and passed. |
| 19 Expiration | Constrained struct mutation, post-tick expiration, safe removal after traversal | Pass, but separate removal per expired instance missed available per-registration batching. |
| 20 Snapshot/reset | All registrations fully snapshotted before any reset; live population | Base pass, but avoidable snapshot point reads and an overstrict original getter ban. Template was revised to permit necessary constrained mutation fallback and to enforce the global phase boundary. |
| 21 Membership transition | Typed write stage, then selection from updated values | Pass; appropriate staged API. |

Task 18's strengthened checker exhausts budget partway through the second tied row, making tie order observable. Task 20 now uses task-local reset hooks: the first reset fails unless the complete snapshot already exists. An interleaved snapshot/reset negative control fails as expected. Task 14 uses struct debuffs so boxed-copy mutation cannot accidentally pass. These checker revisions are part of the experiment; improved results cannot all be attributed to documentation changes.

## Documentation corrections and supported limits

The selection guide now explains input-set selection, registration/type knowledge, eligibility, minimum versus full ordered traversal, and mutation barriers in execution order. It covers nonzero/fresh origins, copied comparer state, per-call budgets, event-fed list capture, span batching, multiple interface implementations and direct-row versus point-lookup costs.

Compiler-checked limits matter when grading:

- Runtime-selected generic functors with user interface constraints emit `DECSGEN008`. Inline generated typed lambdas referring to a visitor's open `T` can emit `CS0246`. A cold constrained visitor can prepare a closed handler whose retained entity traversal calls an ordinary constrained `GetRef<T>` helper. This fallback incurs a point lookup per instance.
- Unconstrained runtime generic read-only snapshot functors are supported. Snapshotting should use that typed path; the constrained mutation fallback does not justify point getters for every read.
- Known-CLR, explicit-ID ordered transfer has a supported retained typed terminal using an explicit `OrderedQuery` receiver. The attempted explicit-ID predicate composition did not generate successfully. Sorting all candidates and checking eligibility before transfer is a supported fallback with `O(N log N)` sorting, not a claim of filter-before-sort capability.
- Filtered ordered iteration with typed `IComponentComparer` structs was separately compiled for the damage-budget task, including the nonzero-player checker. Repeated `First()` selection can be correct but costs `O(N*K)` for K targets and is not an equivalent efficient choice.

## Fresh retests

Retests use new uncoached contexts. Different documentation snapshots and, for task 20, revised fixtures are separately hashed in their manifests. This is a targeted diagnostic run, not a randomized A/B estimate of a model success rate.

| Fresh session | Tasks | Final / first-observed passing | API review |
| --- | --- | --- | --- |
| retest-a | 02, 03 | 2/2 / 0/2 | Nonzero origin handled. 02 uses filtered minimum correctly. 03 falls back to repeated minima (`O(N*K)`), so fails the efficiency criterion. |
| retest-d | 14 | 1/1 / 0/1 | Correct supported constrained handler; first compile failed from an unnecessary generic argument on a helper. |
| retest-e | 16, 17, 18 | 3/3 / 1/3 | Correct staged cleanup; 17 uses a reusable list and immediate span batch; 18 retains explicit-ID typed ordered transfer and discloses sort-all fallback. |
| retest-f | 19, 20 | 2/2 / 1/2 | Batched expiration removal; typed unconstrained snapshot followed by constrained reset only after the global barrier. |
| retest-a2 | 03 | 1/1 / 0/1 | One retained typed ordered traversal replaces repeated minima. Still sorts ineligible rows, whereas the separately checked filtered composition sorts only eligible rows. |

All **9 retest answers pass** their current executable checks (eight distinct tasks; 03 tested twice). Only **2/9 first-observed sources** pass without corrections. Correct families and better work/allocation choices are visible in 17–20, but the damage-budget choice remains suboptimal relative to the available filtered traversal. This is not evidence of universal optimal selection or reliably correct first compilation.

In retest-e the agent recorded attempts in one session-level `ATTEMPTS.md`. The original evaluation metadata reports task-local attempt files absent; the session log is archived as `answers/e/SESSION-ATTEMPTS.md`. The evaluator now preserves and labels that fallback, and new session instructions request per-task logs explicitly.

Remaining concrete follow-up: improve generated API composition/discoverability around captured filtered ordered callbacks and comparer declaration forms; add further independent repetitions before assigning a success rate. Do not treat a compiler-driven sort-all fallback as the fastest available API when the typed filtered traversal is known to compile.

## Verification and retained evidence

The harness has six regression tests covering immutable inputs, isolated commands, external first-answer preservation, rejected unexpected compiler inputs, failed-consumer status and separate first/final rebuilds. All six pass. The repository layout gate passes; Release solution build has zero warnings/errors; core tests pass 260/260. Shared `/workspace/eng/format.sh` and `code-metrics.sh` wrappers are absent in this workspace, so those checks could not run. `git diff --check` passes.

Raw manifests, input snapshots, first/final answers, full compiler output, manual-choice notes and correctness JSON are under `artifacts/docs-evaluation/20261016-medium-expansion/`. Command audits are retained there under `command-audits/`. Private feasibility sources and the global-phase negative control are under `artifacts/docs-evaluation/task-template-validation/`; they were never supplied to solvers. Template `Solution.cs` files remain unimplemented.

No new runtime performance numbers are claimed. This evaluation checks correctness, documented API availability and visible work/allocation differences. A small passing fixture cannot prove global optimality, and the remaining failures below are documentation/generator follow-up work rather than evidence that the goal has been universally achieved.
