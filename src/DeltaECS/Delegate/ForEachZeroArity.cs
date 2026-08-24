namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>Executes a zero-component callback for every matching entity.
    /// Generated component-bearing forms support any arity, for example
    /// <code> world.ForEach(in query, static (ref Position p, in Velocity v) =&gt; p += v);</code>
    /// </summary>
    public void ForEach(in Query query, ForEachAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    /// <summary>
    /// Executes a zero-component callback with the current entity.
    /// Generated component-bearing forms support any arity, for example
    /// <code>world.ForEachEntity(in query, static entity =&gt; Log(entity));</code>
    /// </summary>
    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new EntityActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    /// <summary>
    /// Executes a zero-component callback with caller-owned context.
    /// Generated component-bearing forms support any arity, for example
    /// <code>world.ForEach(in query, ref state, static (ref State s, ref Position p) =&gt; s.Sum += p.X);</code>
    /// </summary>
    public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ContextActionInvoker<TContext>(context, action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
    }

    /// <summary>
    /// Executes a zero-component callback with context and entity.
    /// Generated component-bearing forms support any arity, for example
    /// <code>world.ForEachEntity(in query, ref state, static (ref State s, Entity entity, ref Position p) =&gt; s.Sum += p.X + entity.Index);</code>
    /// </summary>
    public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ContextEntityActionInvoker<TContext>(context, action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
    }

    /// <summary>
    /// Entry point for a functor that processes matching entities without components.
    /// Generated component-bearing forms support any arity, for example
    /// <code>world.ForEachEntity(in query, functor);</code>
    /// </summary>
    public void ForEachEntity<T>(in Query query, T action) where T : IForEachEntity
    {
    }

    /// <summary>
    /// Entry point for a functor that processes matching components.
    /// Generated forms support any arity, for example
    /// <code>world.ForEach(in query, functor);</code>
    /// </summary>
    public void ForEach<T>(in Query query, T action) where T : IForEach
    {
    }

    private struct ActionInvoker : IGeneratedForEachInvoker
    {
        private readonly ForEachAction _action;

        public ActionInvoker(ForEachAction action) => _action = action;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                _action();
            }
        }
    }

    private struct EntityActionInvoker : IGeneratedForEachInvoker
    {
        private readonly ForEachEntityAction _action;

        public EntityActionInvoker(ForEachEntityAction action) => _action = action;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                _action(slots.CurrentEntity);
            }
        }
    }

    private struct ContextActionInvoker<TContext> : IGeneratedForEachInvoker
    {
        private readonly ForEachContextAction<TContext> _action;

        public ContextActionInvoker(TContext context, ForEachContextAction<TContext> action)
        {
            Context = context;
            _action = action;
        }

        public TContext Context;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                _action(ref Context);
            }
        }
    }

    private struct ContextEntityActionInvoker<TContext> : IGeneratedForEachInvoker
    {
        private readonly ForEachContextEntityAction<TContext> _action;

        public ContextEntityActionInvoker(TContext context, ForEachContextEntityAction<TContext> action)
        {
            Context = context;
            _action = action;
        }

        public TContext Context;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                _action(ref Context, slots.CurrentEntity);
            }
        }
    }
}
