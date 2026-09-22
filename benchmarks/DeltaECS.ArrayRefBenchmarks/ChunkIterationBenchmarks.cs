using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET10_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace Delta.ECS.ArrayRefBenchmarks;

/// <summary>Compares indexed, ref-cursor and unmanaged-linked chunk traversal.</summary>
[MemoryDiagnoser]
[Config(typeof(ChunkIterationBenchmarkConfig))]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "BenchmarkDotNet discovers benchmark types only when they are public.")]
public unsafe class ChunkIterationBenchmarks : IDisposable
{
    private byte[][] _managedChunks = null!;
    private GCHandle[] _handles = null!;
    private byte*[] _pinnedAddresses = null!;
    private UnmanagedChunk* _chainHead;

    [StructLayout(LayoutKind.Sequential)]
    private struct UnmanagedChunk
    {
        internal byte* DataPtr;
        internal int Length;
        internal UnmanagedChunk* Next;
    }

    /// <summary>Allocates identical chunks and prepares the traversal alternatives.</summary>
    [GlobalSetup]
    public void Setup()
    {
        int chunkSize = Program.ChunkSize;
        int chunkCount = Program.ChunkCount;
        _managedChunks = new byte[chunkCount][];
        _handles = new GCHandle[chunkCount];
        _pinnedAddresses = new byte*[chunkCount];

        for (int index = chunkCount - 1; index >= 0; index--)
        {
            byte[] chunk = new byte[chunkSize];
            for (int offset = 0; offset < chunkSize; offset++)
            {
                chunk[offset] = (byte)(index + offset);
            }

            _managedChunks[index] = chunk;
            _handles[index] = GCHandle.Alloc(chunk, GCHandleType.Pinned);
            _pinnedAddresses[index] = (byte*)_handles[index].AddrOfPinnedObject();
        }

        UnmanagedChunk* previousChunk = null;
        for (int index = chunkCount - 1; index >= 0; index--)
        {
            UnmanagedChunk* currentChunk = (UnmanagedChunk*)Marshal.AllocHGlobal(sizeof(UnmanagedChunk));
            currentChunk->DataPtr = _pinnedAddresses[index];
            currentChunk->Length = chunkSize;
            currentChunk->Next = previousChunk;
            previousChunk = currentChunk;
        }

        _chainHead = previousChunk;
        int expectedChecksum = CalculateExpectedChecksum(chunkSize, chunkCount);
        if (StandardManaged() != expectedChecksum
            || RefCursorBaseline() != expectedChecksum
            || RefCursor() != expectedChecksum
#if NET10_0_OR_GREATER
            || RefCursorVector128() != expectedChecksum
#endif
            || UnmanagedChain() != expectedChecksum)
        {
            throw new InvalidOperationException("Chunk traversal variants returned different checksums.");
        }
    }

    /// <summary>Releases pinned handles and unmanaged link nodes.</summary>
    [GlobalCleanup]
    public void Cleanup() => Dispose();

    /// <summary>Releases resources owned by the benchmark.</summary>
    public void Dispose()
    {
        for (int index = 0; index < _handles.Length; index++)
        {
            if (_handles[index].IsAllocated)
            {
                _handles[index].Free();
            }
        }

        UnmanagedChunk* current = _chainHead;
        while (current != null)
        {
            UnmanagedChunk* next = current->Next;
            Marshal.FreeHGlobal((IntPtr)current);
            current = next;
        }

        _chainHead = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Traverses each chunk through ordinary managed array indexing.</summary>
    [Benchmark(Baseline = true)]
    public int StandardManaged()
    {
        int sum = 0;
        for (int chunkIndex = 0; chunkIndex < _managedChunks.Length; chunkIndex++)
        {
            byte[] chunk = _managedChunks[chunkIndex];
            for (int index = 0; index < chunk.Length; index++)
            {
                sum += chunk[index];
            }
        }

        return sum;
    }

    /// <summary>Traverses pinned chunks and computes each next reference offset.</summary>
    [Benchmark]
    public int RefCursorBaseline()
    {
        int sum = 0;
        int currentChunkIndex = 0;
        ref byte currentReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(
            _managedChunks[currentChunkIndex]);

        while (currentChunkIndex < _managedChunks.Length)
        {
            for (int index = 0; index < Program.ChunkSize; index++)
            {
                sum += currentReference;
                currentReference = ref Unsafe.Add(ref currentReference, 1);
            }

            currentChunkIndex++;
            if (currentChunkIndex < _managedChunks.Length)
            {
                ref byte nextReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(
                    _managedChunks[currentChunkIndex]);
                IntPtr delta = Unsafe.ByteOffset(ref currentReference, ref nextReference);
                currentReference = ref Unsafe.AddByteOffset(ref currentReference, delta);
            }
        }

        return sum;
    }

    /// <summary>Uses a ref cursor for chunk starts and unrolls four byte reads.</summary>
    [Benchmark]
    public int RefCursor()
    {
        int sum0 = 0;
        int sum1 = 0;
        int sum2 = 0;
        int sum3 = 0;
        int chunkSize = Program.ChunkSize;
        int chunkCount = _managedChunks.Length;
        byte[][] chunks = _managedChunks;
        ref byte[] chunkCursor = ref GeneratedForEachRuntime.GetGeneratedArrayReference(chunks);
        ref byte[] chunkEnd = ref Unsafe.Add(ref chunkCursor, chunkCount);
        ref byte currentReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(chunkCursor);
        int alignedChunkSize = chunkSize & ~3;

        while (true)
        {
            for (int index = 0; index < alignedChunkSize; index += 4)
            {
                sum0 += currentReference;
                sum1 += Unsafe.Add(ref currentReference, 1);
                sum2 += Unsafe.Add(ref currentReference, 2);
                sum3 += Unsafe.Add(ref currentReference, 3);
                currentReference = ref Unsafe.Add(ref currentReference, 4);
            }

            for (int index = alignedChunkSize; index < chunkSize; index++)
            {
                sum0 += currentReference;
                currentReference = ref Unsafe.Add(ref currentReference, 1);
            }

            ref byte[] nextChunk = ref Unsafe.Add(ref chunkCursor, 1);
            if (!Unsafe.IsAddressLessThan(ref nextChunk, ref chunkEnd))
            {
                break;
            }

            ref byte nextReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(nextChunk);
            IntPtr delta = Unsafe.ByteOffset(ref currentReference, ref nextReference);
            currentReference = ref Unsafe.AddByteOffset(ref currentReference, delta);
            chunkCursor = ref nextChunk;
        }

        return sum0 + sum1 + sum2 + sum3;
    }

#if NET10_0_OR_GREATER
    /// <summary>Loads sixteen bytes per iteration and horizontally sums them.</summary>
    [Benchmark]
    public int RefCursorVector128()
    {
        int sum = 0;
        int chunkSize = Program.ChunkSize;
        int chunkCount = _managedChunks.Length;
        byte[][] chunks = _managedChunks;
        ref byte[] chunkCursor = ref GeneratedForEachRuntime.GetGeneratedArrayReference(chunks);
        ref byte[] chunkEnd = ref Unsafe.Add(ref chunkCursor, chunkCount);
        ref byte currentReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(chunkCursor);
        int vectorizedChunkSize = chunkSize & ~15;

        while (true)
        {
            for (int index = 0; index < vectorizedChunkSize; index += 16)
            {
                Vector128<byte> bytes = Vector128.LoadUnsafe(ref currentReference);
                Vector128<ushort> pairedSums = Vector128.Add(
                    Vector128.WidenLower(bytes),
                    Vector128.WidenUpper(bytes));
                sum += Vector128.Sum(pairedSums);
                currentReference = ref Unsafe.Add(ref currentReference, 16);
            }

            for (int index = vectorizedChunkSize; index < chunkSize; index++)
            {
                sum += currentReference;
                currentReference = ref Unsafe.Add(ref currentReference, 1);
            }

            ref byte[] nextChunk = ref Unsafe.Add(ref chunkCursor, 1);
            if (!Unsafe.IsAddressLessThan(ref nextChunk, ref chunkEnd))
            {
                break;
            }

            ref byte nextReference = ref GeneratedForEachRuntime.GetGeneratedArrayReference(nextChunk);
            IntPtr delta = Unsafe.ByteOffset(ref currentReference, ref nextReference);
            currentReference = ref Unsafe.AddByteOffset(ref currentReference, delta);
            chunkCursor = ref nextChunk;
        }

        return sum;
    }
#endif

    /// <summary>Traverses the chunks through unmanaged linked metadata.</summary>
    [Benchmark]
    public int UnmanagedChain()
    {
        int sum = 0;
        UnmanagedChunk* currentChunk = _chainHead;
        while (currentChunk != null)
        {
            byte* currentPointer = currentChunk->DataPtr;
            byte* endPointer = currentPointer + currentChunk->Length;
            while (currentPointer < endPointer)
            {
                sum += *currentPointer;
                currentPointer++;
            }

            currentChunk = currentChunk->Next;
        }

        return sum;
    }

    private static int CalculateExpectedChecksum(int chunkSize, int chunkCount)
    {
        int sum = 0;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            for (int index = 0; index < chunkSize; index++)
            {
                sum += (byte)(chunkIndex + index);
            }
        }

        return sum;
    }
}

/// <summary>Enables disassembly only on platforms supported by BenchmarkDotNet.</summary>
public sealed class ChunkIterationBenchmarkConfig : ManualConfig
{
    public ChunkIterationBenchmarkConfig()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                printSource: true,
                maxDepth: 2)));
        }
    }
}
