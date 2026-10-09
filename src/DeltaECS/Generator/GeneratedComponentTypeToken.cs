namespace Delta.ECS;

using System;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>Compiler-support contract for dispatching a registered CLR type without reflection.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedComponentTypeToken
{
    /// <summary>Gets the CLR type represented by this token.</summary>
    Type ComponentType { get; }

    /// <summary>Dispatches this registered type to an unconstrained generated visitor.</summary>
    void Dispatch<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedComponentTypeVisitor;
}

/// <summary>Compiler-support visitor used to close unconstrained generated generic bindings.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedComponentTypeVisitor
{
    /// <summary>Receives one registered CLR type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining);
}

/// <summary>Compiler-support contract for dispatching a value type to a struct-constrained visitor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedStructComponentTypeToken
{
    /// <summary>Dispatches this value type to a struct-constrained visitor.</summary>
    void DispatchStruct<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedStructComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to struct.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedStructComponentTypeVisitor
{
    /// <summary>Receives one value type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : struct;
}

/// <summary>Compiler-support contract for dispatching an unmanaged type.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedUnmanagedComponentTypeToken
{
    /// <summary>Dispatches this unmanaged type to an unmanaged-constrained visitor.</summary>
    void DispatchUnmanaged<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedUnmanagedComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to unmanaged.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedUnmanagedComponentTypeVisitor
{
    /// <summary>Receives one unmanaged type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : unmanaged;
}

/// <summary>Compiler-support contract for dispatching a reference type.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedClassComponentTypeToken
{
    /// <summary>Dispatches this reference type to a class-constrained visitor.</summary>
    void DispatchClass<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedClassComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to class.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedClassComponentTypeVisitor
{
    /// <summary>Receives one reference type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : class;
}

/// <summary>Compiler-support contract for dispatching a nullable-annotated reference type.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedNullableClassComponentTypeToken
{
    /// <summary>Dispatches this reference type to a nullable class-constrained visitor.</summary>
    void DispatchNullableClass<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedNullableClassComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to class?.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedNullableClassComponentTypeVisitor
{
    /// <summary>Receives one reference type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : class?;
}

/// <summary>Compiler-support contract for dispatching a type that has a public parameterless constructor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedConstructibleComponentTypeToken
{
    /// <summary>Dispatches this type to a new()-constrained visitor.</summary>
    void DispatchConstructible<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedConstructibleComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to new().</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedConstructibleComponentTypeVisitor
{
    /// <summary>Receives one constructible type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : new();
}

/// <summary>Compiler-support contract for dispatching a class with a public parameterless constructor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedClassConstructibleComponentTypeToken
{
    /// <summary>Dispatches this class to a class, new()-constrained visitor.</summary>
    void DispatchClassConstructible<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedClassConstructibleComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to class, new().</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedClassConstructibleComponentTypeVisitor
{
    /// <summary>Receives one constructible reference type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : class, new();
}

/// <summary>Compiler-support contract for dispatching a nullable class with a public parameterless constructor.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedNullableClassConstructibleComponentTypeToken
{
    /// <summary>Dispatches this class to a class?, new()-constrained visitor.</summary>
    void DispatchNullableClassConstructible<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedNullableClassConstructibleComponentTypeVisitor;
}

/// <summary>Compiler-support visitor for generic parameters constrained to class?, new().</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedNullableClassConstructibleComponentTypeVisitor
{
    /// <summary>Receives one constructible reference type and the remaining type tokens.</summary>
    void Visit<T>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining) where T : class?, new();
}

/// <summary>Registers generated capability-aware tokens for CLR component types.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedComponentTypeTokenRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, IGeneratedComponentTypeToken> Tokens = new();

    /// <summary>Registers the generated token for a concrete component type.</summary>
    public static void Register(Type componentType, IGeneratedComponentTypeToken token)
    {
        ThrowHelper.ThrowIfNull(componentType, nameof(componentType));
        ThrowHelper.ThrowIfNull(token, nameof(token));
        if (token.ComponentType != componentType)
        {
            ThrowHelper.ThrowGeneratedComponentTypeTokenMismatch(componentType, token.ComponentType);
        }

        lock (Gate)
        {
            Tokens.TryAdd(componentType, token);
        }
    }

    /// <summary>Gets a generated token when available, otherwise an unconstrained token.</summary>
    internal static IGeneratedComponentTypeToken Get<T>()
    {
        lock (Gate)
        {
            return Tokens.TryGetValue(typeof(T), out IGeneratedComponentTypeToken? token)
                ? token
                : GeneratedComponentTypeToken<T>.Instance;
        }
    }
}

/// <summary>Performs constrained dispatch and fails before generic execution when a type does not qualify.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedComponentTypeDispatch
{
    /// <summary>Dispatches a registered value type.</summary>
    public static void DispatchStruct<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedStructComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedStructComponentTypeToken typed)
        {
            typed.DispatchStruct(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "struct");
    }

    /// <summary>Dispatches a registered unmanaged type.</summary>
    public static void DispatchUnmanaged<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedUnmanagedComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedUnmanagedComponentTypeToken typed)
        {
            typed.DispatchUnmanaged(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "unmanaged");
    }

    /// <summary>Dispatches a registered reference type.</summary>
    public static void DispatchClass<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedClassComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedClassComponentTypeToken typed)
        {
            typed.DispatchClass(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "class");
    }

    /// <summary>Dispatches a registered reference type for a class? constraint.</summary>
    public static void DispatchNullableClass<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedNullableClassComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedNullableClassComponentTypeToken typed)
        {
            typed.DispatchNullableClass(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "class?");
    }

    /// <summary>Dispatches a registered type with a public parameterless constructor.</summary>
    public static void DispatchConstructible<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedConstructibleComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedConstructibleComponentTypeToken typed)
        {
            typed.DispatchConstructible(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "new()");
    }

    /// <summary>Dispatches a registered class with a public parameterless constructor.</summary>
    public static void DispatchClassConstructible<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedClassConstructibleComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedClassConstructibleComponentTypeToken typed)
        {
            typed.DispatchClassConstructible(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "class, new()");
    }

    /// <summary>Dispatches a registered class? with a public parameterless constructor.</summary>
    public static void DispatchNullableClassConstructible<TVisitor>(IGeneratedComponentTypeToken token, ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedNullableClassConstructibleComponentTypeVisitor
    {
        ThrowHelper.ThrowIfNull(token, nameof(token));

        if (token is IGeneratedNullableClassConstructibleComponentTypeToken typed)
        {
            typed.DispatchNullableClassConstructible(remaining, ref visitor);
            return;
        }

        ThrowConstraintMismatch(token, "class?, new()");
    }

    private static void ThrowConstraintMismatch(IGeneratedComponentTypeToken token, string constraint)
        => ThrowHelper.ThrowGeneratedComponentConstraintMismatch(token, constraint);
}

internal sealed class GeneratedComponentTypeToken<T> : IGeneratedComponentTypeToken
{
    internal static readonly GeneratedComponentTypeToken<T> Instance = new();

    private GeneratedComponentTypeToken() { }

    public Type ComponentType => typeof(T);

    public void Dispatch<TVisitor>(ReadOnlySpan<IGeneratedComponentTypeToken> remaining, ref TVisitor visitor)
        where TVisitor : struct, IGeneratedComponentTypeVisitor
        => visitor.Visit<T>(remaining);
}
