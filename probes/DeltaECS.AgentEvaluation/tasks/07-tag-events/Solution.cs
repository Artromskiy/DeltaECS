using System;
using System.Collections.Generic;
using System.Numerics;

using Delta.ECS;

namespace ApiChoiceProbe;

public sealed class Solution
{
    public IReadOnlyList<ChangeNotice> Changes => throw new NotImplementedException();
    public Solution(Scenario scenario) { }
    public void Execute() => throw new NotImplementedException();
}
