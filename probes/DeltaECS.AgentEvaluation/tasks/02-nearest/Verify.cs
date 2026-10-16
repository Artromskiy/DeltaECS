using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Check.That(solution.Execute() == s.Units[1], "Distance tie not resolved by stable ID");
s.World.GetRef<Health>(s.Units[1]).Value = 0;
Check.That(solution.Execute() == s.Units[0], "Dead target selected");
s.World.Add(s.Units[0], s.PausedId);
Check.That(solution.Execute() == s.Units[2], "Paused target selected");
Entity extra = s.SpawnUnit(new Vector2(0.5f, 0), 10, 999);
Check.That(solution.Execute() == extra, "New target not considered");
foreach (Entity e in s.Units.Append(extra)) s.World.GetRef<Health>(e).Value = 0;
Check.That(!solution.Execute().IsValid, "Expected default when no target qualifies");

Console.WriteLine("PASS 02-nearest");
