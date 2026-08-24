```

BenchmarkDotNet v0.13.12, macOS 26.5.2 (25F84) [Darwin 25.5.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD
  Job-FMBMCU : .NET 8.0.29 (8.0.2926.32403), Arm64 RyuJIT AdvSIMD

InvocationCount=1  UnrollFactor=1  

```
| Method                                  | Amount | Mean       | Error      | StdDev     | Median     | Allocated |
|---------------------------------------- |------- |-----------:|-----------:|-----------:|-----------:|----------:|
| **Movement4Components**                     | **100**    |   **3.663 μs** |  **0.1914 μs** |  **0.5430 μs** |   **3.687 μs** |     **736 B** |
| Movement4ComponentsGenericCompatibility | 100    |   3.653 μs |  0.1897 μs |  0.5472 μs |   3.604 μs |     736 B |
| **Movement4Components**                     | **1000**   |  **29.714 μs** |  **1.7611 μs** |  **5.1092 μs** |  **26.916 μs** |     **736 B** |
| Movement4ComponentsGenericCompatibility | 1000   |  30.419 μs |  1.3880 μs |  4.0267 μs |  31.083 μs |     736 B |
| **Movement4Components**                     | **10000**  |  **71.792 μs** |  **2.7769 μs** |  **8.1003 μs** |  **71.520 μs** |     **736 B** |
| Movement4ComponentsGenericCompatibility | 10000  |  65.157 μs |  2.7790 μs |  7.9735 μs |  62.917 μs |     736 B |
| **Movement4Components**                     | **100000** | **375.158 μs** |  **9.4902 μs** | **26.4549 μs** | **373.875 μs** |     **736 B** |
| Movement4ComponentsGenericCompatibility | 100000 | 394.636 μs | 15.3403 μs | 44.9904 μs | 383.667 μs |     736 B |
