using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS;

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that processes matching entities without components.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEach(in query, ref functor);</code>
/// </summary>
public interface IForEachEntity
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that processes matching components.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEach(in query, ref functor);</code>
/// </summary>
public interface IForEach
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that receives caller-owned context.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEach(in query, ref state, ref functor);</code>
/// </summary>
public interface IForEachContext<TContext>
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
/// <summary>
/// Marker contract for a functor that receives context and the current entity.
/// Generated component-bearing forms support callback arity 1-256, for example
/// <code>world.ForEachEntity(in query, ref state, ref functor);</code>
/// </summary>
public interface IForEachContextEntity<TContext>
{
}
