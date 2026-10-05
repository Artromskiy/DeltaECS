namespace Delta.ECS;

using System;

/// <summary>Receives the CLR type and registration ID of one registered component.</summary>
public interface IComponentTypeVisitor
{
    /// <summary>Visits a component registration as its CLR type.</summary>
    void Visit<T>(ComponentId componentId);
}

/// <summary>Dispatches registered component IDs to generic visitors.</summary>
public sealed class ComponentVisitorRegistry
{
    private readonly ComponentLayoutRegistry _layouts;

    internal ComponentVisitorRegistry(ComponentLayoutRegistry layouts)
    {
        _layouts = layouts;
    }

    /// <summary>Visits the CLR type registered for <paramref name="componentId"/>.</summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="componentId">The component registration to visit.</param>
    /// <param name="visitor">The visitor, passed by reference so struct state is retained.</param>
    public void Visit<TVisitor>(ComponentId componentId, ref TVisitor visitor)
        where TVisitor : IComponentTypeVisitor
    {
        if (visitor is null)
        {
            ThrowHelper.ThrowIfNull(visitor, nameof(visitor));
        }

        IGeneratedComponentTypeToken typeToken = _layouts.GetComponentTypeToken(componentId);
        var adapter = new VisitorAdapter<TVisitor>(componentId, visitor);
        try
        {
            typeToken.Dispatch(ReadOnlySpan<IGeneratedComponentTypeToken>.Empty, ref adapter);
        }
        finally
        {
            visitor = adapter.Visitor;
        }
    }

    private struct VisitorAdapter<TVisitor>(ComponentId componentId, TVisitor visitor)
        : IGeneratedComponentTypeVisitor
        where TVisitor : IComponentTypeVisitor
    {
        internal TVisitor Visitor = visitor;

        void IGeneratedComponentTypeVisitor.Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining)
            => Visitor.Visit<T>(componentId);
    }
}
