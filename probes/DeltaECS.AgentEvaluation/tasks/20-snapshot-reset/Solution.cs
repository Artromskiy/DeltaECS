using Delta.ECS;
namespace ApiChoiceProbe;

public sealed class Solution
{
    public SnapshotSink Sink { get; } = new();
    public Solution(SnapshotScenario scenario) { }
    public void Execute() => throw new NotImplementedException();
}
