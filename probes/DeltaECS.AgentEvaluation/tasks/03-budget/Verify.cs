using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
solution.Execute(15);
int[] expected = { 0, 0, 15, 0, 9, 9, 8, 7 };
for (int i = 0; i < s.Units.Length; i++) Check.That(s.World.Get<Health>(s.Units[i]).Value == expected[i], "Budget or ordering mismatch");
solution.Execute(4);
Check.That(s.World.Get<Health>(s.Units[2]).Value == 11, "Second budget not fresh");
Check.That(s.World.Get<Health>(s.Units[4]).Value == 9, "Paused target damaged");
Check.That(s.World.Get<Health>(s.Units[6]).Value == 8, "Non-enemy damaged");

Console.WriteLine("PASS 03-budget");
