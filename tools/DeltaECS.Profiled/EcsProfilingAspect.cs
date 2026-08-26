using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.DeclarationBuilders;
using Metalama.Framework.Fabrics;

namespace DeltaECS.Profiled;

[AttributeUsage(AttributeTargets.Method)]
public sealed class EcsProfileMethodAttribute : OverrideMethodAspect
{
    public override void BuildAspect(IAspectBuilder<IMethod> builder)
    {
        string methodName = builder.Target.ToDisplayString();
        int methodId = StableMethodId(methodName);
        builder.With(builder.Target).IntroduceAttribute(
            AttributeConstruction.Create(
                typeof(DeltaECS.Profiling.ProfiledMethodMetadataAttribute),
                [methodId, methodName]),
            OverrideStrategy.New);
        base.BuildAspect(builder);
    }

    public override dynamic? OverrideMethod()
    {
        int methodId = StableMethodId(meta.Target.Method.ToDisplayString());
        DeltaECS.Profiling.ProfilerRuntime.Enter(methodId);
        try
        {
            return meta.Proceed();
        }
        finally
        {
            DeltaECS.Profiling.ProfilerRuntime.Leave(methodId);
        }
    }

    [CompileTime]
    private static int StableMethodId(string methodName)
    {
        unchecked
        {
            uint hash = 2_166_136_261;
            foreach (char character in methodName)
            {
                hash ^= character;
                hash *= 16_777_619;
            }

            return (int)(hash & 0x7FFF_FFFF);
        }
    }
}

public sealed class EcsProfilingFabric : ProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        amender
            .SelectMany(compilation => compilation.AllTypes)
            .Where(static type => type.ContainingNamespace.ToDisplayString()
                .StartsWith("Delta.ECS", StringComparison.Ordinal))
            .SelectMany(static type => type.Methods)
            .Where(static method => method.Name is not ".ctor" and not ".cctor")
            .AddAspectIfEligible<EcsProfileMethodAttribute>();
    }
}
