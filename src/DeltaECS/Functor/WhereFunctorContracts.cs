using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS;

/// <summary>
/// Marker contract for a query-wide read-only predicate functor.
/// The generator reads a single <c>bool Invoke(...)</c> method and emits the
/// matching <c>World.Where</c> or <c>World.WhereEntity</c> overload on demand.
/// The entity form starts with <c>Entity</c>; the component-only form omits it.
/// A caller-owned context, when used, is the first <c>ref</c> parameter.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IWherePredicate
{
}
