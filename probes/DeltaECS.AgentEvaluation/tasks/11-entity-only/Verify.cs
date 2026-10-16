using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var s = new Scenario();
var solution = new Solution(s);
solution.Execute();
Entity[] first = s.Units.Where(e => s.World.Has<Enemy>(e) && !s.World.Has<Paused>(e)).ToArray();
Check.Set(solution.Handles, first, "Wrong entity-only result");
Check.That(solution.Handles.Count == first.Length, "Duplicate handles");
Entity added = s.World.Create(s.EnemyId);
s.World.Add(s.Units[0], s.PausedId);
solution.Execute();
Check.Set(solution.Handles, first.Where(e => e != s.Units[0]).Append(added), "Tag-only entity or live membership missing");
Check.That(solution.Handles.All(e => s.World.IsAlive(e)), "Stale handles retained");

Console.WriteLine("PASS 11-entity-only");
