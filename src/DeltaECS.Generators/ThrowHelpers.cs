namespace Delta.ECS.Generators;

using System;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

internal static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ITypeSymbol ThrowValidatedComponentTypeUnavailable()
        => throw new InvalidOperationException("Validated component type was unexpectedly unavailable.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string ThrowValidatedContextTypeUnavailable()
        => throw new InvalidOperationException("Validated context type was unexpectedly unavailable.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string ThrowMethodGroupTargetMissing(IMethodSymbol method)
        => throw new ArgumentException("The method group target must belong to a type.", nameof(method));
}
