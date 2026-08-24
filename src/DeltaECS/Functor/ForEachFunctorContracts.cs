using System.Diagnostics.CodeAnalysis;

namespace Delta.ECS;

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachEntity
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEach
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachContext<TContext>
{
}

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Source generator marker contract.")]
public interface IForEachContextEntity<TContext>
{
}
