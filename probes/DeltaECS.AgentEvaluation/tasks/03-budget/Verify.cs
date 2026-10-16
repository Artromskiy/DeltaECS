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

using var shifted = new Scenario();
shifted.World.GetRef<Position>(shifted.Player).Value = new Vector2(4, 0);
var shiftedSolution = new Solution(shifted);
shiftedSolution.Execute(5);
Check.That(shifted.World.Get<Health>(shifted.Units[2]).Value == 15, "Damage order must be relative to the nonzero player position");
Check.That(shifted.World.Get<Health>(shifted.Units[1]).Value == 4, "Origin-nearest target was incorrectly selected");

Console.WriteLine("PASS 03-budget");
