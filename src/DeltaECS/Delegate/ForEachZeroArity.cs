namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>Executes a zero-component callback for every matching entity.</summary>
    /// <remarks>
    /// This handwritten overload is the zero-arity form of the generated
    /// <c>ForEach</c> API. Component-bearing callbacks are generated on demand
    /// for any arity supported by the 256-component mask, for example:
    /// <example>
    /// <code>
    /// world.ForEach&lt;Position, Velocity&gt;(in query,
    ///     static (ref Position position, in Velocity velocity) =&gt;
    ///     {
    ///         position.X += velocity.X;
    ///     });
    /// </code>
    /// </example>
    /// </remarks>
    public void ForEach(in Query query, ForEachAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    /// <summary>Executes a zero-component callback with the current entity.</summary>
    /// <remarks>
    /// Component-bearing <c>ForEachEntity</c> callbacks are generated on demand
    /// with the same entity-first shape and supported arity range.
    /// </remarks>
    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new EntityActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    /// <summary>Executes a zero-component callback with caller-owned context.</summary>
    /// <remarks>
    /// Generated component-bearing forms keep <typeparamref name="TContext"/>
    /// at the callback boundary and support the same component arity range.
    /// </remarks>
    public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ContextActionInvoker<TContext>(context, action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
    }

    /// <summary>Executes a zero-component callback with context and entity.</summary>
    /// <remarks>
    /// Generated component-bearing forms add component parameters after the
    /// context and entity parameters and support the same component arity range.
    /// </remarks>
    public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ContextEntityActionInvoker<TContext>(context, action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
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
