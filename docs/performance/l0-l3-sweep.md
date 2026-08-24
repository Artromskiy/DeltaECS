# Dense Movement4 optimization sweep

This archived record summarizes the accepted internal changes from a focused
ARM64 JIT sweep. The public query API remained the three-loop
`QueryScope`/`QueryArchetypes`/`QueryChunks`/`QuerySlots` path.

## Recorded JIT signal

The selected combination reduced the first emitted `Movement4` block from
1408 B to 1084 B. In the same report, `blr` changed from 16 to 11, `branch`
from 39 to 33, `ldr` from 96 to 66, and `str` from 21 to 12. The report was
captured on ARM64; these counts are not portable to the Linux x64 runner.

## Throughput interpretation

The paired short runs were mixed: small amounts were near the noise floor and
the 100,000-entity lane was not a repeatable throughput win. The change is
therefore retained as an assembly-size observation, not as a universal speed
claim.

Further work must compare the current source with the same checksum, runtime,
architecture and BenchmarkDotNet job. See [performance README](README.md) and
[benchmarks README](../../benchmarks/README.md).
