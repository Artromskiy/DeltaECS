# Ecs.CSharp.Benchmark results

This page contains the complete result tables from the vendored [`Ecs.CSharp.Benchmark`](ecs-csharp-benchmark.md) fork. It covers all seven upstream scenario groups and every implementation included in the run (154 benchmarks).

**Run:** 2026-09-09, package version 0.0.14, 100,000 entities, zero padding, one launch. `DeltaECS_Parallel` uses four workers; the other methods use the implementation and execution mode in their method name.

**Host:** Apple M4 Pro, macOS 26.5.2, .NET 10.0.9, Arm64 RyuJIT; BenchmarkDotNet 0.13.12. Lower mean is better. `-` is BenchmarkDotNet's zero-allocation display.

The report settings were:

```text
BenchmarkDotNet v0.13.12, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.926.27113), Arm64 RyuJIT AdvSIMD
  Job-VYNNUB : .NET 10.0.9 (10.0.926.27113), Arm64 RyuJIT AdvSIMD

InvocationCount=1  IterationTime=300.0000 ms  LaunchCount=1
MaxIterationCount=100  MinIterationCount=10  UnrollFactor=1
WarmupCount=10
```

## CreateEntityWithOneComponent

| Method          | Mean       | Error     | StdDev    | Median     | Gen0      | Allocated  |
|---------------- |-----------:|----------:|----------:|-----------:|----------:|-----------:|
| Frent_Bulk      |   274.9 μs |  10.66 μs |  31.11 μs |   265.1 μs |         - |  3403000 B |
| Frent           |   418.9 μs |  22.53 μs |  66.08 μs |   407.0 μs |         - |  3403000 B |
| DeltaECS_Batch  |   434.0 μs |  23.83 μs |  70.27 μs |   434.4 μs |         - |   431840 B |
| LeopotamEcsLite | 1,118.5 μs |  85.47 μs | 252.00 μs |   965.7 μs |         - |  7323040 B |
| FrifloEngineEcs | 1,175.0 μs |  55.10 μs | 161.59 μs | 1,132.9 μs |         - |  3463168 B |
| FlecsNet        | 2,899.3 μs |  57.98 μs | 141.12 μs | 2,900.1 μs |         - |          - |
| Fennecs         | 3,806.6 μs |  75.81 μs | 199.70 μs | 3,814.6 μs |         - | 13964056 B |
| TinyEcs         | 4,030.4 μs |  80.36 μs | 156.73 μs | 4,004.5 μs |         - |  8022800 B |
| LeopotamEcs     | 4,168.9 μs | 177.16 μs | 522.35 μs | 4,393.6 μs | 1000.0000 | 14012760 B |
| DefaultEcs      | 4,923.1 μs |  94.18 μs | 128.92 μs | 4,925.2 μs |         - | 11593152 B |
| DeltaECS        | 5,175.0 μs | 102.02 μs | 198.98 μs | 5,179.7 μs |         - |  4431800 B |
| HypEcs          | 5,266.3 μs | 111.31 μs | 313.95 μs | 5,191.1 μs | 1000.0000 | 26407280 B |
| Myriad          | 6,775.5 μs | 135.00 μs | 278.80 μs | 6,687.6 μs |         - | 17013856 B |
| Arch            | 9,476.9 μs | 162.71 μs | 233.35 μs | 9,504.4 μs |         - |  3255040 B |

## CreateEntityWithTwoComponents

| Method          | Mean        | Error     | StdDev    | Median      | Gen0      | Allocated  |
|---------------- |------------:|----------:|----------:|------------:|----------:|-----------:|
| Frent_Bulk      |    285.7 μs |  12.29 μs |  35.85 μs |    284.3 μs |         - |  3803088 B |
| Frent           |    432.8 μs |   9.17 μs |  25.86 μs |    431.2 μs |         - |  3803088 B |
| DeltaECS_Batch  |    483.0 μs |  26.82 μs |  78.24 μs |    480.4 μs |         - |   836448 B |
| FrifloEngineEcs |  1,351.8 μs |  82.04 μs | 241.90 μs |  1,265.3 μs |         - |  3989600 B |
| Arch            |  1,567.4 μs |  30.90 μs |  63.82 μs |  1,580.0 μs |         - |  3644160 B |
| LeopotamEcsLite |  1,714.2 μs |  62.07 μs | 183.02 μs |  1,672.2 μs |         - |  9420736 B |
| DefaultEcs      |  3,152.9 μs |  66.79 μs | 181.71 μs |  3,137.4 μs |         - | 15789288 B |
| LeopotamEcs     |  3,548.7 μs |  73.60 μs | 197.72 μs |  3,549.8 μs | 1000.0000 | 15064424 B |
| DeltaECS        |  5,132.3 μs | 102.45 μs | 241.48 μs |  5,121.7 μs |         - |  4836408 B |
| FlecsNet        |  6,203.7 μs | 123.77 μs |  89.50 μs |  6,189.0 μs |         - |          - |
| Fennecs         |  7,514.4 μs | 148.70 μs | 326.41 μs |  7,472.2 μs |         - | 15538976 B |
| TinyEcs         |  9,045.5 μs | 176.51 μs | 322.76 μs |  9,098.4 μs | 1000.0000 | 14116256 B |
| Myriad          |  9,302.4 μs | 184.28 μs | 423.42 μs |  9,146.1 μs |         - | 17418136 B |
| HypEcs          | 11,301.4 μs | 224.24 μs | 477.87 μs | 11,316.3 μs | 3000.0000 | 46342496 B |

## CreateEntityWithThreeComponents

| Method          | Mean        | Error     | StdDev    | Median      | Gen0      | Gen1      | Allocated  |
|---------------- |------------:|----------:|----------:|------------:|----------:|----------:|-----------:|
| Frent_Bulk      |    325.5 μs |  17.62 μs |  51.96 μs |    324.1 μs |         - |         - |  4203176 B |
| DeltaECS_Batch  |    491.4 μs |  29.04 μs |  85.63 μs |    480.8 μs |         - |         - |  1241064 B |
| Frent           |    540.4 μs |  25.34 μs |  73.10 μs |    538.9 μs |         - |         - |  4203176 B |
| FrifloEngineEcs |  1,397.0 μs |  52.32 μs | 153.44 μs |  1,381.2 μs |         - |         - |  4516032 B |
| Arch            |  1,671.3 μs |  36.96 μs | 107.24 μs |  1,670.2 μs |         - |         - |  4042736 B |
| LeopotamEcsLite |  1,907.5 μs |  70.48 μs | 184.43 μs |  1,874.4 μs |         - |         - | 11518432 B |
| LeopotamEcs     |  4,070.4 μs |  80.87 μs | 201.39 μs |  4,008.0 μs | 1000.0000 |         - | 16114408 B |
| DefaultEcs      |  4,109.5 μs |  91.56 μs | 253.72 μs |  4,106.2 μs |         - |         - | 19985416 B |
| DeltaECS        |  5,546.2 μs | 109.13 μs | 207.63 μs |  5,503.4 μs |         - |         - |  5241024 B |
| FlecsNet        |  9,692.4 μs | 189.82 μs | 259.83 μs |  9,696.5 μs |         - |         - |          - |
| Fennecs         | 11,553.6 μs | 213.92 μs | 285.58 μs | 11,613.5 μs |         - |         - | 17114568 B |
| Myriad          | 12,073.9 μs | 240.24 μs | 433.21 μs | 12,051.7 μs |         - |         - | 17823096 B |
| TinyEcs         | 13,964.8 μs | 272.81 μs | 491.93 μs | 13,876.5 μs | 2000.0000 | 1000.0000 | 21826128 B |
| HypEcs          | 20,589.7 μs | 409.64 μs | 342.07 μs | 20,564.3 μs | 6000.0000 |         - | 70398704 B |

## SystemWithOneComponent

| Method                                 | Mean       | Error      | StdDev     | Median     | Gen0   | Allocated |
|--------------------------------------- |-----------:|-----------:|-----------:|-----------:|-------:|----------:|
| Frent_Simd                             |   5.821 μs |  0.0129 μs |  0.0085 μs |   5.823 μs |      - |         - |
| FrifloEngineEcs_SIMD_MonoThread        |   7.812 μs |  0.0897 μs |  0.0593 μs |   7.796 μs |      - |         - |
| DeltaECS_Parallel                      |   8.377 μs |  0.1302 μs |  0.0861 μs |   8.412 μs |      - |         - |
| Myriad_SingleThreadChunk_SIMD          |   8.407 μs |  0.1520 μs |  0.1187 μs |   8.351 μs |      - |         - |
| FrifloEngineEcs_MultiThread            |  23.085 μs |  4.7652 μs | 13.2043 μs |  18.124 μs |      - |         - |
| TinyEcs_EachJob                        |  23.606 μs |  0.7337 μs |  2.1633 μs |  23.079 μs | 0.1389 |    1552 B |
| HypEcs_MonoThread                      |  25.450 μs |  0.1701 μs |  0.1125 μs |  25.480 μs |      - |      72 B |
| TinyEcs_Each                           |  26.046 μs |  0.2379 μs |  0.1574 μs |  26.011 μs |      - |         - |
| Frent_QueryInline                      |  26.426 μs |  0.1643 μs |  0.0859 μs |  26.448 μs |      - |         - |
| Fennecs_Raw                            |  27.015 μs |  0.5166 μs |  0.4314 μs |  26.898 μs |      - |         - |
| DefaultEcs_ComponentSystem_MonoThread  |  27.153 μs |  0.5181 μs |  0.3083 μs |  27.010 μs |      - |         - |
| FrifloEngineEcs_MonoThread             |  27.201 μs |  0.4126 μs |  0.2455 μs |  27.252 μs |      - |         - |
| Frent_QueryDelegate                    |  27.746 μs |  0.6019 μs |  1.7075 μs |  27.104 μs |      - |         - |
| Fennecs_ForEach                        |  27.947 μs |  0.3525 μs |  0.1844 μs |  28.020 μs |      - |         - |
| Myriad_SingleThreadChunk               |  28.182 μs |  0.3596 μs |  0.2379 μs |  28.204 μs |      - |         - |
| DeltaECS                               |  28.341 μs |  0.5004 μs |  0.3310 μs |  28.428 μs |      - |         - |
| Myriad_SingleThread                    |  28.790 μs |  0.5624 μs |  0.5523 μs |  28.937 μs |      - |         - |
| HypEcs_MultiThread                     |  29.202 μs |  0.5237 μs |  0.4089 μs |  29.171 μs | 0.1905 |    2032 B |
| FlecsNet_Iter                          |  30.398 μs |  0.5732 μs |  0.4145 μs |  30.506 μs |      - |         - |
| DefaultEcs_ComponentSystem_MultiThread |  42.765 μs |  8.5791 μs | 23.3402 μs |  34.383 μs |      - |         - |
| Arch_MonoThread_SourceGenerated        |  54.735 μs |  0.5510 μs |  0.3645 μs |  54.835 μs |      - |         - |
| Arch_MonoThread                        |  55.272 μs |  0.2610 μs |  0.1726 μs |  55.269 μs |      - |         - |
| Fennecs_Job                            |  55.447 μs |  1.2073 μs |  3.2640 μs |  55.672 μs |      - |         - |
| DefaultEcs_EntitySetSystem_MonoThread  |  55.828 μs |  1.0839 μs |  0.9051 μs |  56.100 μs |      - |         - |
| LeopotamEcsLite                        |  59.627 μs |  0.7397 μs |  0.4892 μs |  59.603 μs |      - |         - |
| FlecsNet_Each                          |  61.932 μs |  0.8546 μs |  0.5653 μs |  61.846 μs |      - |         - |
| Arch_MultiThread                       |  63.597 μs |  0.3845 μs |  0.2288 μs |  63.660 μs |      - |         - |
| LeopotamEcs                            |  66.068 μs |  0.5450 μs |  0.3605 μs |  65.975 μs |      - |         - |
| DefaultEcs_EntitySetSystem_MultiThread |  81.730 μs | 14.2131 μs | 41.4603 μs |  64.920 μs |      - |         - |
| Myriad_Delegate                        | 105.575 μs |  2.0121 μs |  1.1974 μs | 106.092 μs |      - |         - |

## SystemWithTwoComponents

| Method                          | Mean       | Error      | StdDev     | Median     | Gen0   | Allocated |
|-------------------------------- |-----------:|-----------:|-----------:|-----------:|-------:|----------:|
| Frent_Simd                      |   9.202 μs |  0.0462 μs |  0.0241 μs |   9.196 μs |      - |         - |
| FrifloEngineEcs_SIMD_MonoThread |   9.396 μs |  0.1404 μs |  0.0929 μs |   9.358 μs |      - |         - |
| DeltaECS_Parallel               |   9.615 μs |  0.0813 μs |  0.0538 μs |   9.609 μs |      - |         - |
| Myriad_SingleThreadChunk_SIMD   |  14.911 μs |  0.2929 μs |  0.4645 μs |  14.809 μs |      - |         - |
| FrifloEngineEcs_MultiThread     |  25.768 μs |  3.2162 μs |  9.0713 μs |  22.841 μs |      - |         - |
| Frent_QueryInline               |  27.642 μs |  0.1804 μs |  0.1074 μs |  27.673 μs |      - |         - |
| Frent_QueryDelegate             |  27.659 μs |  0.3037 μs |  0.1807 μs |  27.606 μs |      - |         - |
| HypEcs_MonoThread               |  27.714 μs |  0.4564 μs |  0.3300 μs |  27.575 μs |      - |     112 B |
| FrifloEngineEcs_MonoThread      |  28.287 μs |  0.2325 μs |  0.1216 μs |  28.287 μs |      - |         - |
| Myriad_SingleThread             |  28.344 μs |  0.5389 μs |  0.5767 μs |  28.338 μs |      - |         - |
| TinyEcs_Each                    |  28.546 μs |  0.4729 μs |  0.3128 μs |  28.507 μs |      - |         - |
| DeltaECS                        |  28.685 μs |  0.5540 μs |  0.5928 μs |  28.361 μs |      - |         - |
| HypEcs_MultiThread              |  29.682 μs |  0.5912 μs |  1.2851 μs |  29.494 μs | 0.1947 |    2069 B |
| Fennecs_Raw                     |  29.866 μs |  0.3913 μs |  0.2588 μs |  29.955 μs |      - |         - |
| Myriad_SingleThreadChunk        |  30.962 μs |  0.6091 μs |  0.4029 μs |  30.993 μs |      - |         - |
| FlecsNet_Iter                   |  33.733 μs |  0.2800 μs |  0.1852 μs |  33.728 μs |      - |         - |
| TinyEcs_EachJob                 |  34.941 μs |  1.1146 μs |  3.2160 μs |  34.614 μs |      - |    1556 B |
| Fennecs_ForEach                 |  36.997 μs |  0.6800 μs |  0.4917 μs |  36.942 μs |      - |         - |
| Fennecs_Job                     |  47.548 μs |  1.0206 μs |  3.0094 μs |  46.996 μs |      - |         - |
| Arch_MonoThread                 |  54.311 μs |  0.1866 μs |  0.1234 μs |  54.315 μs |      - |         - |
| Arch_MonoThread_SourceGenerated |  55.149 μs |  0.1616 μs |  0.0961 μs |  55.126 μs |      - |         - |
| Arch_MultiThread                |  61.900 μs |  0.6175 μs |  0.3675 μs |  62.058 μs |      - |         - |
| FlecsNet_Each                   |  85.451 μs |  0.5110 μs |  0.3041 μs |  85.309 μs |      - |         - |
| DefaultEcs_MonoThread           |  87.694 μs |  1.7301 μs |  1.0295 μs |  87.846 μs |      - |         - |
| Myriad_Delegate                 |  98.429 μs |  1.8396 μs |  1.2168 μs |  98.444 μs |      - |         - |
| LeopotamEcs                     | 122.799 μs |  2.3643 μs |  1.5638 μs | 123.539 μs |      - |         - |
| LeopotamEcsLite                 | 142.863 μs |  2.2773 μs |  1.5063 μs | 142.917 μs |      - |         - |
| DefaultEcs_MultiThread          | 159.212 μs | 29.0830 μs | 85.2954 μs | 127.162 μs |      - |         - |

## SystemWithThreeComponents

| Method                          | Mean       | Error       | StdDev      | Median     | Gen0   | Allocated |
|-------------------------------- |-----------:|------------:|------------:|-----------:|-------:|----------:|
| FrifloEngineEcs_SIMD_MonoThread |   9.260 μs |   0.1608 μs |   0.1063 μs |   9.252 μs |      - |         - |
| DeltaECS_Parallel               |  12.872 μs |   0.2492 μs |   0.2559 μs |  12.828 μs |      - |         - |
| Frent_Simd                      |  14.003 μs |   0.2598 μs |   0.1718 μs |  14.008 μs |      - |         - |
| TinyEcs_EachJob                 |  26.278 μs |   0.5436 μs |   1.5684 μs |  26.273 μs | 0.1613 |    1560 B |
| HypEcs_MonoThread               |  30.334 μs |   0.5621 μs |   0.4064 μs |  30.141 μs |      - |     152 B |
| FrifloEngineEcs_MonoThread      |  32.351 μs |   0.5342 μs |   0.3179 μs |  32.333 μs |      - |         - |
| HypEcs_MultiThread              |  33.242 μs |   0.5355 μs |   0.3186 μs |  33.130 μs | 0.2240 |    2111 B |
| DeltaECS                        |  36.861 μs |   0.1183 μs |   0.0782 μs |  36.873 μs |      - |         - |
| Myriad_SingleThreadChunk        |  37.440 μs |   0.7358 μs |   0.6522 μs |  37.102 μs |      - |         - |
| Myriad_SingleThread             |  37.447 μs |   0.6443 μs |   0.6616 μs |  37.154 μs |      - |         - |
| Fennecs_Raw                     |  39.903 μs |   0.2133 μs |   0.1411 μs |  39.925 μs |      - |         - |
| Frent_QueryDelegate             |  40.210 μs |   0.3567 μs |   0.2123 μs |  40.198 μs |      - |         - |
| Frent_QueryInline               |  51.315 μs |   0.4526 μs |   0.2994 μs |  51.344 μs |      - |         - |
| FlecsNet_Iter                   |  52.721 μs |   1.0398 μs |   1.3150 μs |  52.682 μs |      - |         - |
| Fennecs_ForEach                 |  53.403 μs |   0.5741 μs |   0.8234 μs |  53.512 μs |      - |         - |
| Fennecs_Job                     |  55.007 μs |   1.0899 μs |   2.6323 μs |  54.880 μs |      - |         - |
| TinyEcs_Each                    |  57.346 μs |   0.5839 μs |   0.3475 μs |  57.349 μs |      - |         - |
| Arch_MonoThread_SourceGenerated |  61.915 μs |   1.1965 μs |   1.0606 μs |  62.249 μs |      - |         - |
| Arch_MonoThread                 |  62.109 μs |   1.1938 μs |   1.1167 μs |  62.490 μs |      - |         - |
| Arch_MultiThread                |  75.500 μs |   1.4499 μs |   1.0484 μs |  75.629 μs |      - |         - |
| DefaultEcs_MultiThread          |  75.687 μs |   6.9059 μs |  20.1447 μs |  69.996 μs |      - |         - |
| FlecsNet_Each                   | 104.097 μs |   1.8530 μs |   1.1027 μs | 104.590 μs |      - |         - |
| Myriad_Delegate                 | 122.624 μs |   1.8765 μs |   1.2412 μs | 122.477 μs |      - |         - |
| DefaultEcs_MonoThread           | 137.556 μs |   2.7014 μs |   1.7868 μs | 137.554 μs |      - |         - |
| LeopotamEcs                     | 209.111 μs |   2.5465 μs |   1.6844 μs | 208.610 μs |      - |         - |
| LeopotamEcsLite                 | 249.955 μs |   2.6560 μs |   1.5805 μs | 249.371 μs |      - |         - |
| FrifloEngineEcs_MultiThread     | 639.988 μs | 111.0309 μs | 314.9760 μs | 619.653 μs |      - |         - |

## SystemWithTwoComponentsMultipleComposition

| Method                          | Mean       | Error     | StdDev     | Median     | Gen0   | Allocated |
|-------------------------------- |-----------:|----------:|-----------:|-----------:|-------:|----------:|
| DeltaECS_Parallel               |   9.300 μs | 0.0331 μs |  0.0219 μs |   9.308 μs |      - |         - |
| Frent_Simd                      |   9.528 μs | 0.2155 μs |  0.6044 μs |   9.275 μs |      - |         - |
| FrifloEngineEcs_SIMD_MonoThread |  10.171 μs | 0.1153 μs |  0.0763 μs |  10.166 μs |      - |         - |
| HypEcs_MultiThread              |  12.326 μs | 0.1815 μs |  0.1080 μs |  12.305 μs | 0.3594 |    3051 B |
| Myriad_SingleThreadChunk_SIMD   |  14.773 μs | 0.2893 μs |  0.3095 μs |  14.762 μs |      - |         - |
| Myriad_SingleThread             |  26.417 μs | 0.1658 μs |  0.0987 μs |  26.407 μs |      - |         - |
| HypEcs_MonoThread               |  27.197 μs | 0.4933 μs |  0.3263 μs |  27.262 μs |      - |     352 B |
| FrifloEngineEcs_MonoThread      |  27.984 μs | 0.4894 μs |  0.2912 μs |  27.942 μs |      - |         - |
| Frent_QueryInline               |  28.062 μs | 0.2559 μs |  0.1692 μs |  28.033 μs |      - |         - |
| DeltaECS                        |  28.266 μs | 0.3136 μs |  0.2074 μs |  28.315 μs |      - |         - |
| TinyEcs_Each                    |  28.366 μs | 0.5510 μs |  0.5659 μs |  28.360 μs |      - |         - |
| Myriad_SingleThreadChunk        |  28.689 μs | 0.1758 μs |  0.1163 μs |  28.668 μs |      - |         - |
| TinyEcs_EachJob                 |  30.058 μs | 1.0066 μs |  2.9522 μs |  30.560 μs | 0.1685 |    2080 B |
| Fennecs_Raw                     |  30.437 μs | 0.2127 μs |  0.1407 μs |  30.420 μs |      - |         - |
| FlecsNet_Iter                   |  35.596 μs | 0.6965 μs |  0.7153 μs |  35.644 μs |      - |         - |
| Fennecs_ForEach                 |  37.592 μs | 0.4002 μs |  0.2647 μs |  37.556 μs |      - |         - |
| FrifloEngineEcs_MultiThread     |  44.650 μs | 3.9439 μs | 11.3789 μs |  43.611 μs |      - |         - |
| Fennecs_Job                     |  48.660 μs | 1.0122 μs |  2.9686 μs |  48.632 μs |      - |         - |
| DefaultEcs_MultiThread          |  53.393 μs | 4.7446 μs | 13.6130 μs |  48.655 μs |      - |         - |
| Arch_MonoThread_SourceGenerated |  55.703 μs | 0.7656 μs |  0.5064 μs |  55.778 μs |      - |         - |
| Arch                            |  56.644 μs | 0.6750 μs |  0.4017 μs |  56.715 μs |      - |         - |
| FlecsNet_Each                   |  84.058 μs | 0.9955 μs |  0.6585 μs |  84.047 μs |      - |         - |
| DefaultEcs_MonoThread           |  89.600 μs | 1.3736 μs |  0.8174 μs |  89.460 μs |      - |         - |
| Myriad_Delegate                 |  97.552 μs | 1.8532 μs |  1.9031 μs |  96.604 μs |      - |         - |
| LeopotamEcs                     | 123.075 μs | 1.2882 μs |  0.7666 μs | 123.199 μs |      - |         - |
| LeopotamEcsLite                 | 135.658 μs | 2.6862 μs |  1.7768 μs | 135.382 μs |      - |         - |
| Arch_MultiThread                | 225.232 μs | 4.2244 μs |  3.0545 μs | 225.735 μs |      - |         - |
