using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedOperationGrammarProof
{
    internal static void Run(World world, in Query query, ref Context context)
    {
        var unboxed = world.ForEach(in query, static (ref Cmp1 component) => component.Value++);
        InvokeUnboxed(unboxed);

#pragma warning disable CA1859 // Exercise interface storage and its one-time boxing behavior.
        IOperation boxed = world.ForEach<Cmp1>(in query, world.Layouts.GetPrimary<Cmp1>(),
            static (in Cmp1 component) => _ = component.Value);
        boxed.Invoke();

        var unboxedContext = world.ForEach(in query, ref context,
            static (ref Context state, ref Cmp1 component) => state.Value += component.Value);
        InvokeUnboxed(ref context, unboxedContext);

        IOperation<Context> boxedContext = world.ForEach(in query, ref context,
            static (ref Context state, ref Cmp1 component) => state.Value += component.Value);
        boxedContext.Invoke(ref context);

        var functor = new FunctorContext();
        var unboxedContextFunctor = world.ForEach(in query, ref context, ref functor);
        InvokeUnboxed(ref context, ref functor, unboxedContextFunctor);

        IOperation<Context, FunctorContext> boxedContextFunctor = world.ForEach(in query, ref context, ref functor);
        boxedContextFunctor.Invoke(ref context, ref functor);
        boxedContextFunctor.Invoke(ref functor);
        boxedContextFunctor.InvokeWithContext(ref context);
        boxedContextFunctor.Invoke();
#pragma warning restore CA1859
    }

    private static void InvokeUnboxed<TOperation>(TOperation operation)
        where TOperation : struct, IOperation
        => operation.Invoke();

    private static void InvokeUnboxed<TState, TOperation>(ref TState state, TOperation operation)
        where TOperation : struct, IOperation<TState>
        => operation.Invoke(ref state);

    private static void InvokeUnboxed<TContext, TFunctor, TOperation>(
        ref TContext context,
        ref TFunctor functor,
        TOperation operation)
        where TOperation : struct, IOperation<TContext, TFunctor>
        => operation.Invoke(ref context, ref functor);
}
