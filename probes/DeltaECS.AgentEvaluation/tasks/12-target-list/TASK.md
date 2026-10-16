# Apply a command to an existing target list

The supplied `Scenario` has a created world, registrations and entities. Read `Fixture.cs` for the data model. Implement only `Solution.cs`; preserve the fixture, verification code and project settings.

The constructor receives a fixed immutable entity array owned by a command. Reduce health by 3 for targets with `Health` and without `Paused`. Leave all other world entities untouched. Repeated calls apply the command again to still-matching targets. Targets can include entities without health. Use the supplied list directly as the selection rather than scanning the whole world and checking whether each handle is in the array.

Construct `Solution` once and reuse it. Entities and component values can change after construction. Prefer the most appropriate public API, avoid repeated preparation in `Execute`, and do not hardcode fixture indices or enumerate `Scenario.Units` as a substitute for querying current world state. The checker may use that array to assert results; your solution must work for new matching entities too. Preserve unrelated components, including `Noise`.

Use only the supplied public documentation and consumer files. Explain your API choice, complexity, preparation cost and possible overhead in `CHOICE.md`. Record the first implementation before any compiler-driven corrections in `FIRST-Solution.cs.txt`. Run `dotnet run --project Consumer.csproj -c Release` and report its result. Do not edit the checks to make a solution pass.
