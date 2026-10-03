# DeltaECS.Generators

`DeltaECS.Generators` is a build-time Roslyn analyzer/source generator for
consumer callback shapes and component registration. It is packaged as an
analyzer and has no runtime dependency on the generator assembly.

```xml
<PackageReference Include="DeltaECS" Version="*" />
<PackageReference Include="DeltaECS.Generators" Version="*"
                  PrivateAssets="all" />
```

The package places its analyzer assembly under `analyzers/dotnet/cs`. It
generates consumer-side `ForEach`/`ForEachEntity` callback forms, query-wide
`Where` predicate terminals, generic primary-component structural façades for
`Create`/`Add`/`Remove` (including multi-value `Add` calls), and typed `World`
query factories on demand. Runtime
`ComponentId` query factories are provided by `DeltaECS`; storage and runtime
execution remain there as well.

```csharp
Query combatants = world
    .WhereAll<Position, Health, Human>()
    .WhereNone<Dead, Escaped>()
    .WhereAny<Armed, Berserk>();
```

Typed `WhereAll`, `WhereNone`, and `WhereAny` are generated for both `World`
and `Query`. The `ComponentId` query factories are runtime overloads that accept
a `ReadOnlySpan<ComponentId>`; pass a span (or array) as one argument on older
C# versions. C# 13 and later can also use expanded positional arguments.
Generated iteration callbacks accept explicit `ComponentId` selectors
positionally, one ID per component row. The first query call creates a query;
each following call composes another filter and returns a new query handle while
reusing the world's existing query-plan cache.

```csharp
ReadOnlySpan<ComponentId> required = stackalloc ComponentId[] { positionId, velocityId };
Query bySpan = world.WhereAll(required);
Query byExpandedParams = world.WhereAll(positionId, velocityId); // C# 13+
Query byTypes = world.WhereAll<Position, Velocity>();

world.ForEach<Position, Velocity>(in byTypes, positionId, velocityId,
    static (ref Position position, in Velocity velocity) =>
        position.X += velocity.X);
```

The generator targets `netstandard2.0` and is shipped from
`analyzers/dotnet/cs`. Its target is independent from the target framework of
the consumer project.

For Unity component authoring, `DeltaECS.Generators` also derives schema IDs
for types marked with `Delta.ECS.DeltaEcsComponentAttribute` and
generates strongly typed registration factories. `DeltaECS.Unity` uses these
factories to register components without enumerating every type in loaded
assemblies. A name-derived ID is stable for a given metadata name; set a
non-zero `SchemaId` explicitly when the identity must survive component
renames. The analyzer reports the current value so it can be copied into the
attribute as an explicit `SchemaId`, and reports explicit zero IDs. The
generator reports duplicate IDs within the consumer compilation; the Unity
registry also checks for collisions when it combines catalogs from loaded
assemblies.

Where functors implement `IWherePredicate`. Use `Where` for component-only
predicates and `WhereEntity` when the predicate also needs the current entity.
Terminal functors reuse the regular `IForEach*` contracts, including their
`ref` context forms. See the
[generator API guide](../src/DeltaECS.Generators/README.md) for complete syntax
and interceptor configuration.

The generator is compiled against `Microsoft.CodeAnalysis.CSharp 4.3.0`, which
is the Roslyn version required by Unity 6 source-generator projects. On newer
Roslyn hosts the optional interceptor path is detected dynamically; Unity/C# 9
consumers use the ordinary generated path.

Consumers using C# 9 or C# 10, including Unity projects with a
`netstandard2.1` API profile, automatically use ordinary generated `ForEach`
overloads. Interceptor source is emitted only when the consumer language
version can parse the required interceptor declarations.

For the optional interceptor path, configure the consumer project as described
in the [generator documentation](https://github.com/Artromskiy/DeltaECS/blob/main/docs/src/DeltaECS.Generators/README.md).
