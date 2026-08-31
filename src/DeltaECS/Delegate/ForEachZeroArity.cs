namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>Executes a zero-component callback for every matching entity.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code> world.ForEach(in query, static (ref Position p, in Velocity v) =&gt; p += v);</code>
    /// </summary>
    public void ForEach(in Query query, ForEachAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out var slots))
        {
            while (slots.MoveNext())
            {
                action();
            }
        }
    }

    /// <summary>
    /// Executes a zero-component callback with the current entity.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code>world.ForEachEntity(in query, static entity =&gt; Log(entity));</code>
    /// </summary>
    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out var slots))
        {
            while (slots.MoveNext())
            {
                action(slots.CurrentEntity);
            }
        }
    }

    /// <summary>
    /// Executes a zero-component callback with caller-owned context.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code>world.ForEach(in query, ref state, static (ref State s, ref Position p) =&gt; s.Sum += p.X);</code>
    /// </summary>
    public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out var slots))
        {
            while (slots.MoveNext())
            {
                action(ref context);
            }
        }
    }

    /// <summary>
    /// Executes a zero-component callback with context and entity.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code>world.ForEachEntity(in query, ref state, static (ref State s, Entity entity, ref Position p) =&gt; s.Sum += p.X + entity.Index);</code>
    /// </summary>
    public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out var slots))
        {
            while (slots.MoveNext())
            {
                action(ref context, slots.CurrentEntity);
            }
        }
    }

    /// <summary>
    /// Entry point for a functor that processes matching entities without components.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code>world.ForEachEntity(in query, functor);</code>
    /// </summary>
    public void ForEachEntity<T>(in Query query, T action) where T : IForEachEntity
    {
    }

    /// <summary>
    /// Entry point for a functor that processes matching components.
    /// Generated component-bearing forms support callback arity 1-256, for example
    /// <code>world.ForEach(in query, functor);</code>
    /// </summary>
    public void ForEach<T>(in Query query, T action) where T : IForEach
    {
    }

}
