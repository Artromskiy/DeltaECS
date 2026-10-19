using System;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s, s.HealthId);
solution.Execute();
Check.That(solution.Changes.Count == s.Units.Length, "Initial health baseline missing");
Check.That(solution.Changes.All(c => c.Kind == ChangeKind.Added), "Initial change kind incorrect");
Check.That(!s.World.Has(s.Units[3], s.EnemyId), "Initially defeated enemy was not cleaned up");
solution.Execute();
Check.That(solution.Changes.Count == 0, "Unchanged call emitted duplicate changes");

Entity old = s.Units[5];
s.World.GetRef<Health>(s.Units[0]).Value = -2;
s.World.Remove(s.Units[1], s.HealthId);
s.World.Destroy(old);
Entity reused = s.SpawnHealth(-5);
Check.That(old.Index == reused.Index && old.Generation != reused.Generation, "Test setup did not reuse the old entity index");
s.World.Add(reused, s.EnemyId);
solution.Execute();
Check.Notices(solution.Changes,
    new ChangeNotice(s.Units[0], ChangeKind.Modified, -2),
    new ChangeNotice(s.Units[1], ChangeKind.Removed, null),
    new ChangeNotice(old, ChangeKind.Removed, null),
    new ChangeNotice(reused, ChangeKind.Added, -5));
Check.That(!s.World.Has(s.Units[0], s.EnemyId) && !s.World.Has(reused, s.EnemyId), "Post-observation cleanup missed defeated enemies");
Check.That(s.World.IsAlive(reused), "Cleanup destroyed the reused entity");

s.World.Add(s.Units[1], new Health(4));
s.World.Add(s.Units[1], s.EnemyId);
s.World.Add(reused, s.EnemyId);
s.World.GetRef<Health>(reused).Value = 6;
s.World.GetRef<Health>(s.Units[0]).Value = 9;
solution.Execute();
Check.Notices(solution.Changes,
    new ChangeNotice(s.Units[0], ChangeKind.Modified, 9),
    new ChangeNotice(s.Units[1], ChangeKind.Added, 4),
    new ChangeNotice(reused, ChangeKind.Modified, 6));
Check.That(s.World.Has(s.Units[1], s.EnemyId) && s.World.Has(reused, s.EnemyId), "Positive health failed to preserve enemy membership");
Check.That(s.World.IsAlive(s.Units[0]), "Health re-addition altered liveness");
solution.Execute();
Check.That(solution.Changes.Count == 0, "Repeated call replayed prior notifications");
Console.WriteLine("PASS 17-observed-pipeline");
