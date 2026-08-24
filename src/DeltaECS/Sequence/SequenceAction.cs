namespace Delta.ECS;

/// <summary>Callback used by ordered entity-sequence execution.</summary>
public delegate void SequenceAction(Entity entity);

/// <summary>Callback used by ordered entity-sequence execution with caller-owned state.</summary>
public delegate void SequenceAction<TContext>(ref TContext context, Entity entity);
