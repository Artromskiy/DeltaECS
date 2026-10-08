namespace Delta.ECS;

/// <summary>Processes one selected entity through a scoped borrowed view valid during the callback.</summary>
public delegate void ForEachEntityAction(scoped EntityRef entity);

/// <summary>Runs a callback that does not receive entity or component data.</summary>
public delegate void ForEachAction();

/// <summary>Processes a selection with mutable caller-owned context.</summary>
public delegate void ForEachContextAction<TContext>(ref TContext context);

/// <summary>Processes each selected entity with mutable caller-owned context and a scoped borrowed view.</summary>
public delegate void ForEachContextEntityAction<TContext>(ref TContext context, scoped EntityRef entity);

/// <summary>Processes a selection with read-only caller-owned context.</summary>
public delegate void ForEachContextActionIn<TContext>(in TContext context);

/// <summary>Processes each selected entity with read-only caller-owned context and a scoped borrowed view.</summary>
public delegate void ForEachContextEntityActionIn<TContext>(in TContext context, scoped EntityRef entity);

/// <summary>Processes a selection with a context value passed by value.</summary>
public delegate void ForEachContextActionValue<TContext>(TContext context);

/// <summary>Processes each selected entity with a context value and a scoped borrowed view.</summary>
public delegate void ForEachContextEntityActionValue<TContext>(TContext context, scoped EntityRef entity);
