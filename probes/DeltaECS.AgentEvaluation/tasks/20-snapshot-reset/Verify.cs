using System;
using System.Linq;

using ApiChoiceProbe;

using Delta.ECS;
using var s = new SnapshotScenario();
var solution = new Solution(s);
ResetPhaseAudit.Expect(solution.Sink, 4);
solution.Execute();
CheckRecords(solution, "initial complete pre-reset snapshot", new SnapshotRecord(s.A, s.PlateId, new ResetPlate(7)), new SnapshotRecord(s.A, s.PlateAltId, new ResetPlate(9)), new SnapshotRecord(s.A, s.WardId, new ResetWard(3)), new SnapshotRecord(s.B, s.PlateAltId, new ResetPlate(8)));
Check.That(ResetPhaseAudit.ResetCalls == 3, "unexpected selected reset count");
Check.That(s.World.Get<ResetPlate>(s.A, s.PlateId).Value == 0 && s.World.Get<ResetPlate>(s.A, s.PlateAltId).Value == 0 && s.World.Get<ResetWard>(s.A).Value == 0, "marked reset failed");
Check.That(s.World.Get<ResetPlate>(s.B, s.PlateAltId).Value == 8, "unmarked reset occurred");
ResetPhaseAudit.Expect(solution.Sink, 4);
solution.Execute();
CheckRecords(solution, "unchanged repeat must still snapshot current values", new SnapshotRecord(s.A, s.PlateId, new ResetPlate(0)), new SnapshotRecord(s.A, s.PlateAltId, new ResetPlate(0)), new SnapshotRecord(s.A, s.WardId, new ResetWard(0)), new SnapshotRecord(s.B, s.PlateAltId, new ResetPlate(8)));
Check.That(ResetPhaseAudit.ResetCalls == 3, "unexpected repeated reset count");
s.World.GetRef<ResetPlate>(s.A, s.PlateId).Value = 5;
s.World.Remove(s.B, s.PlateAltId);
Entity added = s.World.Create(s.WardId);
s.World.GetRef<ResetWard>(added) = new(11);
ResetPhaseAudit.Expect(solution.Sink, 4);
solution.Execute();
CheckRecords(solution, "live membership and pre-reset current values", new SnapshotRecord(s.A, s.PlateId, new ResetPlate(5)), new SnapshotRecord(s.A, s.PlateAltId, new ResetPlate(0)), new SnapshotRecord(s.A, s.WardId, new ResetWard(0)), new SnapshotRecord(added, s.WardId, new ResetWard(11)));
Check.That(ResetPhaseAudit.ResetCalls == 3, "unexpected live reset count");
Check.That(s.World.Get<ResetPlate>(s.A, s.PlateId).Value == 0, "reset following snapshot failed");
Console.WriteLine("PASS 20-snapshot-reset");

static void CheckRecords(Solution solution, string label, params SnapshotRecord[] expected)
{
    Check.That(solution.Sink.Records.Count == expected.Length, $"{label}: record count differs");
    Check.That(new System.Collections.Generic.HashSet<SnapshotRecord>(solution.Sink.Records).SetEquals(expected), $"{label}: registration, entity or value differs");
}
