namespace Delta.ECS;

public delegate void QueryAction<TContext>(ref TContext context, ref QueryChunkCursor cursor);
