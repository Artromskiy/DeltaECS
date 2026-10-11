namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyArchetypeIteration()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId firstId = layouts.Register<FirstValue>(new SchemaId(934_031));
        ComponentId secondId = layouts.Register<SecondValue>(new SchemaId(934_032));
        using var world = new World(layouts);
        Entity first = world.Create(firstId);
        Entity second = world.Create(secondId);
        Entity both = world.Create(stackalloc ComponentId[] { firstId, secondId });
        var visited = new List<(int EntityIndex, Type ComponentType)>();
        var operation = world.ForEachArchetype(visited);
        var registration = new QueryProcessorRegistration(world, operation);

        foreach (ComponentId id in new[] { firstId, secondId })
        {
            Require(layouts.TryVisit(id, registration));
        }

        operation.Invoke(ref visited);

        Require(visited.Count == 4);
        Require(visited.Contains((first.Index, typeof(FirstValue))));
        Require(visited.Contains((second.Index, typeof(SecondValue))));
        Require(visited.Contains((both.Index, typeof(FirstValue))));
        Require(visited.Contains((both.Index, typeof(SecondValue))));
    }

    private sealed class QueryProcessorRegistration : IUnconstrainedVisitor
    {
        private readonly World _world;
        private readonly ArchetypeForEachOperation<List<(int EntityIndex, Type ComponentType)>> _operation;

        internal QueryProcessorRegistration(
            World world,
            ArchetypeForEachOperation<List<(int EntityIndex, Type ComponentType)>> operation)
        {
            _world = world;
            _operation = operation;
        }

        public void Visit<T>(ComponentId componentId)
        {
            Query query = _world.WhereAll(componentId);
            _operation.Process<T, RecordComponent<T>>(in query, componentId, new RecordComponent<T>());
        }
    }

    private struct RecordComponent<T> : IArchetypeForEachComponent<List<(int EntityIndex, Type ComponentType)>, T>
    {
        public void Invoke(ref List<(int EntityIndex, Type ComponentType)> context, EntityRef entity, ref T component)
            => context.Add((entity.Index, typeof(T)));
    }

    private record struct FirstValue(int Value);
    private record struct SecondValue(int Value);
}
