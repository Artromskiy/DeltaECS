using System.ComponentModel;
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

/// <summary>Generated callback contract used to compose predicate views with ordered-query terminals.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedWhereEntityConsumer
{
    /// <summary>Consumes an entity that passed a generated <c>Where</c> predicate.</summary>
    void Invoke(Entity entity);
}

/// <summary>
/// Marker contract for a read-only component comparer. Implement one
/// <c>int Invoke(in T1 left1, ..., in T1 right1, ...)</c> method. A context may
/// be the first parameter. The source generator emits typed <c>OrderBy</c> and
/// <c>ThenBy</c> forms for the comparer signature.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IComponentComparer
{
}

/// <summary>
/// Marker contract for an entity-aware component comparer. Implement one
/// <c>int Invoke(Entity leftEntity, in T1 left1, ..., Entity rightEntity, in T1 right1, ...)</c>
/// method. A context may be its first parameter.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IComponentComparerEntity : IComponentComparer
{
}

/// <summary>Generated bridge used by ordered-query comparer extensions.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public interface IGeneratedComponentComparer
{
    /// <summary>Validates the registrations selected for this ordering key.</summary>
    void Validate(World world, in Query query);

    /// <summary>Compares two entities using this ordering key.</summary>
    int Compare(World world, Entity left, Entity right);
}
