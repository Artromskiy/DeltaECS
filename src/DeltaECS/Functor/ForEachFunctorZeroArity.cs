namespace Delta.ECS;

public sealed partial class World
{
    public void ForEach<TFunctor>(in Query query, ref TFunctor functor)
        where TFunctor : struct, IForEach
    {
        var invoker = new FunctorInvoker<TFunctor>(functor);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        functor = invoker.Functor;
    }

    public void ForEachEntity<TFunctor>(in Query query, ref TFunctor functor)
        where TFunctor : struct, IForEachEntity
    {
        var invoker = new EntityFunctorInvoker<TFunctor>(functor);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        functor = invoker.Functor;
    }

    public void ForEach<TContext, TFunctor>(in Query query, ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContext<TContext>
    {
        var invoker = new ContextFunctorInvoker<TContext, TFunctor>(context, functor);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
        functor = invoker.Functor;
    }

    public void ForEachEntity<TContext, TFunctor>(in Query query, ref TContext context, ref TFunctor functor)
        where TFunctor : struct, IForEachContextEntity<TContext>
    {
        var invoker = new ContextEntityFunctorInvoker<TContext, TFunctor>(context, functor);
        ExecuteGeneratedForEach(in query, ref invoker, hasWrites: false);
        context = invoker.Context;
        functor = invoker.Functor;
    }

    private struct FunctorInvoker<TFunctor> : IGeneratedForEachInvoker
        where TFunctor : struct, IForEach
    {
        public FunctorInvoker(TFunctor functor) => Functor = functor;

        public TFunctor Functor;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                Functor.Invoke();
            }
        }
    }

    private struct EntityFunctorInvoker<TFunctor> : IGeneratedForEachInvoker
        where TFunctor : struct, IForEachEntity
    {
        public EntityFunctorInvoker(TFunctor functor) => Functor = functor;

        public TFunctor Functor;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                Functor.Invoke(slots.CurrentEntity);
            }
        }
    }

    private struct ContextFunctorInvoker<TContext, TFunctor> : IGeneratedForEachInvoker
        where TFunctor : struct, IForEachContext<TContext>
    {
        public ContextFunctorInvoker(TContext context, TFunctor functor)
        {
            Context = context;
            Functor = functor;
        }

        public TContext Context;
        public TFunctor Functor;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                Functor.Invoke(ref Context);
            }
        }
    }

    private struct ContextEntityFunctorInvoker<TContext, TFunctor> : IGeneratedForEachInvoker
        where TFunctor : struct, IForEachContextEntity<TContext>
    {
        public ContextEntityFunctorInvoker(TContext context, TFunctor functor)
        {
            Context = context;
            Functor = functor;
        }

        public TContext Context;
        public TFunctor Functor;

        public void Invoke(ref QuerySlots slots)
        {
            while (slots.MoveNext())
            {
                Functor.Invoke(ref Context, slots.CurrentEntity);
            }
        }
    }
}
