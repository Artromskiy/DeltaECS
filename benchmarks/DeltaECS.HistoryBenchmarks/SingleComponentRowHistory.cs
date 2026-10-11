namespace Delta.ECS.HistoryBenchmarks;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal sealed class SingleComponentRowHistory
{
    private readonly HistoryPosition[] _row;

    internal SingleComponentRowHistory(int entityCount)
    {
        _row = new HistoryPosition[entityCount];
    }

    internal void Restore()
    {
        ref HistoryPosition component = ref MemoryMarshal.GetArrayDataReference(_row);
        for (int index = 0; index < _row.Length; index++)
        {
            component.Value = index + 1;
            component = ref Unsafe.Add(ref component, 1);
        }
    }
}
