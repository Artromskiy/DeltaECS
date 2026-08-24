namespace Delta.ECS;

public sealed partial class World
{
    public void ForEach(in Query query, ForEachAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new EntityActionInvoker(action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
    }

    public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var invoker = new ContextActionInvoker<TContext>(context, action);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
    }

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

        public void Invoke(ref GeneratedQuerySlots slots)
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

        public void Invoke(ref GeneratedQuerySlots slots)
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

        public void Invoke(ref GeneratedQuerySlots slots)
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

        public void Invoke(ref GeneratedQuerySlots slots)
        {
            while (slots.MoveNext())
            {
                _action(ref Context, slots.CurrentEntity);
            }
        }
    }
}
