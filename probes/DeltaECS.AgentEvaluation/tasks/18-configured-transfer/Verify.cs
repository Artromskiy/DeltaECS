using System;
using System.Linq;

using ApiChoiceProbe;

using Delta.ECS;
using var s = new Scenario();
var solution = new Solution(s);
// Make a deterministic tie so the stable-ID key decides which row is visited first.
s.World.GetRef<float>(s.Units[0], s.DamageId) = 3;
s.World.GetRef<float>(s.Units[1], s.DamageId) = 3;
for (int i = 2; i < s.Units.Length; i++) s.World.GetRef<float>(s.Units[i], s.DamageId) = 0;
solution.Execute(4);
Check.That(s.World.Get<float>(s.Units[1], s.DamageId) == 0, "stable-id tie ordering not honored");
Check.That(s.World.Get<float>(s.Units[1], s.ConfiguredHealthId) == 14, "wrong first transfer");
Check.That(s.World.Get<float>(s.Units[0], s.DamageId) == 2, "remaining budget did not stop inside second target");
Check.That(s.World.Get<float>(s.Units[0], s.ConfiguredHealthId) == 11, "wrong tie transfer");
Check.That(s.World.Get<float>(s.Units[2], s.DamageId) == 0, "ineligible source row changed");
s.World.GetRef<float>(s.Units[0], s.DamageId) = 0;
s.World.GetRef<float>(s.Units[2], s.DamageId) = 15;
s.World.GetRef<float>(s.Units[2], s.ConfiguredHealthId) = 19;
Entity added = s.World.Create(new[] { s.DamageId, s.ConfiguredHealthId, s.AmbientId, s.StableIdId });
s.World.GetRef<float>(added, s.DamageId) = 8;
s.World.GetRef<float>(added, s.ConfiguredHealthId) = 19;
s.World.GetRef<float>(added, s.AmbientId) = 999;
s.World.GetRef<StableId>(added) = new(1);
solution.Execute(4);
Check.That(s.World.Get<float>(added, s.DamageId) == 7 && s.World.Get<float>(added, s.ConfiguredHealthId) == 20, "new eligible entity or capacity cap failed");
Check.That(s.World.Get<float>(s.Units[2], s.ConfiguredHealthId) == 20, "capacity cap failed");
Check.That(s.World.Get<float>(s.Units[2], s.DamageId) == 14, "source value after capacity-capped transfer");
Check.That(s.World.Get<float>(s.Units[0], s.AmbientId) == 999 && s.World.Get<Noise>(s.Units[0]).Value == 1234, "unrelated component changed");
Check.That(s.World.Get<float>(added, s.AmbientId) == 999, "new entity primary float registration changed");
Console.WriteLine("PASS 18-configured-transfer");
