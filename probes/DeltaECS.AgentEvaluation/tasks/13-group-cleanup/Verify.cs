using System;
using System.Linq;
using System.Numerics;

using ApiChoiceProbe;

using Delta.ECS;

using var scenario = new Scenario();
GroupSetup.Configure(scenario);
var solution = new Solution(scenario);
Entity added = scenario.SpawnUnit(new Vector2(20, 20), 50, 999);
scenario.AddConfigured(added, 7, 11);
scenario.World.Add(added, new Armor(9));
scenario.World.Add(added, scenario.MarkedId);
int population = scenario.World.AliveEntityCount;
Entity[] units = scenario.Units.Append(added).ToArray();
ComponentId[] selected = { scenario.ArmorId, scenario.EnergyId, scenario.DamageId, scenario.ConfiguredHealthId, scenario.MarkedId };

solution.Execute();
foreach (Entity entity in units)
{
    foreach (ComponentId id in selected)
    {
        Check.That(!scenario.World.Has(entity, id), "Grouped registration survived cleanup");
    }

    Check.That(scenario.World.Has<Position>(entity) && scenario.World.Has<Health>(entity), "History component removed");
    Check.That(scenario.World.Get<Noise>(entity).Value == 1234, "Unrelated component changed");
    Check.That(scenario.World.Get<float>(entity) == 999, "Primary float registration removed or changed");
}

Check.That(scenario.World.AliveEntityCount == population, "Cleanup destroyed entities");
scenario.World.Add(scenario.Units[0], new Armor(42));
scenario.World.Add(scenario.Units[0], scenario.DamageId);
scenario.World.Add(scenario.Units[0], scenario.MarkedId);
solution.Execute();
foreach (ComponentId id in selected)
{
    Check.That(!scenario.World.Has(scenario.Units[0], id), "Repeated cleanup used stale entity selection");
}

solution.Execute();
Check.That(scenario.World.Get<Health>(added).Value == 50, "Cleanup modified a protected value");
Check.That(scenario.World.AliveEntityCount == population, "Repeated cleanup changed population");
Console.WriteLine("PASS 13-group-cleanup");
