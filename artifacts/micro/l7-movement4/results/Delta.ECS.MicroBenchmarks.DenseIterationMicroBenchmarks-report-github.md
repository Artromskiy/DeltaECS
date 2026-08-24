```

BenchmarkDotNet v0.13.12, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD
  Job-UZJGTW : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD

InvocationCount=1  UnrollFactor=1

```
| Method              | Amount | Mean       | Error      | StdDev     | Median     | Allocated |
|-------------------- |------- |-----------:|-----------:|-----------:|-----------:|----------:|
| **Movement4Components** | **100**    |   **5.411 μs** |  **0.1734 μs** |  **0.4948 μs** |   **5.375 μs** |     **736 B** |
| **Movement4Components** | **1000**   |  **43.159 μs** |  **1.5656 μs** |  **4.5171 μs** |  **41.459 μs** |     **736 B** |
| **Movement4Components** | **10000**  |  **61.473 μs** |  **2.0331 μs** |  **5.8984 μs** |  **60.209 μs** |     **736 B** |
| **Movement4Components** | **100000** | **227.890 μs** | **10.4182 μs** | **30.3905 μs** | **218.228 μs** |     **736 B** |
