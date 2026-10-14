namespace Delta.ECS;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

internal sealed class ComponentRowArrayPool
{
    private const long SharedArrayByteLimit = 8L * 1024 * 1024;
    private const int SharedTypeLimit = 512;
    private static readonly object s_sharedArraysLock = new();
    private static readonly ConditionalWeakTable<Type, ElementSize> s_elementSizes = new();
    private static readonly ConcurrentDictionary<Type, ConcurrentStack<SharedArray>> s_sharedArrays = new();
    private static readonly PropertyInfo? s_collectibleAssemblyProperty = typeof(Assembly).GetProperty("IsCollectible");
    private static long s_sharedArrayBytes;
    private static int s_sharedArrayCount;
    private static int s_sharedPoolGeneration;

    private readonly Dictionary<Type, Stack<Array>> _arrays = new();
    private Dictionary<Type, ConcurrentStack<SharedArray>?>? _sharedArrayBuckets;
    private int _sharedPoolGeneration = -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Array Rent(Type runtimeType, ComponentRowOperations rowOperations, int capacity)
    {
        if (_arrays.TryGetValue(runtimeType, out Stack<Array>? arrays)
            && arrays.Count != 0)
        {
            return arrays.Pop();
        }

        return RentFromSharedOrCreate(runtimeType, rowOperations, capacity);
    }

    internal void Return(Array array)
    {
        Array.Clear(array, 0, array.Length);
        Type? runtimeType = array.GetType().GetElementType();
        if (runtimeType is null)
        {
            ThrowHelper.ThrowArrayRowsRequiresRuntimeType();
            return;
        }

        if (!_arrays.TryGetValue(runtimeType, out Stack<Array>? arrays))
        {
            arrays = new Stack<Array>();
            _arrays.Add(runtimeType, arrays);
        }

        arrays.Push(array);
    }

    internal void Clear()
    {
        if (_arrays.Count == 0)
        {
            return;
        }

        lock (s_sharedArraysLock)
        {
            bool sharedArraysAdded = false;
            foreach (KeyValuePair<Type, Stack<Array>> entry in _arrays)
            {
                if (UsesCollectibleAssembly(entry.Key))
                {
                    continue;
                }
                while (entry.Value.TryPop(out Array? array))
                {
                    long byteCount = GetArrayByteCount(array, entry.Key);
                    long sharedBytes = Interlocked.Read(ref s_sharedArrayBytes);
                    if (byteCount > SharedArrayByteLimit || sharedBytes > SharedArrayByteLimit - byteCount)
                    {
                        continue;
                    }

                    if (!s_sharedArrays.TryGetValue(entry.Key, out ConcurrentStack<SharedArray>? sharedArrays))
                    {
                        if (s_sharedArrays.Count >= SharedTypeLimit)
                        {
                            continue;
                        }

                        sharedArrays = s_sharedArrays.GetOrAdd(entry.Key, static _ => new ConcurrentStack<SharedArray>());
                    }

                    sharedArrays.Push(new SharedArray(array, byteCount));
                    Interlocked.Add(ref s_sharedArrayBytes, byteCount);
                    Interlocked.Increment(ref s_sharedArrayCount);
                    sharedArraysAdded = true;
                }
            }

            _arrays.Clear();
            if (sharedArraysAdded)
            {
                Interlocked.Increment(ref s_sharedPoolGeneration);
            }
        }
    }

    private static bool TryPop(ConcurrentStack<SharedArray>? sharedArrays, out Array array)
    {
        if (sharedArrays is not null
            && sharedArrays.TryPop(out SharedArray sharedArray))
        {
            Interlocked.Add(ref s_sharedArrayBytes, -sharedArray.ByteCount);
            Interlocked.Decrement(ref s_sharedArrayCount);
            array = sharedArray.Array;
            return true;
        }

        array = null!;
        return false;
    }

    private static long GetArrayByteCount(Array array, Type runtimeType)
    {
        ElementSize elementSize = s_elementSizes.GetValue(runtimeType, static type => new ElementSize(EstimateElementSize(type)));
        return checked((long)array.LongLength * elementSize.Bytes + (3 * IntPtr.Size));
    }

    private static int EstimateElementSize(Type runtimeType)
    {
        if (!runtimeType.IsValueType || runtimeType.IsPointer || runtimeType.IsByRef)
        {
            return IntPtr.Size;
        }

        if (runtimeType.IsEnum)
        {
            return EstimateElementSize(Enum.GetUnderlyingType(runtimeType));
        }

        TypeCode typeCode = Type.GetTypeCode(runtimeType);
        int primitiveSize = typeCode switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double or TypeCode.DateTime => 8,
            TypeCode.Decimal => 16,
            _ => 0
        };
        if (primitiveSize != 0)
        {
            return primitiveSize;
        }

        long estimatedSize = Math.Max(0, runtimeType.StructLayoutAttribute?.Size ?? 0);
        foreach (FieldInfo field in runtimeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            estimatedSize += EstimateElementSize(field.FieldType) + (2 * IntPtr.Size) - 1;
        }

        return (int)Math.Max(1, Math.Min(int.MaxValue, estimatedSize));
    }

    private static bool UsesCollectibleAssembly(Type runtimeType)
    {
        if (s_collectibleAssemblyProperty?.GetValue(runtimeType.Assembly) is true)
        {
            return true;
        }

        if (runtimeType.HasElementType && runtimeType.GetElementType() is { } elementType && UsesCollectibleAssembly(elementType))
        {
            return true;
        }

        if (runtimeType.IsGenericType)
        {
            foreach (Type argument in runtimeType.GetGenericArguments())
            {
                if (UsesCollectibleAssembly(argument))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Array RentFromSharedOrCreate(Type runtimeType, ComponentRowOperations rowOperations, int capacity)
    {
        if (Volatile.Read(ref s_sharedArrayCount) > 0 && TryRentShared(runtimeType, out Array? sharedArray))
        {
            return sharedArray;
        }

        return rowOperations.CreateArray(capacity);
    }

    private bool TryRentShared(Type runtimeType, out Array array)
    {
        int generation = Volatile.Read(ref s_sharedPoolGeneration);
        if (_sharedPoolGeneration != generation)
        {
            _sharedArrayBuckets?.Clear();
            _sharedPoolGeneration = generation;
        }

        if (_sharedArrayBuckets is not null
            && _sharedArrayBuckets.TryGetValue(runtimeType, out ConcurrentStack<SharedArray>? cachedArrays))
        {
            if (TryPop(cachedArrays, out array))
            {
                return true;
            }

            array = null!;
            return false;
        }

        if (s_sharedArrays.TryGetValue(runtimeType, out ConcurrentStack<SharedArray>? sharedArrays))
        {
            (_sharedArrayBuckets ??= new Dictionary<Type, ConcurrentStack<SharedArray>?>()).Add(runtimeType, sharedArrays);
        }
        else
        {
            (_sharedArrayBuckets ??= new Dictionary<Type, ConcurrentStack<SharedArray>?>()).Add(runtimeType, null);
            array = null!;
            return false;
        }

        return TryPop(sharedArrays, out array);
    }

    private readonly struct SharedArray(Array array, long byteCount)
    {
        internal Array Array { get; } = array;

        internal long ByteCount { get; } = byteCount;
    }

    private sealed class ElementSize(int bytes)
    {
        internal int Bytes { get; } = bytes;
    }
}
