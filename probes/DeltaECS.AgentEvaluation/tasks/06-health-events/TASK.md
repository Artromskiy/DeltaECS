# Report health changes between calls

The supplied `Scenario` has a created world, registrations and entities. Read `Fixture.cs` for the data model. Implement only `Solution.cs`; preserve the fixture, verification code and project settings.

Maintain one observer for the `HealthId` registration. Each `Execute()` clears `Changes` and reports `(Entity, ChangeKind, current health)` since its previous successful call. Initially report all existing health instances as added. Later report writable access, membership changes and destruction; removals have a null `Value`. Coalesce intermediate add/remove transitions, preserve generations, and report nothing on an unchanged repeat. This asks for ECS write/membership semantics, not value-equality differences. Do not retain copied health values to detect differences.

Construct `Solution` once and reuse it. Entities and component values can change after construction. Prefer the most appropriate public API, avoid repeated preparation in `Execute`, and do not hardcode fixture indices or enumerate `Scenario.Units` as a substitute for querying current world state. The checker may use that array to assert results; your solution must work for new matching entities too. Preserve unrelated components, including `Noise`.

Use only the supplied public documentation and consumer files. Explain your API choice, complexity, preparation cost and possible overhead in `CHOICE.md`. Record the first implementation before any compiler-driven corrections in `FIRST-Solution.cs.txt`. Run `dotnet run --project Consumer.csproj -c Release` and report its result. Do not edit the checks to make a solution pass.
