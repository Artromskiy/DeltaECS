using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;
using var scenario = new Scenario();
scenario.World.GetRef<Position>(scenario.Player).Value = new Vector2(4, 0);
var solution = new Solution(scenario);
solution.Execute(5);
Check.That(scenario.World.Get<Health>(scenario.Units[2]).Value == 15, "Damage must start at the player-nearest enemy");
Check.That(scenario.World.Get<Health>(scenario.Units[1]).Value == 4, "Origin-nearest enemy incorrectly damaged");
System.Console.WriteLine("PASS budget with nonzero player position");
