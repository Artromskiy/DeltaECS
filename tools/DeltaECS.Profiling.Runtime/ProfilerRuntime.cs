using System.Runtime.CompilerServices;
using System.Reflection;

namespace DeltaECS.Profiling;

/// <summary>Process-local entry point used by Metalama-instrumented methods.</summary>
public static class ProfilerRuntime
{
    [ThreadStatic]
    private static CallProfiler? s_current;

    /// <summary>Starts collection on the current thread.</summary>
    public static CallProfiler Start(int maxDepth = 32, int sampleCapacity = 1_048_576)
    {
        var profiler = new CallProfiler(maxDepth, sampleCapacity);
        s_current = profiler;
        return profiler;
    }

    /// <summary>Reattaches an existing warmed collector on the current thread.</summary>
    public static void Attach(CallProfiler profiler)
    {
        ArgumentNullException.ThrowIfNull(profiler);
        if (s_current is not null)
        {
            throw new InvalidOperationException("A profiler is already active on this thread.");
        }

        s_current = profiler;
    }

    /// <summary>Stops collection without formatting the captured samples.</summary>
    public static CallProfiler? Detach()
    {
        CallProfiler? profiler = s_current;
        s_current = null;
        return profiler;
    }

    /// <summary>Stops collection and writes a Markdown report.</summary>
    public static void Stop(TextWriter writer)
        => Stop(writer, methodNames: null);

    /// <summary>Stops collection and resolves method names after profiling.</summary>
    public static void Stop(TextWriter writer, IReadOnlyDictionary<int, string>? methodNames)
    {
        ArgumentNullException.ThrowIfNull(writer);
        CallProfiler? profiler = Detach();
        profiler?.WriteMarkdown(writer, methodNames);
    }

    /// <summary>Stops collection and writes a Markdown report to a file.</summary>
    public static void StopToFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(fullPath);
        CallProfiler? profiler = Detach();
        profiler?.WriteMarkdown(writer);
    }

    /// <summary>Marks an instrumented method entry. Called only from woven IL.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Enter(int methodId)
    {
        s_current?.EnterMethod(methodId);
    }

    /// <summary>Marks an instrumented method exit. Called only from woven IL.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Leave(int methodId)
    {
        s_current?.ExitMethod(methodId);
    }

    /// <summary>Builds the method-name map from compile-time Metalama metadata.</summary>
    public static Dictionary<int, string> LoadMethodNames(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var result = new Dictionary<int, string>();
        foreach (Type type in assembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                         BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.Instance
                         | BindingFlags.Static
                         | BindingFlags.DeclaredOnly))
            {
                foreach (ProfiledMethodMetadataAttribute metadata in
                         method.GetCustomAttributes<ProfiledMethodMetadataAttribute>())
                {
                    if (result.TryGetValue(metadata.MethodId, out string? existingName)
                        && !string.Equals(existingName, metadata.Name, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Profile method ID collision between '{existingName}' and '{metadata.Name}'.");
                    }

                    result[metadata.MethodId] = metadata.Name;
                }
            }
        }

        return result;
    }

}

/// <summary>Compile-time method identity emitted by the profiling-only Metalama build.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ProfiledMethodMetadataAttribute : Attribute
{
    public ProfiledMethodMetadataAttribute(int methodId, string name)
    {
        MethodId = methodId;
        Name = name;
    }

    public int MethodId { get; }

    public string Name { get; }
}
