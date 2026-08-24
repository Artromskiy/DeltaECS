namespace Delta.ECS;

public sealed partial class World
{
    public void ForEach<TFunctor>(in Query query, ref TFunctor functor)
        where TFunctor : struct, IForEach
    {
        Execute(in query, ref functor, static (ref TFunctor value, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                value.Invoke();
            }
        });
    }

    public void ForEachEntity<TFunctor>(in Query query, ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
    {
        Execute(in query, ref functor, static (ref TFunctor value, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                value.Invoke(cursor.Entities[cursor.CurrentIndex]);
            }
        });
    }

    public void ForEach<TContext, TFunctor>(
        in Query query,
        ref TContext context,
        ref TFunctor functor)
        where TFunctor : struct, IForEachContext<TContext>
    {
        var state = new ContextFunctorState<TContext, TFunctor>(context, functor);
        Execute(in query, ref state, static (ref ContextFunctorState<TContext, TFunctor> state, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                state.Functor.Invoke(ref state.Context);
            }
        });
        context = state.Context;
        functor = state.Functor;
    }

    public void ForEachEntity<TContext, TFunctor>(
        in Query query,
        ref TContext context,
        ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
    {
        var state = new ContextEntityFunctorState<TContext, TFunctor>(context, functor);
        Execute(in query, ref state, static (ref ContextEntityFunctorState<TContext, TFunctor> state, ref QueryChunkCursor cursor) =>
        {
            while (cursor.MoveNext())
            {
                state.Functor.Invoke(ref state.Context, cursor.Entities[cursor.CurrentIndex]);
            }
        });
        context = state.Context;
        functor = state.Functor;
    }

    private struct ContextFunctorState<TContext, TFunctor>
        where TFunctor : struct, IForEachContext<TContext>
    {
        public ContextFunctorState(TContext context, TFunctor functor)
        {
            Context = context;
            Functor = functor;
        }

        public TContext Context;
        public TFunctor Functor;
    }

    private struct ContextEntityFunctorState<TContext, TFunctor>
        where TFunctor : struct, IForEachContextEntity<TContext>
    {
        public ContextEntityFunctorState(TContext context, TFunctor functor)
        {
            Context = context;
            Functor = functor;
        }

        public TContext Context;
        public TFunctor Functor;
    }
}
