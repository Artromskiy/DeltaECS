using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    private int[] _chunkCounts = null!;
    private GCHandle[] _handles = null!;
    private byte*[] _pinnedAddresses = null!;
    private UnmanagedChunk* _chainHead;
    private UnmanagedEndChunk* _endChainHead;
    private IntPtr _endChainStorage;

    [StructLayout(LayoutKind.Sequential)]
    private struct UnmanagedChunk
    {
        internal byte* DataPtr;
        internal int Length;
        internal UnmanagedChunk* Next;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnmanagedEndChunk
    {
        internal byte* DataPtr;
        internal byte* EndPtr;
        internal UnmanagedEndChunk* Next;
    }

    /// <summary>Allocates identical chunks and prepares the traversal alternatives.</summary>
    [GlobalSetup]
    public void Setup()
    {
        int chunkCapacity = Program.ChunkCapacity;
        int chunkCount = Program.ChunkCount;
        _managedChunks = new byte[chunkCount][];
        _chunkCounts = new int[chunkCount];
        _handles = new GCHandle[chunkCount];
        _pinnedAddresses = new byte*[chunkCount];

        for (int index = chunkCount - 1; index >= 0; index--)
        {
            byte[] chunk = new byte[chunkCapacity];
            int count = Math.Min(chunkCapacity, Program.Amount - (index * chunkCapacity));
            for (int offset = 0; offset < count; offset++)
            {
                chunk[offset] = (byte)(index + offset);
            }

            _managedChunks[index] = chunk;
            _chunkCounts[index] = count;
            _handles[index] = GCHandle.Alloc(chunk, GCHandleType.Pinned);
            _pinnedAddresses[index] = (byte*)_handles[index].AddrOfPinnedObject();
        }

        UnmanagedChunk* previousChunk = null;
        _endChainStorage = Marshal.AllocHGlobal(checked(sizeof(UnmanagedEndChunk) * chunkCount));
        UnmanagedEndChunk* endChunks = (UnmanagedEndChunk*)_endChainStorage;
        for (int index = chunkCount - 1; index >= 0; index--)
        {
            UnmanagedChunk* currentChunk = (UnmanagedChunk*)Marshal.AllocHGlobal(sizeof(UnmanagedChunk));
            currentChunk->DataPtr = _pinnedAddresses[index];
            currentChunk->Length = _chunkCounts[index];
            currentChunk->Next = previousChunk;
            previousChunk = currentChunk;

            UnmanagedEndChunk* currentEndChunk = &endChunks[index];
            currentEndChunk->DataPtr = _pinnedAddresses[index];
            currentEndChunk->EndPtr = _pinnedAddresses[index] + _chunkCounts[index];
            currentEndChunk->Next = index + 1 < chunkCount ? currentEndChunk + 1 : null;
        }

        _chainHead = previousChunk;
        _endChainHead = endChunks;
        int expectedChecksum = CalculateExpectedChecksum(chunkCapacity, chunkCount);
        if (StandardManaged() != expectedChecksum
            || RefCursorBaseline() != expectedChecksum
            || UnmanagedChainBaseline() != expectedChecksum
            || UnmanagedChainCandidate() != expectedChecksum)
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
        _endChainHead = null;
        if (_endChainStorage != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_endChainStorage);
            _endChainStorage = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Traverses active elements in each full-capacity chunk through managed indexing.</summary>
    [Benchmark(Baseline = true)]
    public int StandardManaged()
    {
        int sum = 0;
        for (int chunkIndex = 0; chunkIndex < _managedChunks.Length; chunkIndex++)
        {
            byte[] chunk = _managedChunks[chunkIndex];
            int count = _chunkCounts[chunkIndex];
            for (int index = 0; index < count; index++)
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
            int count = _chunkCounts[currentChunkIndex];
            for (int index = 0; index < count; index++)
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

    /// <summary>Traverses linked descriptors with a length and a per-chunk empty check.</summary>
    [Benchmark]
    public int UnmanagedChainBaseline()
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

    /// <summary>Traverses the chunks through unmanaged linked metadata.</summary>
    [Benchmark]
    public int UnmanagedChainCandidate()
    {
        int sum = 0;
        UnmanagedEndChunk* currentChunk = _endChainHead;
        do
        {
            byte* currentPointer = currentChunk->DataPtr;
            byte* endPointer = currentChunk->EndPtr;
            UnmanagedEndChunk* nextChunk = currentChunk->Next;
            do
            {
                sum += *currentPointer;
                currentPointer++;
            }
            while (currentPointer < endPointer);

            currentChunk = nextChunk;
        }
        while (currentChunk != null);

        return sum;
    }

    private static int CalculateExpectedChecksum(int chunkCapacity, int chunkCount)
    {
        int sum = 0;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int count = Math.Min(chunkCapacity, Program.Amount - (chunkIndex * chunkCapacity));
            for (int index = 0; index < count; index++)
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
