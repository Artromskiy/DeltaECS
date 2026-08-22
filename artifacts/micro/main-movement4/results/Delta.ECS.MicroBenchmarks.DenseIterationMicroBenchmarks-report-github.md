```

BenchmarkDotNet v0.13.12, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD
  Job-UUEAIZ : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD

InvocationCount=1  UnrollFactor=1

```
| Method              | Amount | Mean       | Error     | StdDev     | Median     | Allocated |
|-------------------- |------- |-----------:|----------:|-----------:|-----------:|----------:|
| **Movement4Components** | **100**    |   **3.345 μs** | **0.1197 μs** |  **0.3436 μs** |   **3.292 μs** |     **736 B** |
| **Movement4Components** | **1000**   |  **26.468 μs** | **1.0579 μs** |  **3.0859 μs** |  **26.208 μs** |     **736 B** |
| **Movement4Components** | **10000**  |  **39.910 μs** | **1.5731 μs** |  **4.5639 μs** |  **38.416 μs** |     **736 B** |
| **Movement4Components** | **100000** | **163.565 μs** | **5.0671 μs** | **14.7809 μs** | **161.500 μs** |     **736 B** |
