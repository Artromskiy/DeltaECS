using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;
using var scenario = new Scenario();
scenario.World.GetRef<Position>(scenario.Player).Value = new Vector2(4, 0);
var solution = new Solution(scenario);
Check.That(solution.Execute() == scenario.Units[2], "Ordering must measure from the player, not the origin");
System.Console.WriteLine("PASS nearest with nonzero player position");
