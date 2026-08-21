# DeltaECS workflow

Correctness first:

```bash
dotnet restore DeltaECS.slnx
dotnet build DeltaECS.slnx -c Release --no-restore \
  --disable-build-servers -m:1 /p:UseSharedCompilation=false -v:minimal
dotnet test tests/DeltaECSTests/DeltaECSTests.csproj \
  -c Release --no-build --no-restore --disable-build-servers
git diff --check
```

Build benchmark projects and use dry contract smokes during review. Do not run
full BenchmarkDotNet measurements unless the user asks. For assembly-guided
micro-algorithms use [benchmarks/README.md](benchmarks/README.md) and
`benchmarks/run-jit-disasm.sh`. For GitHub/manual version comparison use
[docs/github-benchmarks.md](docs/github-benchmarks.md).
