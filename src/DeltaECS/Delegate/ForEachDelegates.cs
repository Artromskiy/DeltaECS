namespace Delta.ECS;

public delegate void ForEachEntityAction(Entity entity);

public delegate void ForEachAction();

public delegate void ForEachContextAction<TContext>(ref TContext context);

public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);

public delegate void ForEachContextActionIn<TContext>(in TContext context);

public delegate void ForEachContextEntityActionIn<TContext>(in TContext context, Entity entity);

public delegate void ForEachContextActionValue<TContext>(TContext context);

public delegate void ForEachContextEntityActionValue<TContext>(TContext context, Entity entity);
