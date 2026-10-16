# Generated registrations in consumer templates

Prepared consumers now default to `--registrations generated`. Primary shared
and task-local component registrations use `GeneratedComponentCatalog` with
explicit `[ComponentSchema]` declarations. Alternate registrations of one CLR
type and configured `float` registrations remain manual. Interface bindings
remain explicit because the generated catalog does not establish those bindings.

`--registrations manual` retains the prior registration path. The project pins
the mode and the manifest records it; schema declarations are supplied public
consumer inputs and protected by the existing input hashing/sandbox rules.

Validation uses both modes across all 21 tasks, with previously checked answers
and private feasibility answers copied into scratch consumers. It checks
compilation and behavior, not independent model selection. Sources in the task
suite remain unsolved stubs. In particular, this does not establish that the
registration change fixes the earlier generated-overload failures.

Both modes pass all 21 consumer checks: **42/42** total. The six harness
regression tests pass, the layout gate passes and `git diff --check` is clean.

Raw mode-specific manifests, full consumer logs and `checks.json` are under
`artifacts/docs-evaluation/generated-registration-templates/`. Runtime and
generator implementation are unchanged. The shared format/metrics wrappers
remain absent in this workspace.
