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
    /// Zero-component entity callback overload.
    /// Use a component-bearing generated <c>ForEachEntity</c> form such as
    /// <c>world.ForEachEntity(in query, static (Entity entity, in Position position) =&gt; ...)</c>.
    /// Generated forms put <c>Entity</c> first, use one or more component
    /// parameters, may target the query or an explicit entity span, and may
    /// include explicit <c>ComponentId</c> selectors and context.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachEntity(in Query query, ForEachEntityAction action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

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
    /// Zero-component entity context callback overload.
    /// Use a component-bearing generated form such as
    /// <c>world.ForEachEntity(in query, ref state, static (ref State value, Entity entity, ref Position position) =&gt; ...)</c>.
    /// Generated forms put <c>Entity</c> first, support caller context, query or
    /// explicit entity-span targets, explicit <c>ComponentId</c> selectors, and
    /// one or more component parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public void ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
        => ThrowHelper.ThrowGeneratedIterationRequired();

    /// <summary>
    /// Zero-component entity functor overload.
    /// Use a component-bearing generated <c>ForEachEntity</c> form with a functor
    /// implementing <c>IForEachEntity</c>; generated forms use one or more
    /// component parameters and may include caller context or explicit
    /// <c>ComponentId</c> selectors.
    /// </summary>
    /// <remarks>This zero-component overload always throws; no generated zero-component functor form exists.</remarks>
    /// <exception cref="System.InvalidOperationException">Zero-component functor iteration is not supported.</exception>
    public void ForEachEntity<T>(in Query query, T action) where T : IForEachEntity
        => ThrowHelper.ThrowGeneratedFunctorRequired();

    /// <summary>
    /// Zero-component functor overload.
    /// Use a component-bearing generated <c>ForEach</c> form with a functor
    /// implementing <c>IForEach</c>; generated forms use one or more component
    /// parameters and may include caller context or explicit <c>ComponentId</c>
    /// selectors.
    /// </summary>
    /// <remarks>This zero-component overload always throws; no generated zero-component functor form exists.</remarks>
    /// <exception cref="System.InvalidOperationException">Zero-component functor iteration is not supported.</exception>
    public void ForEach<T>(in Query query, T action) where T : IForEach
        => ThrowHelper.ThrowGeneratedFunctorRequired();

}
