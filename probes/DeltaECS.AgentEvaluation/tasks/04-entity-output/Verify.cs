using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Entity extra = s.SpawnUnit(new Vector2(1, 1), 50, 999);
solution.Execute();
Entity[] expected = s.Units.Where(e => s.World.Has<Enemy>(e) && !s.World.Has<Paused>(e)).Append(extra).ToArray();
Check.Set(solution.Touched, expected, "Touched identities mismatch");
Check.That(solution.Touched.Count == expected.Length, "Duplicate output");
Check.That(s.World.Get<Health>(s.Units[0]).Value == 7, "Health not incremented");
Check.That(s.World.Get<Health>(s.Units[3]).Value == 1, "Zero-health enemy omitted");
Check.That(s.World.Get<Health>(s.Units[4]).Value == 9, "Paused enemy changed");
solution.Execute();
Check.That(solution.Touched.Count == expected.Length, "Output not cleared");
Check.That(s.World.Get<Health>(extra).Value == 52, "New entity not updated twice");

Console.WriteLine("PASS 04-entity-output");
