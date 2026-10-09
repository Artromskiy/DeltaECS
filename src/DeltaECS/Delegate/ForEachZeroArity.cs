namespace Delta.ECS;

public sealed partial class World
{
    /// <summary>
    /// Zero-component delegate callback overload.
    /// Use a component-bearing generated form such as
    /// <c>world.ForEach(in query, static (ref Position position, in Velocity velocity) =&gt; ...).Invoke()</c>.
    /// Generated callbacks use one or more component parameters and may target
    /// the query or an explicit entity span, with optional <c>ComponentId</c>
    /// selectors and caller context.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public EcsOperation<ThrowingOperationInvoker> ForEach(in Query query, ForEachAction action)
        => new(default(ThrowingOperationInvoker));

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> without
    /// requesting component rows, for example
    /// <c>world.ForEachEntity(in query, static entity =&gt; Log(entity.Handle)).Invoke()</c>.
    /// Use a component-bearing generated <c>ForEachEntity</c> form such as
    /// <c>world.ForEachEntity(in query, static (EntityRef entity, in Position position) =&gt; ...).Invoke()</c>.
    /// Generated forms put <c>EntityRef</c> first and may target the query or an
    /// explicit entity span, include component rows, explicit
    /// <c>ComponentId</c> selectors, or caller context.
    /// </summary>
    public EcsOperation<ForEachEntityOperationInvoker> ForEachEntity(in Query query, ForEachEntityAction action)
        => new(new ForEachEntityOperationInvoker(this, query, action));

    /// <summary>
    /// Zero-component context callback overload.
    /// Use a component-bearing generated form such as
    /// <c>world.ForEach(in query, ref state, static (ref State value, ref Position position) =&gt; ...).Invoke(ref state)</c>.
    /// Generated forms support caller context, query or explicit entity-span
    /// targets, explicit <c>ComponentId</c> selectors, and one or more component
    /// parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    /// <exception cref="System.InvalidOperationException">The generated component-bearing overload was not selected.</exception>
    public EcsOperation<TContext, ThrowingOperationInvoker<TContext>> ForEach<TContext>(in Query query, ref TContext context, ForEachContextAction<TContext> action)
        => new(context, default(ThrowingOperationInvoker<TContext>));

    /// <summary>
    /// Iterates every entity selected by <paramref name="query"/> with mutable
    /// caller context and without requesting component rows, for example
    /// <c>world.ForEachEntity(in query, ref state, static (ref State value, EntityRef entity) =&gt; ...).Invoke(ref state)</c>.
    /// A generated component-bearing form can also be used, for example
    /// <c>world.ForEachEntity(in query, ref state, static (ref State value, EntityRef entity, ref Position position) =&gt; ...).Invoke(ref state)</c>.
    /// Generated forms place <c>EntityRef</c> after caller context and before any
    /// component parameters.
    /// </summary>
    public EcsOperation<TContext, ForEachEntityContextOperationInvoker<TContext>> ForEachEntity<TContext>(in Query query, ref TContext context, ForEachContextEntityAction<TContext> action)
        => new(context, new ForEachEntityContextOperationInvoker<TContext>(this, query, action));

    /// <summary>
    /// Zero-component stamp callback anchor. Use a generated
    /// <c>ForEachStamp</c> form with one or more <c>in Stamp</c> parameters,
    /// for example <c>world.ForEachStamp&lt;Health&gt;(in query,
    /// static (in Stamp stamp) =&gt; Process(stamp))</c>.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public EcsOperation<ThrowingOperationInvoker> ForEachStamp(in Query query, ForEachAction action)
        => new(default(ThrowingOperationInvoker));

    /// <summary>
    /// Zero-component entity stamp callback anchor. Generated
    /// <c>ForEachEntityStamp</c> callbacks receive <c>EntityRef</c> followed by
    /// one or more <c>in Stamp</c> parameters.
    /// </summary>
    /// <remarks>This zero-component overload always throws.</remarks>
    public EcsOperation<ThrowingOperationInvoker> ForEachEntityStamp(in Query query, ForEachEntityAction action)
        => new(default(ThrowingOperationInvoker));

}

/// <summary>Direct executor for a zero-component entity callback operation.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct ForEachEntityOperationInvoker : IEcsOperationInvoker
{
    private readonly World _world;
    private readonly Query _query;
    private readonly ForEachEntityAction _action;

    internal ForEachEntityOperationInvoker(World world, in Query query, ForEachEntityAction action)
    {
        _world = world;
        _query = query;
        _action = action;
    }

    /// <summary>Executes the entity callback over the stored query.</summary>
    public void Invoke()
    {
        ThrowHelper.ThrowIfNull(_action, nameof(_action));
        using var execution = GeneratedForEachRuntime.OpenReadDense(_world, in _query);
        var entityRef = new EntityRef(_world);
        while (execution.MoveNextTrusted(out GeneratedReadQuerySlots slots))
        {
            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                entityRef._entity = slots.EntityAt(index);
                _action(entityRef);
            }
        }
    }
}

/// <summary>Direct executor for a zero-component entity callback with mutable context.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public readonly struct ForEachEntityContextOperationInvoker<TContext> : IEcsOperationInvoker<TContext>
{
    private readonly World _world;
    private readonly Query _query;
    private readonly ForEachContextEntityAction<TContext> _action;

    internal ForEachEntityContextOperationInvoker(World world, in Query query, ForEachContextEntityAction<TContext> action)
    {
        _world = world;
        _query = query;
        _action = action;
    }

    /// <summary>Executes the entity callback using and updating caller-owned context.</summary>
    public void Invoke(ref TContext context)
    {
        ThrowHelper.ThrowIfNull(_action, nameof(_action));
        using var execution = GeneratedForEachRuntime.OpenReadDense(_world, in _query);
        while (execution.MoveNextTrusted(out GeneratedReadQuerySlots slots))
        {
            if (slots.TryGetTagSlots(out var tagSlots))
            {
                for (int index = 0; index < tagSlots.Length; index++)
                {
                    _action(ref context, slots.GetEntityRef(index));
                }

                continue;
            }

            int count = slots.Count;
            for (int index = 0; index < count; index++)
            {
                _action(ref context, slots.GetEntityRef(index));
            }
        }
    }
}
