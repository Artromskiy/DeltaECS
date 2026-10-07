namespace Delta.ECS;

/// <summary>Marker for visitors that receive a registered component type.</summary>
public interface IComponentTypeVisitor
{
}

/// <summary>Visits a component type without a built-in type constraint.</summary>
public interface IUnconstrainedComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives the registered component CLR type and ID.</summary>
    void Visit<T>(ComponentId componentId);
}

/// <summary>Marks a visitor whose type constraint is selected at runtime.</summary>
public interface IComponentTypeVisitorConstraint : IComponentTypeVisitor
{
    /// <summary>Gets the CLR type of the explicitly registered interface route.</summary>
    RuntimeTypeHandle ConstraintType { get; }
}

/// <summary>Visits a registered component that implements <typeparamref name="TConstraint"/>.</summary>
public interface IComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a component type satisfying the interface route.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : TConstraint;
}

/// <summary>Visits a component type registered as a value type.</summary>
public interface IStructComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives a registered value type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : struct;
}

/// <summary>Visits a component type registered as a reference type.</summary>
public interface IClassComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives a registered reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class;
}

/// <summary>Visits a component type registered as unmanaged.</summary>
public interface IUnmanagedComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives a registered unmanaged type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : unmanaged;
}

/// <summary>Visits a component type with a public parameterless constructor.</summary>
public interface INewComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives a constructible registered type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : new();
}

/// <summary>Visits a component type registered as a constructible reference type.</summary>
public interface IClassNewComponentTypeVisitor : IComponentTypeVisitor
{
    /// <summary>Receives a constructible registered reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, new();
}

/// <summary>Visits a component type satisfying a value-type and interface constraint.</summary>
public interface IStructComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a constrained value type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : struct, TConstraint;
}

/// <summary>Visits a component type satisfying a reference-type and interface constraint.</summary>
public interface IClassComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a constrained reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint;
}

/// <summary>Visits an unmanaged component type satisfying an interface constraint.</summary>
public interface IUnmanagedComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a constrained unmanaged type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : unmanaged, TConstraint;
}

/// <summary>Visits a component type satisfying an interface and constructor constraint.</summary>
public interface INewComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a constrained constructible type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : TConstraint, new();
}

/// <summary>Visits a component type satisfying reference, interface, and constructor constraints.</summary>
public interface IClassNewComponentTypeVisitor<TConstraint> : IComponentTypeVisitorConstraint
{
    /// <summary>Receives a constrained constructible reference type.</summary>
    void Visit<TComponent>(ComponentId componentId) where TComponent : class, TConstraint, new();
}
