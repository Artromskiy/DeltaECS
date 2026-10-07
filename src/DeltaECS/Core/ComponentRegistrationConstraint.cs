namespace Delta.ECS;

/// <summary>Selects a struct-constrained component registration.</summary>
public readonly struct StructConstraint { }

/// <summary>Selects a class-constrained component registration.</summary>
public readonly struct ClassConstraint { }

/// <summary>Selects an unmanaged-constrained component registration.</summary>
public readonly struct UnmanagedConstraint { }

/// <summary>Selects a parameterless-constructor component registration.</summary>
public readonly struct NewConstraint { }

/// <summary>Selects a class and parameterless-constructor registration.</summary>
public readonly struct ClassNewConstraint { }
