# Reset one dynamically selected component

The supplied `Scenario` has a created world, registrations and entities. Read `Fixture.cs` for the data model. Implement only `Solution.cs`; preserve the fixture, verification code and project settings.

At construction receive the registrations in `Scenario.ResettableIds`; their CLR types must be treated as unknown to this solution. The fixture already bound their `IResettable` interface routes. Build reusable behavior once. `Execute(Entity target, ComponentId id)` resets just the selected component through `IResettable.Reset`, in place, returning false for an unconfigured ID, a missing component or an invalid entity. It must work with future registrations implementing the interface, without new branches for `Armor`, `Energy`, CLR type names, reflection or `dynamic`. This is a repeated single-entity operation, not a world-wide scan.

Construct `Solution` once and reuse it. Entities and component values can change after construction. Prefer the most appropriate public API, avoid repeated preparation in `Execute`, and do not hardcode fixture indices or enumerate `Scenario.Units` as a substitute for querying current world state. The checker may use that array to assert results; your solution must work for new matching entities too. Preserve unrelated components, including `Noise`.

Use only the supplied public documentation and consumer files. Explain your API choice, complexity, preparation cost and possible overhead in `CHOICE.md`. Record the first implementation before any compiler-driven corrections in `FIRST-Solution.cs.txt`. Run `dotnet run --project Consumer.csproj -c Release` and report its result. Do not edit the checks to make a solution pass.
