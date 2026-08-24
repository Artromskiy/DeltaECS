namespace Delta.ECS;

public delegate void QueryChunkAction<TContext>(ref TContext context, ref QueryChunkCursor chunk);
