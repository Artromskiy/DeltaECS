using System;
using System.Collections.Generic;
using Delta.ECS;

namespace ApiChoiceProbe;

public sealed class SnapshotScenario : IDisposable
{
    public readonly World World = new();
    public readonly ComponentId PlateId, PlateAltId, WardId, MarkedId;
    public readonly ComponentId[] Selected;
    public readonly Entity A, B;
    public SnapshotScenario()
    {
        var r = World.Components;
        PlateId = r.Register<ResetPlate>(new SchemaId(201));
        PlateAltId = r.Register<ResetPlate>(new SchemaId(202));
        WardId = r.Register<ResetWard>(new SchemaId(203));
        MarkedId = r.Register<Marked>(new SchemaId(204));
        r.BindInterface<ResetPlate, IResettable>(); r.BindInterface<ResetWard, IResettable>();
        Selected = new[] { PlateId, PlateAltId, WardId };
        A = World.Create(Selected);
        World.Add(A, MarkedId);
        B = World.Create(PlateAltId);
        World.GetRef<ResetPlate>(A, PlateId) = new(7);
        World.GetRef<ResetPlate>(A, PlateAltId) = new(9);
        World.GetRef<ResetWard>(A) = new(3);
        World.GetRef<ResetPlate>(B, PlateAltId) = new(8);
    }
    public void Dispose() => World.Dispose();
}

public record struct ResetPlate(int Value) : IResettable
{
    public void Reset() { ResetPhaseAudit.BeforeReset(); Value = 0; }
}

public record struct ResetWard(int Value) : IResettable
{
    public void Reset() { ResetPhaseAudit.BeforeReset(); Value = 0; }
}

public static class ResetPhaseAudit
{
    private static IReadOnlyList<SnapshotRecord>? records;
    private static int expectedRecords;
    public static int ResetCalls { get; private set; }

    public static void Expect(SnapshotSink sink, int currentRecordCount)
    {
        records = sink.Records;
        expectedRecords = currentRecordCount;
        ResetCalls = 0;
    }

    public static void BeforeReset()
    {
        if (records is null || records.Count != expectedRecords)
            throw new InvalidOperationException("Reset began before the complete configured snapshot was recorded.");
        ResetCalls++;
    }
}
