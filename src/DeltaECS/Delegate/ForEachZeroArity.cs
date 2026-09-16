namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>
    /// Zero-component delegate callback overload.
    /// Use a component-bearing generated form such as
    /// <c>world.ForEach(in query, static (ref Position position, in Velocity velocity) =&gt; ...)</c>.
    /// Generated callbacks use one or more component parameters and may target
    /// the query or an explicit entity span, with optional <c>ComponentId</c>
    /// selectors and caller context.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEach(in Query query, ForEachAction action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> without
    /// requesting component rows, for example
    /// <c>world.ForEachEntity(in query, static entity =&gt; Log(entity))</c>.
    /// Use a component-bearing generated <c>ForEachEntity</c> form such as
    /// <c>world.ForEachEntity(in query, static (Entity entity, in Position position) =&gt; ...)</c>.
    /// Generated forms put <c>Entity</c> first and may target the query or an
    /// explicit entity span, include component rows, explicit
    /// <c>ComponentId</c> selectors, or caller context.
    /// </summary>
    public void ForEachEntity(in Query query, ForEachEntityAction action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out GeneratedReadQuerySlots slots))
        {
            int count = slots.Count;
            ref readonly Entity firstEntity = ref slots.GetGeneratedEntityReference();
            for (int index = 0; index < count; index++)
            {
                action(global::System.Runtime.CompilerServices.Unsafe.Add(
                    ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in firstEntity),
                    index));
            }
        }
    }

    /// <summary>
    /// Zero-component context callback overload.
    /// Use a component-bearing generated form such as
    /// <c>world.ForEach(in query, ref state, static (ref State value, ref Position position) =&gt; ...)</c>.
    /// Generated forms support caller context, query or explicit entity-span
    /// targets, explicit <c>ComponentId</c> selectors, and one or more component
    /// parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> with mutable
    /// caller context and without requesting component rows, for example
    /// <c>world.ForEachEntity(in query, ref state, static (ref State value, Entity entity) =&gt; ...)</c>.
    /// A generated component-bearing form can also be used, for example
    /// <c>world.ForEachEntity(in query, ref state, static (ref State value, Entity entity, ref Position position) =&gt; ...)</c>.
    /// Generated forms place <c>Entity</c> after caller context and before any
    /// component parameters.
    /// </summary>
    public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
    {
        ThrowHelper.ThrowIfNull(action, nameof(action));
        using var execution = GeneratedForEachRuntime.OpenReadDense(this, in query);
        while (execution.MoveNextTrusted(out GeneratedReadQuerySlots slots))
        {
            int count = slots.Count;
            ref readonly Entity firstEntity = ref slots.GetGeneratedEntityReference();
            for (int index = 0; index < count; index++)
            {
                action(
                    ref context,
                    global::System.Runtime.CompilerServices.Unsafe.Add(
                        ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in firstEntity),
                        index));
            }
        }
    }

    /// <summary>
    /// Compiler anchor for generated <c>ForEachEntity</c> functor forms.
    /// A functor implementing <c>IForEachEntity</c> may receive only
    /// <c>Entity</c> or <c>Entity</c> followed by component parameters.
    /// For example: <c>world.ForEachEntity(in query, ref action)</c>, where
    /// <c>action.Invoke</c> receives <c>Entity</c> first.
    /// </summary>
    /// <remarks>The by-value anchor throws; use the generated <c>ref</c> overload.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated functor overload was not selected.</exception>
    public void ForEachEntity<T>(in Query query, T action) where T : IForEachEntity
        => ThrowHelper.ThrowGeneratedFunctorRequired();

    /// <summary>
    /// Zero-component functor overload.
    /// Use a component-bearing generated <c>ForEach</c> form with a functor
    /// implementing <c>IForEach</c>; generated forms use one or more component
    /// parameters and may include caller context or explicit <c>ComponentId</c>
    /// selectors.
    /// For example: <c>world.ForEach(in query, ref action)</c>, where
    /// <c>action.Invoke</c> receives the requested components.
    /// </summary>
    /// <remarks>This zero-component overload always throws; no generated zero-component functor form exists.</remarks>
    /// <exception cref="System.InvalidOperationException">Zero-component functor iteration is not supported.</exception>
    public void ForEach<T>(in Query query, T action) where T : IForEach
        => ThrowHelper.ThrowGeneratedFunctorRequired();

    /// <summary>
    /// Zero-component stamp callback anchor. Use a generated
    /// <c>ForEachStamp</c> form with one or more <c>in Stamp</c> parameters,
    /// for example <c>world.ForEachStamp&lt;Health&gt;(in query,
    /// static (in Stamp stamp) =&gt; Process(stamp))</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachStamp(in Query query, ForEachAction action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component entity stamp callback anchor. Generated
    /// <c>ForEachEntityStamp</c> callbacks receive <c>Entity</c> followed by
    /// one or more <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachEntityStamp(in Query query, ForEachEntityAction action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component stamp functor anchor. Use a generated
    /// <c>ForEachStamp</c> form whose functor receives one or more
    /// <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachStamp<T>(in Query query, T action) where T : IForEach
        => ThrowHelper.ThrowGeneratedFunctorRequired();

    /// <summary>
    /// Zero-component entity stamp functor anchor. Use a generated
    /// <c>ForEachEntityStamp</c> form whose functor receives <c>Entity</c>
    /// followed by one or more <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public void ForEachEntityStamp<T>(in Query query, T action) where T : IForEachEntity
        => ThrowHelper.ThrowGeneratedFunctorRequired();

}
