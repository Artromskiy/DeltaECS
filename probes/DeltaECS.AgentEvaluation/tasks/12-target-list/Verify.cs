using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
Entity[] targets = { s.Units[7], s.Units[4], s.Units[0], s.Player };
var solution = new Solution(s, targets);
solution.Execute();
solution.Execute();
Check.That(s.World.Get<Health>(s.Units[7]).Value == 1, "Selected target not updated");
Check.That(s.World.Get<Health>(s.Units[0]).Value == 0, "Selected target not updated twice");
Check.That(s.World.Get<Health>(s.Units[4]).Value == 9, "Paused target updated");
Check.That(s.World.Get<Health>(s.Units[1]).Value == 4, "Unselected entity updated");

Console.WriteLine("PASS 12-target-list");
