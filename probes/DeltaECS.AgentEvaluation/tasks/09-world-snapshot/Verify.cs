using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
solution.Execute();
Check.That(solution.Sink.Records.Count == 27, "Initial snapshot count mismatch");
Check.That(solution.Sink.Records.Count(r => r.Id == s.PositionId) == 9, "Player position missing");
Check.That(solution.Sink.Records.Count(r => r.Id == s.ArmorId) == 2, "Optional armor membership mismatch");
Check.That(solution.Sink.Records.All(r => s.HistoryIds.Contains(r.Id)), "Unexpected snapshot registration");
s.World.GetRef<Health>(s.Units[0]).Value = 77;
s.World.GetRef<float>(s.Units[0], s.DamageId) = 42;
s.World.Remove(s.Units[1], s.ArmorId);
Entity added = s.SpawnHealth(55);
solution.Execute();
Check.That(solution.Sink.Records.Count == 27, "Snapshot not cleared or membership stale");
Check.That(solution.Sink.Records.Single(r => r.Entity == s.Units[0] && r.Id == s.HealthId).Value is Health capturedHealth && capturedHealth.Value == 77, "Health value not copied");
Check.That(solution.Sink.Records.Single(r => r.Entity == s.Units[0] && r.Id == s.DamageId).Value is float value && value == 42, "Explicit float registration lost");
Check.That(solution.Sink.Records.Single(r => r.Entity == added && r.Id == s.HealthId).Value is Health addedHealth && addedHealth.Value == 55, "New entity missing");

Console.WriteLine("PASS 09-world-snapshot");
