using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedComponentVisitorGrammarProof
{
    internal static void Run(ComponentLayoutRegistry layouts)
    {
        ComponentId[] componentIds =
        [
            layouts.GetPrimary<Cmp1>(),
            layouts.GetPrimary<Cmp2>(),
            layouts.GetPrimary<Cmp3>(),
            layouts.GetPrimary<Cmp4>(),
            layouts.GetPrimary<Dead>(),
            layouts.GetPrimary<Alive>(),
            layouts.GetPrimary<NeedsRespawn>()
        ];
        Type[] expectedTypes =
        [
            typeof(Cmp1),
            typeof(Cmp2),
            typeof(Cmp3),
            typeof(Cmp4),
            typeof(Dead),
            typeof(Alive),
            typeof(NeedsRespawn)
        ];
        var visitor = new RecordingVisitor(componentIds, expectedTypes);

        foreach (ComponentId componentId in componentIds)
        {
            Require(layouts.TryVisit(componentId, visitor));
        }

        Require(visitor.VisitCount == componentIds.Length);
        Require(!layouts.TryVisit(ComponentId.Invalid, visitor));
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Generated component visitor grammar proof failed.");
        }
    }

    private sealed class RecordingVisitor(ComponentId[] componentIds, Type[] expectedTypes) : IUnconstrainedVisitor
    {
        private int _nextIndex;

        internal int VisitCount => _nextIndex;

        public void Visit<TComponent>(ComponentId componentId)
        {
            if (_nextIndex >= componentIds.Length
                || componentIds[_nextIndex] != componentId
                || expectedTypes[_nextIndex] != typeof(TComponent))
            {
                throw new InvalidOperationException("The generated visitor received an unexpected component route.");
            }

            _nextIndex++;
        }
    }
}
