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
    private Query _query;
    private WriteAccess[] _writeBindings = Array.Empty<WriteAccess>();
    private ReadAccess[] _readBindings = Array.Empty<ReadAccess>();
    private LegacyProfileBackend _legacy = null!;

    private long _checksum;
    private int _iterations;
    private ProfileState _profileState;

    private struct ProfileValue
    {
        public float X;
        public float Y;
    }

    private struct ProfileState
    {
        public int ComponentCount;
        public long Checksum;
        public WriteAccess[] WriteBindings;
        public ReadAccess[] ReadBindings;
    }

    [GlobalSetup]
    public void Setup()
    {
        var layouts = new ComponentLayoutRegistry();
        _components = new ComponentId[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _components[i] = i switch
            {
                0 => layouts.Register(typeof(ProfileValue), new SchemaId(50_001)),
                1 => layouts.Register(typeof(ProfileValue), new SchemaId(50_002)),
                2 => layouts.Register(typeof(ProfileValue), new SchemaId(50_003)),
                3 => layouts.Register(typeof(ProfileValue), new SchemaId(50_004)),
                4 => layouts.Register(typeof(ProfileValue), new SchemaId(50_005)),
                5 => layouts.Register(typeof(ProfileValue), new SchemaId(50_006)),
                6 => layouts.Register(typeof(ProfileValue), new SchemaId(50_007)),
                _ => layouts.Register(typeof(ProfileValue), new SchemaId(50_008)),
            };
        }

        _world = new World(layouts, initialEntityCapacity: Amount);
        var entities = new Entity[Amount];
        _world.Create(_components, entities);
        for (var i = 0; i < Amount; i++)
        {
            var entity = entities[i];
            for (var componentIndex = 0; componentIndex < _components.Length; componentIndex++)
            {
                _world.Set(entity, _components[componentIndex], new ProfileValue { X = 1, Y = 2 });
            }
        }

        var spec = QuerySpec.WhereAll(_components);
        _query = _world.CreateQuery(in spec);
        _writeBindings = new WriteAccess[ComponentCount];
        _readBindings = new ReadAccess[ComponentCount];
        for (var i = 0; i < ComponentCount; i++)
        {
            _writeBindings[i] = _query.AccessWrite(_components[i]);
            _readBindings[i] = _query.AccessRead(_components[i]);
        }
        _legacy = new LegacyProfileBackend(ComponentCount, Amount);
    }

    [Benchmark]
    public void DeltaArray_EntityMajor_Profile()
    {
        _profileState = new ProfileState { ComponentCount = ComponentCount, WriteBindings = _writeBindings, ReadBindings = _readBindings };
        _iterations = RunUntilDuration(IterateEntityMajor);
        _checksum = _profileState.Checksum;
    }

    [Benchmark]
    public void DeltaArray_RowMajor_Profile()
    {
        _profileState = new ProfileState { ComponentCount = ComponentCount, WriteBindings = _writeBindings, ReadBindings = _readBindings };
        _iterations = RunUntilDuration(IterateRowMajor);
        _checksum = _profileState.Checksum;
    }

    [Benchmark]
    public void DeltaArray_DispatchOnly_Profile()
    {
        _profileState = new ProfileState { ReadBindings = _readBindings };
        _iterations = RunUntilDuration(DispatchOnly);
        _checksum = _profileState.Checksum;
    }

    [Benchmark]
    public void DeltaArray_LookupOnly_Profile()
    {
        _profileState = new ProfileState { ComponentCount = ComponentCount, ReadBindings = _readBindings, WriteBindings = _writeBindings };
        _iterations = RunUntilDuration(LookupOnly);
        _checksum = _profileState.Checksum;
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

    private void IterateEntityMajor()
    {
        ref var state = ref _profileState;
        switch (state.ComponentCount)
        {
            case 1:
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[0], static (ref ProfileState profile, ref ProfileValue p0) =>
                {
                    p0.X += p0.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                });
                break;
            case 2:
                _world.ForEach<ProfileState, ProfileValue, ProfileValue>(in _query, ref state, _components[0], _components[1], static (ref ProfileState profile, ref ProfileValue p0, ref ProfileValue p1) =>
                {
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                });
                break;
            case 4:
                _world.ForEach<ProfileState, ProfileValue, ProfileValue, ProfileValue, ProfileValue>(in _query, ref state, _components[0], _components[1], _components[2], _components[3], static (ref ProfileState profile, ref ProfileValue p0, ref ProfileValue p1, ref ProfileValue p2, ref ProfileValue p3) =>
                {
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    profile.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                });
                break;
            case 8:
                _world.ForEach<ProfileState, ProfileValue, ProfileValue, ProfileValue, ProfileValue, ProfileValue, ProfileValue, ProfileValue, ProfileValue>(in _query, ref state, _components[0], _components[1], _components[2], _components[3], _components[4], _components[5], _components[6], _components[7], static (ref ProfileState profile, ref ProfileValue p0, ref ProfileValue p1, ref ProfileValue p2, ref ProfileValue p3, ref ProfileValue p4, ref ProfileValue p5, ref ProfileValue p6, ref ProfileValue p7) =>
                {
                    p0.X += p0.Y;
                    p1.X += p1.Y;
                    p2.X += p2.Y;
                    p3.X += p3.Y;
                    p4.X += p4.Y;
                    p5.X += p5.Y;
                    p6.X += p6.Y;
                    p7.X += p7.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X) + BitConverter.SingleToInt32Bits(p1.X);
                    profile.Checksum += BitConverter.SingleToInt32Bits(p2.X) + BitConverter.SingleToInt32Bits(p3.X);
                    profile.Checksum += BitConverter.SingleToInt32Bits(p4.X) + BitConverter.SingleToInt32Bits(p5.X);
                    profile.Checksum += BitConverter.SingleToInt32Bits(p6.X) + BitConverter.SingleToInt32Bits(p7.X);
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    private void IterateRowMajor()
    {
        ref var state = ref _profileState;
        switch (state.ComponentCount)
        {
            case 1:
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[0], static (ref ProfileState profile, ref ProfileValue p0) =>
                {
                    p0.X += p0.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                });
                break;
            case 2:
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[0], static (ref ProfileState profile, ref ProfileValue p0) =>
                {
                    p0.X += p0.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[1], static (ref ProfileState profile, ref ProfileValue p1) =>
                {
                    p1.X += p1.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p1.X);
                });
                break;
            case 4:
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[0], static (ref ProfileState profile, ref ProfileValue p0) =>
                {
                    p0.X += p0.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[1], static (ref ProfileState profile, ref ProfileValue p1) =>
                {
                    p1.X += p1.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p1.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[2], static (ref ProfileState profile, ref ProfileValue p2) =>
                {
                    p2.X += p2.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p2.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[3], static (ref ProfileState profile, ref ProfileValue p3) =>
                {
                    p3.X += p3.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p3.X);
                });
                break;
            case 8:
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[0], static (ref ProfileState profile, ref ProfileValue p0) =>
                {
                    p0.X += p0.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p0.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[1], static (ref ProfileState profile, ref ProfileValue p1) =>
                {
                    p1.X += p1.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p1.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[2], static (ref ProfileState profile, ref ProfileValue p2) =>
                {
                    p2.X += p2.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p2.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[3], static (ref ProfileState profile, ref ProfileValue p3) =>
                {
                    p3.X += p3.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p3.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[4], static (ref ProfileState profile, ref ProfileValue p4) =>
                {
                    p4.X += p4.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p4.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[5], static (ref ProfileState profile, ref ProfileValue p5) =>
                {
                    p5.X += p5.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p5.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[6], static (ref ProfileState profile, ref ProfileValue p6) =>
                {
                    p6.X += p6.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p6.X);
                });
                _world.ForEach<ProfileState, ProfileValue>(in _query, ref state, _components[7], static (ref ProfileState profile, ref ProfileValue p7) =>
                {
                    p7.X += p7.Y;
                    profile.Checksum += BitConverter.SingleToInt32Bits(p7.X);
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
        }
    }

    private void LookupOnly()
    {
        ref var state = ref _profileState;
        using var scope = _world.OpenQuery(in _query);
        var r0 = default(ReadAccess);
        var r1 = default(ReadAccess);
        var r2 = default(ReadAccess);
        var r3 = default(ReadAccess);
        var r4 = default(ReadAccess);
        var r5 = default(ReadAccess);
        var r6 = default(ReadAccess);
        var r7 = default(ReadAccess);

        if (state.ComponentCount > 0) r0 = state.ReadBindings[0];
        if (state.ComponentCount > 1) r1 = state.ReadBindings[1];
        if (state.ComponentCount > 2) r2 = state.ReadBindings[2];
        if (state.ComponentCount > 3) r3 = state.ReadBindings[3];
        if (state.ComponentCount > 4) r4 = state.ReadBindings[4];
        if (state.ComponentCount > 5) r5 = state.ReadBindings[5];
        if (state.ComponentCount > 6) r6 = state.ReadBindings[6];
        if (state.ComponentCount > 7) r7 = state.ReadBindings[7];

        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                var chunk = chunks.Current;
                var slots = chunk.Slots;
                switch (state.ComponentCount)
                {
                    case 1:
                        _ = slots.GetRow(r0);
                        break;
                    case 2:
                        _ = slots.GetRow(r0);
                        _ = slots.GetRow(r1);
                        break;
                    case 4:
                        _ = slots.GetRow(r0);
                        _ = slots.GetRow(r1);
                        _ = slots.GetRow(r2);
                        _ = slots.GetRow(r3);
                        break;
                    case 8:
                        _ = slots.GetRow(r0);
                        _ = slots.GetRow(r1);
                        _ = slots.GetRow(r2);
                        _ = slots.GetRow(r3);
                        _ = slots.GetRow(r4);
                        _ = slots.GetRow(r5);
                        _ = slots.GetRow(r6);
                        _ = slots.GetRow(r7);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state.ComponentCount));
                }

                state.Checksum += chunk.SlotCount;
            }
        }
    }

    private void DispatchOnly()
    {
        ref var state = ref _profileState;
        using var scope = _world.OpenQuery(in _query);
        var archetypes = scope.Archetypes;
        while (archetypes.MoveNext())
        {
            var chunks = archetypes.Current.Chunks;
            while (chunks.MoveNext())
            {
                state.Checksum += chunks.Current.SlotCount;
            }
        }
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
