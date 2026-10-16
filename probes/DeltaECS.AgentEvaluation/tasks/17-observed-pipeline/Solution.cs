using System;
using System.Collections.Generic;
using Delta.ECS;

namespace ApiChoiceProbe;

public sealed class Solution
{
    public List<ChangeNotice> Changes { get; } = new();

    public Solution(Scenario scenario, ComponentId healthId) { }

    public void Execute() => throw new NotImplementedException();
}
