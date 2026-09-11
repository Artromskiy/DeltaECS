using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS;

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that processes matching entities without components.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEachEntity(in query, ref functor);</code> and
/// <code>world.ForEachEntityParallel(in query, ref functor, workerCount: 4);</code>.
/// Functor calls are explicit and are not intercepted.
/// </summary>
public interface IForEachEntity
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that processes matching components.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEach(in query, ref functor);</code> and
/// <code>world.ForEachParallel(in query, ref functor, workerCount: 4);</code>.
/// Functor calls are explicit and are not intercepted.
/// </summary>
public interface IForEach
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that receives caller-owned context.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEach(in query, ref state, ref functor);</code> and
/// <code>world.ForEachParallel(in query, in state, ref functor, workerCount: 4);</code>.
/// Parallel context must be read-only or by value; a parallel <c>ref</c> state
/// form is not generated.
/// </summary>
public interface IForEachContext<TContext>
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that receives context and the current entity.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEachEntity(in query, ref state, ref functor);</code> and
/// <code>world.ForEachEntityParallel(in query, in state, ref functor, workerCount: 4);</code>.
/// Parallel context must be read-only or by value; a parallel <c>ref</c> state
/// form is not generated.
/// </summary>
public interface IForEachContextEntity<TContext>
{
}
