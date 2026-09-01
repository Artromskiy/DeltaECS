namespace Delta.ECS;

/// <summary>Consumes one independently owned chunk during a parallel query.</summary>
/// <remarks>
/// The callback is invoked exactly once for each active matching chunk. The callback may
/// iterate the supplied chunk's rows, but must not perform structural world operations or
/// retain the chunk after the callback returns. ECS component storage is disjoint by chunk;
/// user-owned state captured by the callback remains the caller's responsibility.
/// </remarks>
public delegate void QueryChunkAction(QueryChunk chunk);
