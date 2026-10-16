using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
Entity extra = s.World.Create(new ComponentId[] { s.AmbientId, s.DamageId, s.ConfiguredHealthId });
s.World.GetRef<float>(extra, s.AmbientId) = 999;
s.World.GetRef<float>(extra, s.DamageId) = 7;
s.World.GetRef<float>(extra, s.ConfiguredHealthId) = 5;
solution.Execute();
solution.Execute();
for (int i = 0; i < s.Units.Length; i++)
{
    Check.That(s.World.Get<float>(s.Units[i], s.ConfiguredHealthId) == Math.Max(0, 6 - i), "Wrong configured health");
    Check.That(s.World.Get<float>(s.Units[i], s.DamageId) == i + 2, "Damage row modified");
    Check.That(s.World.Get<float>(s.Units[i], s.AmbientId) == 999, "Primary float row modified");
}
Check.That(s.World.Get<float>(extra, s.ConfiguredHealthId) == 0, "New configured-only entity missed");

Console.WriteLine("PASS 05-configured-rows");
