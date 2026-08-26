namespace DeltaECS;

public delegate void ForEachEntityAction(Entity entity);

public delegate void ForEachAction();

public delegate void ForEachContextAction<TContext>(ref TContext context);

public delegate void ForEachContextEntityAction<TContext>(ref TContext context, Entity entity);
