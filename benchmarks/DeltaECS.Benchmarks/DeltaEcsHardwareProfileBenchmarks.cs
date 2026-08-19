using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Delta.ECS;

namespace Delta.ECS.Benchmarks;

[MemoryDiagnoser]
[HardwareCounters(
    HardwareCounter.TotalCycles,
    HardwareCounter.InstructionRetired,
    HardwareCounter.BranchInstructions,
    HardwareCounter.BranchMispredictions,
    HardwareCounter.CacheMisses,
    HardwareCounter.LlcReference,
    HardwareCounter.LlcMisses,
    HardwareCounter.BranchInstructionRetired,
    HardwareCounter.BranchMispredictsRetired)]
[SimpleJob]
public class HardwareProfileBenchmarks
{
    private const int TargetProfileMilliseconds = 1200;

[Params(100, 1_000, 10_000, 100_000, 1_000_000)]
public int Amount { get; set; }

    [Params(1, 2, 4, 8)]
    public int ComponentCount { get; set; }

    private World _world = null!;
    private ComponentId[] _components = Array.Empty<ComponentId>();
    private QueryHandle _query;
    private LegacyProfileBackend _legacy = null!;

    private long _checksum;
    private int _iterations;

    private struct ProfileValue
    {
        public float X;
        public float Y;
    }

    private struct ProfileState
    {
        public int ComponentCount;
        public long Checksum;
    }

    private static readonly ChunkAction<ProfileState> s_entityMajor = IterateEntityMajor;
    private static readonly ChunkAction<ProfileState> s_rowMajor = IterateRowMajor;
    private static readonly ChunkAction<ProfileState> s_lookupOnly = LookupOnly;
    private static readonly ChunkAction<ProfileState> s_dispatchOnly = DispatchOnly;

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _components = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _components[i] = i switch
            {
                0 => layouts.Register<ProfileValue>(new SchemaId(50_001)),
                1 => layouts.Register<ProfileValue>(new SchemaId(50_002)),
                2 => layouts.Register<ProfileValue>(new SchemaId(50_003)),
                3 => layouts.Register<ProfileValue>(new SchemaId(50_004)),
                4 => layouts.Register<ProfileValue>(new SchemaId(50_005)),
                5 => layouts.Register<ProfileValue>(new SchemaId(50_006)),
                6 => layouts.Register<ProfileValue>(new SchemaId(50_007)),
                _ => layouts.Register<ProfileValue>(new SchemaId(50_008)),
            };
        }

        _world = new World(layouts, initialEntityCapacity: Amount);
        var entities = new Entity[Amount];
        _world.CreateBatch(_components, entities);
        for (var i = 0; i < Amount; i++)
        {
            var entity = entities[i];
            for (var componentIndex = 0; componentIndex < _components.Length; componentIndex++)
            {
                _world.SetComponent(entity, _components[componentIndex], new ProfileValue { X = 1, Y = 2 });
            }
        }

        var description = QueryDescription.ForComponents(_components);
        _query = _world.CreateQuery(in description);
        _legacy = new LegacyProfileBackend(ComponentCount, Amount);
    }

    [Benchmark]
    public void DeltaArray_EntityMajor_Profile()
    {
        var state = new ProfileState { ComponentCount = ComponentCount };
        _iterations = RunUntilDuration(() => _world.Query(in _query, QueryAccess.Write, ref state, s_entityMajor));
        _checksum = state.Checksum;
    }

    [Benchmark]
    public void DeltaArray_RowMajor_Profile()
    {
        var state = new ProfileState { ComponentCount = ComponentCount };
        _iterations = RunUntilDuration(() => _world.Query(in _query, QueryAccess.Write, ref state, s_rowMajor));
        _checksum = state.Checksum;
    }

    [Benchmark]
    public void DeltaArray_DispatchOnly_Profile()
    {
        var state = new ProfileState();
        _iterations = RunUntilDuration(() => _world.Query(in _query, QueryAccess.Read, ref state, s_dispatchOnly));
        _checksum = state.Checksum;
    }

    [Benchmark]
    public void DeltaArray_LookupOnly_Profile()
    {
        var state = new ProfileState { ComponentCount = ComponentCount };
        _iterations = RunUntilDuration(() => _world.Query(in _query, QueryAccess.Read, ref state, s_lookupOnly));
        _checksum = state.Checksum;
    }

    [Benchmark]
    public void LegacyEntity_EntityMajor_Profile()
    {
        _iterations = 0;
        var checksum = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < TargetProfileMilliseconds)
        {
            checksum += _legacy.IterateEntityMajor(ComponentCount);
            _iterations++;
        }

        _checksum = checksum;
    }

    [Benchmark]
    public void Legacy_RowMajor_Profile()
    {
        _iterations = 0;
        var checksum = 0L;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < TargetProfileMilliseconds)
        {
            checksum += _legacy.IterateRowMajor(ComponentCount);
            _iterations++;
        }

        _checksum = checksum;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        GC.KeepAlive(_checksum);
        GC.KeepAlive(_iterations);
    }

    private static int RunUntilDuration(Action action)
    {
        var iterations = 0;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < TargetProfileMilliseconds)
        {
            action();
            iterations++;
        }

        return iterations;
    }

    private static void IterateEntityMajor(ref ProfileState state, ref DenseChunkAccessor lease)
    {
        switch (state.ComponentCount)
        {
            case 1:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                for (var slotIndex = lease.SlotCount - 1; slotIndex >= 0; slotIndex--)
                {
                    var value = c0[slotIndex];
                    value.X += value.Y;
                    c0[slotIndex] = value;
                    state.Checksum += BitConverter.SingleToInt32Bits(value.X);
                }
                return;
            }
            case 2:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                for (var slotIndex = lease.SlotCount - 1; slotIndex >= 0; slotIndex--)
                {
                    var p0 = c0[slotIndex];
                    var p1 = c1[slotIndex];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    c0[slotIndex] = p0;
                    c1[slotIndex] = p1;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                }
                return;
            }
            case 4:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                var c2 = lease.GetComponentRow<ProfileValue>(2);
                var c3 = lease.GetComponentRow<ProfileValue>(3);
                for (var slotIndex = lease.SlotCount - 1; slotIndex >= 0; slotIndex--)
                {
                    var p0 = c0[slotIndex];
                    var p1 = c1[slotIndex];
                    var p2 = c2[slotIndex];
                    var p3 = c3[slotIndex];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    c0[slotIndex] = p0;
                    c1[slotIndex] = p1;
                    c2[slotIndex] = p2;
                    c3[slotIndex] = p3;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                }
                return;
            }
            case 8:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                var c2 = lease.GetComponentRow<ProfileValue>(2);
                var c3 = lease.GetComponentRow<ProfileValue>(3);
                var c4 = lease.GetComponentRow<ProfileValue>(4);
                var c5 = lease.GetComponentRow<ProfileValue>(5);
                var c6 = lease.GetComponentRow<ProfileValue>(6);
                var c7 = lease.GetComponentRow<ProfileValue>(7);
                for (var slotIndex = lease.SlotCount - 1; slotIndex >= 0; slotIndex--)
                {
                    var p0 = c0[slotIndex];
                    var p1 = c1[slotIndex];
                    var p2 = c2[slotIndex];
                    var p3 = c3[slotIndex];
                    var p4 = c4[slotIndex];
                    var p5 = c5[slotIndex];
                    var p6 = c6[slotIndex];
                    var p7 = c7[slotIndex];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    p4.X += p4.Y;
                    p5.X += p5.Y;
                    p6.X += p6.Y;
                    p7.X += p7.Y;
                    c0[slotIndex] = p0;
                    c1[slotIndex] = p1;
                    c2[slotIndex] = p2;
                    c3[slotIndex] = p3;
                    c4[slotIndex] = p4;
                    c5[slotIndex] = p5;
                    c6[slotIndex] = p6;
                    c7[slotIndex] = p7;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p4.X) + BitConverter.SingleToInt32Bits(p5.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p6.X) + BitConverter.SingleToInt32Bits(p7.X);
                }
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    private static void IterateRowMajor(ref ProfileState state, ref DenseChunkAccessor lease)
    {
        switch (state.ComponentCount)
        {
            case 1:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                for (var i = c0.Length - 1; i >= 0; i--)
                {
                    var p0 = c0[i];
                    p0.X += p0.Y;
                    c0[i] = p0;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                }
                return;
            }
            case 2:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                for (var i = c0.Length - 1; i >= 0; i--)
                {
                    var p0 = c0[i];
                    var p1 = c1[i];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    c0[i] = p0;
                    c1[i] = p1;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                }
                return;
            }
            case 4:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                var c2 = lease.GetComponentRow<ProfileValue>(2);
                var c3 = lease.GetComponentRow<ProfileValue>(3);
                for (var i = c0.Length - 1; i >= 0; i--)
                {
                    var p0 = c0[i];
                    var p1 = c1[i];
                    var p2 = c2[i];
                    var p3 = c3[i];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    c0[i] = p0;
                    c1[i] = p1;
                    c2[i] = p2;
                    c3[i] = p3;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                }
                return;
            }
            case 8:
            {
                var c0 = lease.GetComponentRow<ProfileValue>(0);
                var c1 = lease.GetComponentRow<ProfileValue>(1);
                var c2 = lease.GetComponentRow<ProfileValue>(2);
                var c3 = lease.GetComponentRow<ProfileValue>(3);
                var c4 = lease.GetComponentRow<ProfileValue>(4);
                var c5 = lease.GetComponentRow<ProfileValue>(5);
                var c6 = lease.GetComponentRow<ProfileValue>(6);
                var c7 = lease.GetComponentRow<ProfileValue>(7);
                for (var i = c0.Length - 1; i >= 0; i--)
                {
                    var p0 = c0[i];
                    var p1 = c1[i];
                    var p2 = c2[i];
                    var p3 = c3[i];
                    var p4 = c4[i];
                    var p5 = c5[i];
                    var p6 = c6[i];
                    var p7 = c7[i];
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    p4.X += p4.Y;
                    p5.X += p5.Y;
                    p6.X += p6.Y;
                    p7.X += p7.Y;
                    c0[i] = p0;
                    c1[i] = p1;
                    c2[i] = p2;
                    c3[i] = p3;
                    c4[i] = p4;
                    c5[i] = p5;
                    c6[i] = p6;
                    c7[i] = p7;
                    state.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p4.X) + BitConverter.SingleToInt32Bits(p5.X);
                    state.Checksum += BitConverter.SingleToInt32Bits(p6.X) + BitConverter.SingleToInt32Bits(p7.X);
                }
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    private static void LookupOnly(ref ProfileState state, ref DenseChunkAccessor lease)
    {
        switch (state.ComponentCount)
        {
            case 1:
                _ = lease.GetComponentRow<ProfileValue>(0);
                break;
            case 2:
                _ = lease.GetComponentRow<ProfileValue>(0);
                _ = lease.GetComponentRow<ProfileValue>(1);
                break;
            case 4:
                _ = lease.GetComponentRow<ProfileValue>(0);
                _ = lease.GetComponentRow<ProfileValue>(1);
                _ = lease.GetComponentRow<ProfileValue>(2);
                _ = lease.GetComponentRow<ProfileValue>(3);
                break;
            case 8:
                _ = lease.GetComponentRow<ProfileValue>(0);
                _ = lease.GetComponentRow<ProfileValue>(1);
                _ = lease.GetComponentRow<ProfileValue>(2);
                _ = lease.GetComponentRow<ProfileValue>(3);
                _ = lease.GetComponentRow<ProfileValue>(4);
                _ = lease.GetComponentRow<ProfileValue>(5);
                _ = lease.GetComponentRow<ProfileValue>(6);
                _ = lease.GetComponentRow<ProfileValue>(7);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }

        state.Checksum += lease.SlotCount;
    }

    private static void DispatchOnly(ref ProfileState state, ref DenseChunkAccessor lease)
    {
        state.Checksum += lease.SlotCount;
    }
}

internal sealed class LegacyProfileBackend
{
    private const int ChunkCapacity = 1024;

    private readonly byte[][][] _chunks;
    private readonly int[] _sizes;

    public LegacyProfileBackend(int rowCount, int amount)
    {
        var chunkCount = (amount + ChunkCapacity - 1) / ChunkCapacity;
        _chunks = new byte[chunkCount][][];
        _sizes = new int[chunkCount];

        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var size = Math.Min(ChunkCapacity, amount - chunkIndex * ChunkCapacity);
            _sizes[chunkIndex] = size;
            _chunks[chunkIndex] = new byte[rowCount][];
            for (var row = 0; row < rowCount; row++)
            {
                var rowBytes = size * sizeof(float) * 2;
                _chunks[chunkIndex][row] = new byte[rowBytes];
                var values = CastLegacyRow(_chunks[chunkIndex][row]);
                for (var slotIndex = 0; slotIndex < size; slotIndex++)
                {
                    values[slotIndex] = new LegacyValue { X = 1, Y = 2 };
                }
            }
        }
    }

    public long IterateEntityMajor(int rowCount)
    {
        var checksum = 0L;

        for (var chunkIndex = 0; chunkIndex < _chunks.Length; chunkIndex++)
        {
            var rows = _chunks[chunkIndex];
            var size = _sizes[chunkIndex];

            switch (rowCount)
            {
                case 1:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var value = row0[slotIndex];
                        value.X += value.Y;
                        row0[slotIndex] = value;
                        checksum += BitConverter.SingleToInt32Bits(value.X);
                    }
                    break;
                }
                case 2:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var p0 = row0[slotIndex];
                        var p1 = row1[slotIndex];
                        p0.X += p0.Y;
                        p1.X += p1.Y;
                        row0[slotIndex] = p0;
                        row1[slotIndex] = p1;
                        checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    }
                    break;
                }
                case 4:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    var row2 = CastLegacyRow(rows[2]);
                    var row3 = CastLegacyRow(rows[3]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var p0 = row0[slotIndex];
                        var p1 = row1[slotIndex];
                        var p2 = row2[slotIndex];
                        var p3 = row3[slotIndex];
                        p0.X += p0.Y;
                        p1.X += p1.Y;
                        p2.X += p2.Y;
                        p3.X += p3.Y;
                        row0[slotIndex] = p0;
                        row1[slotIndex] = p1;
                        row2[slotIndex] = p2;
                        row3[slotIndex] = p3;
                        checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                        checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                    }
                    break;
                }
                case 8:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    var row2 = CastLegacyRow(rows[2]);
                    var row3 = CastLegacyRow(rows[3]);
                    var row4 = CastLegacyRow(rows[4]);
                    var row5 = CastLegacyRow(rows[5]);
                    var row6 = CastLegacyRow(rows[6]);
                    var row7 = CastLegacyRow(rows[7]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var p0 = row0[slotIndex];
                        var p1 = row1[slotIndex];
                        var p2 = row2[slotIndex];
                        var p3 = row3[slotIndex];
                        var p4 = row4[slotIndex];
                        var p5 = row5[slotIndex];
                        var p6 = row6[slotIndex];
                        var p7 = row7[slotIndex];
                        p0.X += p0.Y;
                        p1.X += p1.Y;
                        p2.X += p2.Y;
                        p3.X += p3.Y;
                        p4.X += p4.Y;
                        p5.X += p5.Y;
                        p6.X += p6.Y;
                        p7.X += p7.Y;
                        row0[slotIndex] = p0;
                        row1[slotIndex] = p1;
                        row2[slotIndex] = p2;
                        row3[slotIndex] = p3;
                        row4[slotIndex] = p4;
                        row5[slotIndex] = p5;
                        row6[slotIndex] = p6;
                        row7[slotIndex] = p7;
                        checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                        checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                        checksum += BitConverter.SingleToInt32Bits(p4.X) + BitConverter.SingleToInt32Bits(p5.X);
                        checksum += BitConverter.SingleToInt32Bits(p6.X) + BitConverter.SingleToInt32Bits(p7.X);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(rowCount));
            }
        }

        return checksum;
    }

    public long IterateRowMajor(int rowCount)
    {
        var checksum = 0L;

        for (var chunkIndex = 0; chunkIndex < _chunks.Length; chunkIndex++)
        {
            var rows = _chunks[chunkIndex];
            var size = _sizes[chunkIndex];

            switch (rowCount)
            {
                case 1:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var value = row0[slotIndex];
                        value.X += value.Y;
                        row0[slotIndex] = value;
                        checksum += BitConverter.SingleToInt32Bits(value.X);
                    }
                    break;
                }
                case 2:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var value0 = row0[slotIndex];
                        value0.X += value0.Y;
                        row0[slotIndex] = value0;
                        var value1 = row1[slotIndex];
                        value1.X += value1.Y;
                        row1[slotIndex] = value1;
                        checksum += BitConverter.SingleToInt32Bits(value0.X) + BitConverter.SingleToInt32Bits(value1.X);
                    }
                    break;
                }
                case 4:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    var row2 = CastLegacyRow(rows[2]);
                    var row3 = CastLegacyRow(rows[3]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var value0 = row0[slotIndex];
                        value0.X += value0.Y;
                        row0[slotIndex] = value0;
                        var value1 = row1[slotIndex];
                        value1.X += value1.Y;
                        row1[slotIndex] = value1;
                        var value2 = row2[slotIndex];
                        value2.X += value2.Y;
                        row2[slotIndex] = value2;
                        var value3 = row3[slotIndex];
                        value3.X += value3.Y;
                        row3[slotIndex] = value3;
                        checksum += BitConverter.SingleToInt32Bits(value0.X) + BitConverter.SingleToInt32Bits(value1.X);
                        checksum += BitConverter.SingleToInt32Bits(value2.X) + BitConverter.SingleToInt32Bits(value3.X);
                    }
                    break;
                }
                case 8:
                {
                    var row0 = CastLegacyRow(rows[0]);
                    var row1 = CastLegacyRow(rows[1]);
                    var row2 = CastLegacyRow(rows[2]);
                    var row3 = CastLegacyRow(rows[3]);
                    var row4 = CastLegacyRow(rows[4]);
                    var row5 = CastLegacyRow(rows[5]);
                    var row6 = CastLegacyRow(rows[6]);
                    var row7 = CastLegacyRow(rows[7]);
                    for (var slotIndex = size - 1; slotIndex >= 0; slotIndex--)
                    {
                        var value0 = row0[slotIndex];
                        value0.X += value0.Y;
                        row0[slotIndex] = value0;
                        var value1 = row1[slotIndex];
                        value1.X += value1.Y;
                        row1[slotIndex] = value1;
                        var value2 = row2[slotIndex];
                        value2.X += value2.Y;
                        row2[slotIndex] = value2;
                        var value3 = row3[slotIndex];
                        value3.X += value3.Y;
                        row3[slotIndex] = value3;
                        var value4 = row4[slotIndex];
                        value4.X += value4.Y;
                        row4[slotIndex] = value4;
                        var value5 = row5[slotIndex];
                        value5.X += value5.Y;
                        row5[slotIndex] = value5;
                        var value6 = row6[slotIndex];
                        value6.X += value6.Y;
                        row6[slotIndex] = value6;
                        var value7 = row7[slotIndex];
                        value7.X += value7.Y;
                        row7[slotIndex] = value7;
                        checksum += BitConverter.SingleToInt32Bits(value0.X) + BitConverter.SingleToInt32Bits(value1.X);
                        checksum += BitConverter.SingleToInt32Bits(value2.X) + BitConverter.SingleToInt32Bits(value3.X);
                        checksum += BitConverter.SingleToInt32Bits(value4.X) + BitConverter.SingleToInt32Bits(value5.X);
                        checksum += BitConverter.SingleToInt32Bits(value6.X) + BitConverter.SingleToInt32Bits(value7.X);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(rowCount));
            }
        }

        return checksum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<LegacyValue> CastLegacyRow(byte[] rowBytes)
    {
        return MemoryMarshal.Cast<byte, LegacyValue>(rowBytes.AsSpan());
    }

    private struct LegacyValue
    {
        public float X;
        public float Y;
    }
}
