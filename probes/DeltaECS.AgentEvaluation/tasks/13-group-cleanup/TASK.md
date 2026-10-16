# Remove components belonging to configured groups

The supplied `Scenario` has a created world, registrations and entities. `GroupSetup.Configure` assigns registration metadata before your solution is constructed. Read `Fixture.cs` and `Groups.cs` for the model; implement only `Solution.cs` and preserve the supplied checks and settings.

On every `Execute()`, remove every component belonging to either `ProbeGroups.Temp` or `ProbeGroups.Transient` from every entity. Obtain the registrations through the public group API. The metadata is fixed after construction, but component membership and entities may change. Components may belong to both groups; treat the selection as a union. This is cleanup of component membership, not destruction of entities or modification of their values.

Treat grouped registrations' CLR types as unknown. Some registrations share a CLR type, and one is a tag. Do not hardcode IDs, type names, schema IDs or the current group members; do not use reflection or per-entity component getters. Preserve all registrations outside the selected groups, including `History`, the primary float registration and `Noise`. Do not enumerate `Scenario.Units` as a substitute for processing the current world.

Construct the solution once and reuse it. Prefer the appropriate API family and avoid repeated preparation inside `Execute`. Explain the chosen API, complexity, preparation and overhead in `CHOICE.md`. Preserve the first implementation in `FIRST-Solution.cs.txt` before compiler-driven corrections. Record attempts and run `dotnet run --project Consumer.csproj -c Release`. Do not edit the checker to make the solution pass.
