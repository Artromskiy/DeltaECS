using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Check.That(solution.Execute() == 1, "Wrong first destruction count");
Check.That(!s.World.IsAlive(s.Units[3]), "Defeated enemy alive");
Check.That(solution.Execute() == 0, "Deleted twice");
s.World.GetRef<Health>(s.Units[0]).Value = -1;
s.World.GetRef<Health>(s.Units[4]).Value = -1;
Entity extra = s.SpawnUnit(Vector2.One, -5, 999);
Check.That(solution.Execute() == 2, "New defeated matches not deleted");
Check.That(!s.World.IsAlive(extra) && !s.World.IsAlive(s.Units[0]), "Targets still alive");
Check.That(s.World.IsAlive(s.Units[4]), "Paused enemy destroyed");

Console.WriteLine("PASS 10-structural");
