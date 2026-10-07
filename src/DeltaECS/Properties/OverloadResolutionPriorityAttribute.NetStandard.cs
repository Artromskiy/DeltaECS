#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    using System;

    /// <summary>Provides overload priority metadata for older target frameworks.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OverloadResolutionPriorityAttribute : Attribute
    {
        /// <summary>Initializes the overload priority.</summary>
        public OverloadResolutionPriorityAttribute(int priority) => Priority = priority;

        /// <summary>Gets the overload priority.</summary>
        public int Priority { get; }
    }
}
#endif
