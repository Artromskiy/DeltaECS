using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace Delta.ECS.Unity
{
    internal static class UnityThrowHelpers
    {
        internal static T ThrowArgumentNull<T>(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        internal static void ThrowArgumentNull(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        internal static void ThrowArgument(string message, string parameterName)
        {
            throw new ArgumentException(message, parameterName);
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperation(string message)
        {
            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        internal static void ThrowObjectDisposed(string objectName)
        {
            throw new ObjectDisposedException(objectName);
        }

        internal static T Rethrow<T>(Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
            return default;
        }
    }
}
