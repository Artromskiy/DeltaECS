using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Entity extra = s.SpawnUnit(new Vector2(12, 8), 10, 999);
Entity[] targets = s.Units.Append(extra).ToArray();
var before = targets.ToDictionary(e => e, e => s.World.Get<Position>(e).Value);
solution.Execute();
solution.Execute();
foreach (Entity e in targets)
{
    Vector2 expected = before[e] + (s.World.Has<Paused>(e) ? Vector2.Zero : 2 * s.World.Get<Velocity>(e).Value);
    Check.That(s.World.Get<Position>(e).Value == expected, "Motion mismatch");
    Check.That(s.World.Get<Noise>(e).Value == 1234, "Noise changed");
}
Check.That(s.World.Get<Position>(s.Player).Value == Vector2.Zero, "Player moved");

Console.WriteLine("PASS 01-motion");
