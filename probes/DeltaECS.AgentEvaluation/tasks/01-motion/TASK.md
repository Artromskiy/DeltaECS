# Move active entities

The supplied `Scenario` has a created world, registrations and entities. Read `Fixture.cs` for the data model. Implement only `Solution.cs`; preserve the fixture, verification code and project settings.

Each entity with `Position` and `Velocity` must advance by its velocity on every call. Skip entities carrying `Paused`. The handler needs no entity identity. Use a unit time step. The player has no velocity and must remain unchanged.

Construct `Solution` once and reuse it. Entities and component values can change after construction. Prefer the most appropriate public API, avoid repeated preparation in `Execute`, and do not hardcode fixture indices or enumerate `Scenario.Units` as a substitute for querying current world state. The checker may use that array to assert results; your solution must work for new matching entities too. Preserve unrelated components, including `Noise`.

Use only the supplied public documentation and consumer files. Explain your API choice, complexity, preparation cost and possible overhead in `CHOICE.md`. Record the first implementation before any compiler-driven corrections in `FIRST-Solution.cs.txt`. Run `dotnet run --project Consumer.csproj -c Release` and report its result. Do not edit the checks to make a solution pass.
