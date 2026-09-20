using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS;

/// <summary>
/// Marker contract for a functor that processes matching entities.
/// <c>Invoke</c> receives <c>Entity</c> first and may omit component parameters
/// or include generated component-bearing parameters, for example
/// <code>world.ForEachEntity(in query, ref functor);</code> and
/// <code>world.ForEachEntityParallel(in query, ref functor, workerCount: 4);</code>.
/// Functor calls are explicit and are not intercepted.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachEntity
{
}

/// <summary>
/// Marker contract for a functor that processes matching components.
/// Generated component-bearing forms support one or more component parameters,
/// for example
/// <code>world.ForEach(in query, ref functor);</code>,
/// <code>world.ForEach(in query, new Functor());</code>, and
/// <code>world.ForEachParallel(in query, in functor, workerCount: 4);</code>.
/// Functor calls are explicit and are not intercepted.
/// Use <c>ref</c> when the functor carries mutable state that must round-trip;
/// prefer by-value or <c>in</c> for stateless functors.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEach
{
}

/// <summary>
/// Marker contract for a functor that receives caller-owned context.
/// Generated component-bearing forms support one or more component parameters,
/// for example
/// <code>world.ForEach(in query, ref state, ref functor);</code> and
/// <code>world.ForEachParallel(in query, in state, ref functor, workerCount: 4);</code>.
/// Parallel context must be read-only or by value; a parallel <c>ref</c> state
/// form is not generated.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachContext<TContext>
{
}

/// <summary>
/// Marker contract for a functor that receives context and the current entity.
/// <c>Invoke</c> may stop after <c>Entity</c> or include generated
/// component-bearing parameters, for example
/// <code>world.ForEachEntity(in query, ref state, ref functor);</code> and
/// <code>world.ForEachEntityParallel(in query, in state, ref functor, workerCount: 4);</code>.
/// Parallel context must be read-only or by value; a parallel <c>ref</c> state
/// form is not generated.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachContextEntity<TContext>
{
}
