using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Check.That(solution.Execute(s.Units[0], s.ArmorId), "Armor reset rejected");
Check.That(s.World.Get<Armor>(s.Units[0]).Value == 0, "Struct reset lost through boxing");
Check.That(s.World.Get<Energy>(s.Units[0]).Value == 3, "Unselected component changed");
Check.That(solution.Execute(s.Units[0], s.EnergyId), "Energy reset rejected");
Check.That(s.World.Get<Energy>(s.Units[0]).Value == 0, "Energy not reset");
Check.That(!solution.Execute(s.Units[2], s.ArmorId), "Absent component accepted");
Check.That(!solution.Execute(s.Units[0], s.NoiseId), "Unconfigured ID accepted");
Check.That(!solution.Execute(default, s.ArmorId), "Invalid entity accepted");

Console.WriteLine("PASS 08-runtime-dispatch");
