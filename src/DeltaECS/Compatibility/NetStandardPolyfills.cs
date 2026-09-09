#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [System.Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        All = -1,
    }

    [System.AttributeUsage(
        System.AttributeTargets.Field
        | System.AttributeTargets.ReturnValue
        | System.AttributeTargets.GenericParameter
        | System.AttributeTargets.Parameter
        | System.AttributeTargets.Property,
        Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : System.Attribute
    {
        internal DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }

        internal DynamicallyAccessedMemberTypes MemberTypes { get; }
    }
}
#endif
