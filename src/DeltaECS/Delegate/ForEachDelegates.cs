namespace Delta.ECS;

/// <summary>Processes one selected entity through a borrowed view valid during the callback.</summary>
public delegate void ForEachEntityAction(EntityRef entity);

/// <summary>Runs a callback that does not receive entity or component data.</summary>
public delegate void ForEachAction();

/// <summary>Processes a selection with mutable caller-owned context.</summary>
public delegate void ForEachContextAction<TContext>(ref TContext context);

/// <summary>Processes each selected entity with mutable caller-owned context.</summary>
public delegate void ForEachContextEntityAction<TContext>(ref TContext context, EntityRef entity);

/// <summary>Processes a selection with read-only caller-owned context.</summary>
public delegate void ForEachContextActionIn<TContext>(in TContext context);

/// <summary>Processes each selected entity with read-only caller-owned context.</summary>
public delegate void ForEachContextEntityActionIn<TContext>(in TContext context, EntityRef entity);

/// <summary>Processes a selection with a context value passed by value.</summary>
public delegate void ForEachContextActionValue<TContext>(TContext context);

/// <summary>Processes each selected entity with a context value passed by value.</summary>
public delegate void ForEachContextEntityActionValue<TContext>(TContext context, EntityRef entity);
