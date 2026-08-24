namespace Delta.ECS;

public sealed partial class World
{
    public void ForEach(in Query query, ForEachAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Execute(in query, ref action, static (ref ForEachAction callback, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                callback();
            }
        });
    }

    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Execute(in query, ref action, static (ref ForEachEntityAction callback, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                callback(cursor.Entities[cursor.CurrentIndex]);
            }
        });
    }

    public void ForEach<TContext>(
        in Query query,
        ref TContext context,
        ForEachContextAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var state = new ContextActionState<TContext>(context, action);
        Execute(in query, ref state, static (ref ContextActionState<TContext> state, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                state.Action(ref state.Context);
            }
        });
        context = state.Context;
    }

    public void ForEachEntity<TContext>(
        in Query query,
        ref TContext context,
        ForEachContextEntityAction<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var state = new ContextEntityActionState<TContext>(context, action);
        Execute(in query, ref state, static (ref ContextEntityActionState<TContext> state, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                state.Action(ref state.Context, cursor.Entities[cursor.CurrentIndex]);
            }
        });
        context = state.Context;
    }

    private struct ContextActionState<TContext>
    {
        public ContextActionState(TContext context, ForEachContextAction<TContext> action)
        {
            Context = context;
            Action = action;
        }

        public TContext Context;
        public ForEachContextAction<TContext> Action;
    }

    private struct ContextEntityActionState<TContext>
    {
        public ContextEntityActionState(TContext context, ForEachContextEntityAction<TContext> action)
        {
            Context = context;
            Action = action;
        }

        public TContext Context;
        public ForEachContextEntityAction<TContext> Action;
    }
}
