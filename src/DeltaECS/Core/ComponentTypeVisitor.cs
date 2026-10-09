namespace Delta.ECS;

/// <summary>Marker for visitors that receive a registered component type.</summary>
public interface IVisitor
{
}

/// <summary>Visits a component type without a built-in type constraint.</summary>
public interface IUnconstrainedVisitor : IVisitor
{
    /// <summary>Receives the registered component CLR type and ID.</summary>
    void Visit<T>(ComponentId componentId);
}

/// <summary>Marks a visitor whose type constraint is selected at runtime.</summary>
public interface IComponentVisitor : IVisitor
{
    /// <summary>Gets the CLR type of the explicitly registered interface route.</summary>
    RuntimeTypeHandle ConstraintType { get; }
}

/// <summary>Visits a registered component that implements <typeparamref name="TConstraint"/>.</summary>
public interface IComponentVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a component type satisfying the interface route.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : TConstraint;

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : TConstraint
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : TConstraint
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

/// <summary>Visits a component type registered as a value type.</summary>
public interface IStructVisitor : IVisitor
{
    /// <summary>Receives a registered value type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : struct;
}

/// <summary>Visits a component type registered as a reference type.</summary>
public interface IClassVisitor : IVisitor
{
    /// <summary>Receives a registered reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class;
}

/// <summary>Visits a component type registered as unmanaged.</summary>
public interface IUnmanagedVisitor : IVisitor
{
    /// <summary>Receives a registered unmanaged type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : unmanaged;
}

/// <summary>Visits a component type with a public parameterless constructor.</summary>
public interface INewVisitor : IVisitor
{
    /// <summary>Receives a constructible registered type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : new();
}

/// <summary>Visits a component type registered as a constructible reference type.</summary>
public interface IClassNewVisitor : IVisitor
{
    /// <summary>Receives a constructible registered reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, new();
}

/// <summary>Visits a component type satisfying a value-type and interface constraint.</summary>
public interface IStructVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a constrained value type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint;

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

/// <summary>Visits a component type satisfying a reference-type and interface constraint.</summary>
public interface IClassVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a constrained reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint;

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

/// <summary>Visits an unmanaged component type satisfying an interface constraint.</summary>
public interface IUnmanagedVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a constrained unmanaged type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint;

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

/// <summary>Visits a component type satisfying an interface and constructor constraint.</summary>
public interface INewVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a constrained constructible type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : TConstraint, new();

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : TConstraint, new()
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : TConstraint, new()
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

/// <summary>Visits a component type satisfying reference, interface, and constructor constraints.</summary>
public interface IClassNewVisitor<in TConstraint> : IComponentVisitor
{
    /// <summary>Receives a constrained constructible reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new();

    /// <summary>Visits the component when this visitor selects the matching interface constraint.</summary>
    bool TryVisit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new()
        => ComponentVisitorConstraints.HasMatchingConstraint<TConstraint>(this) && TrueVisit<TComponent>(componentId);

    private bool TrueVisit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new()
    {
        Visit<TComponent>(componentId);
        return true;
    }
}

internal static class ComponentVisitorConstraints
{
    internal static bool HasMatchingConstraint<TConstraint>(IComponentVisitor visitor)
        => visitor.ConstraintType.Equals(typeof(TConstraint).TypeHandle);
}


/// <summary>Provides virtual visits selected by component type constraints for types that implement <typeparamref name="TConstraint"/>.</summary>
/// <typeparam name="TConstraint">The interface implemented by visitable component types.</typeparam>
public abstract class GeneralComponentTypeVisitor<TConstraint> :
    IComponentVisitor
{
    /// <summary>Gets the interface constraint selected by this visitor's type argument.</summary>
    public RuntimeTypeHandle ConstraintType => typeof(TConstraint).TypeHandle;

    /// <summary>Visits a component type satisfying only the interface constraint.</summary>
    protected virtual void Visit<TComponent>(ComponentId componentId) where TComponent : TConstraint { }

    /// <summary>Visits a component type satisfying the unmanaged and interface constraints.</summary>
    protected virtual void VisitUnmanaged<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint { }

    /// <summary>Visits a component type satisfying the struct and interface constraints.</summary>
    protected virtual void VisitStruct<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint { }

    /// <summary>Visits a component type satisfying the class and interface constraints.</summary>
    protected virtual void VisitClass<TComponent>(ComponentId componentId) where TComponent : class, TConstraint { }

    /// <summary>Visits a component type satisfying the interface and constructor constraints.</summary>
    protected virtual void VisitConstructible<TComponent>(ComponentId componentId) where TComponent : TConstraint, new() { }

    /// <summary>Visits a component type satisfying the class, interface, and constructor constraints.</summary>
    protected virtual void VisitClassConstructible<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new() { }

    internal bool TryUnconstrained<TComponent>(ComponentId componentId) where TComponent : TConstraint
    {
        Visit<TComponent>(componentId);
        return true;
    }

    internal bool TryUnmanaged<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint
    {
        VisitUnmanaged<TComponent>(componentId);
        return true;
    }

    internal bool TryStruct<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint
    {
        VisitStruct<TComponent>(componentId);
        return true;
    }

    internal bool TryClass<TComponent>(ComponentId componentId) where TComponent : class, TConstraint
    {
        VisitClass<TComponent>(componentId);
        return true;
    }

    internal bool TryNew<TComponent>(ComponentId componentId) where TComponent : TConstraint, new()
    {
        VisitConstructible<TComponent>(componentId);
        return true;
    }

    internal bool TryClassNew<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new()
    {
        VisitClassConstructible<TComponent>(componentId);
        return true;
    }
}
