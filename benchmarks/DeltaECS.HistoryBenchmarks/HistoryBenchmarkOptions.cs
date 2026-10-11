namespace Delta.ECS.HistoryBenchmarks;

internal static class HistoryBenchmarkOptions
{
    internal static int EntityCount { get; set; } = ReadEntityCount();

    private static int ReadEntityCount() => int.TryParse(Environment.GetEnvironmentVariable("DELTA_ECS_HISTORY_ENTITY_COUNT"), out int entityCount)
        ? entityCount
        : 256;
}
