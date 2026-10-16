using System;

using Delta.ECS;

namespace ApiChoiceProbe;

[Flags]
public enum ProbeGroups
{
    History = 1,
    Temp = 2,
    Transient = 4,
}

public static class GroupSetup
{
    public static void Configure(Scenario scenario)
    {
        ComponentRegistry components = scenario.World.Components;
        components.AddGroup(ProbeGroups.History, scenario.PositionId);
        components.AddGroup(ProbeGroups.History, scenario.HealthId);
        components.AddGroup(ProbeGroups.History, scenario.AmbientId);
        components.AddGroup(ProbeGroups.Temp, scenario.ArmorId);
        components.AddGroup(ProbeGroups.Temp, scenario.DamageId);
        components.AddGroup(ProbeGroups.Temp, scenario.MarkedId);
        components.AddGroup(ProbeGroups.Transient, scenario.EnergyId);
        components.AddGroup(ProbeGroups.Transient, scenario.ConfiguredHealthId);
        components.AddGroup(ProbeGroups.Transient, scenario.MarkedId);
    }
}
