namespace Delta.ECS;

public delegate void ForEachEntityAction(Entity entity);

public delegate void ForEachAction();

public delegate void ForEachContextAction<TContext>(ref TContext context);

public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);

public delegate void ForEachContextAction_In<TContext>(in TContext context);

public delegate void ForEachContextEntityAction_In<TContext>(in TContext context, Entity entity);

public delegate void ForEachContextAction_Value<TContext>(TContext context);

public delegate void ForEachContextEntityAction_Value<TContext>(TContext context, Entity entity);
