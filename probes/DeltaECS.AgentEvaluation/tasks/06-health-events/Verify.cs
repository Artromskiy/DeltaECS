using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
solution.Execute();
Check.That(solution.Changes.Count == s.Units.Length && solution.Changes.All(n => n.Kind == ChangeKind.Added), "Initial baseline mismatch");
solution.Execute();
Check.That(solution.Changes.Count == 0, "Unchanged repeat produced events");
Entity added = s.SpawnHealth(80);
s.World.GetRef<Health>(s.Units[2]).Value = 99;
s.World.Remove(s.Units[1], s.HealthId);
Entity old = s.Units[3];
s.World.Destroy(old);
Entity replacement = s.SpawnHealth(70);
solution.Execute();
Check.Notices(solution.Changes, new(added, ChangeKind.Added, 80), new(s.Units[2], ChangeKind.Modified, 99), new(s.Units[1], ChangeKind.Removed, null), new(old, ChangeKind.Removed, null), new(replacement, ChangeKind.Added, 70));
solution.Execute();
Check.That(solution.Changes.Count == 0, "Events repeated");
Entity transient = s.SpawnHealth(9);
s.World.Remove(transient, s.HealthId);
s.World.Remove(s.Units[0], s.HealthId);
s.World.Add(s.Units[0], new Health(6));
solution.Execute();
Check.Notices(solution.Changes, new ChangeNotice(s.Units[0], ChangeKind.Modified, 6));

Console.WriteLine("PASS 06-health-events");
