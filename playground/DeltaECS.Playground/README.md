# DeltaECS Playground

Small executable for trying the public query and iteration APIs without
touching the benchmark projects.

Run it from the repository root:

```bash
dotnet run --project playground/DeltaECS.Playground/DeltaECS.Playground.csproj -c Release
```

The sample demonstrates two compact entry points: a generated component
callback and an ordered, query-filtered entity sequence.

Add temporary components, queries and experiments to `Program.cs`. Keep
performance measurements in the benchmark projects instead.
