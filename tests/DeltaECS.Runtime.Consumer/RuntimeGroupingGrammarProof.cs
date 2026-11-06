namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyGrouping()
    {
        var layouts = new ComponentRegistry();
        ComponentId id = layouts.Register<GroupingValue>(new SchemaId(939_001), group: true);
        ComponentId alternative = layouts.Register<GroupingValue>(new SchemaId(939_002), group: true);
        using var world = new World(layouts);
        Entity entity = world.Create(id);
        world.Set(entity, id, new GroupingValue(1));
        Query selected = world.WhereAll(new GroupingValue(1));
        Require(selected.First().Invoke() == entity);
        Query alternate = world.WhereAll(alternative, new GroupingValue(2));
        Require(!alternate.First().Invoke().IsValid);
        world.GetRef<GroupingValue>(entity).Value = 2;
        Require(!selected.First().Invoke().IsValid);
        Require(world.WhereAll(id).WhereAll(id, new GroupingValue(2)).First().Invoke() == entity);
        Require(world.WhereAny(new GroupingValue(2)).First().Invoke() == entity);
        Require(world.WhereNone(new GroupingValue(1)).First().Invoke() == entity);
        Require(!world.WhereNone(id, new GroupingValue(2)).First().Invoke().IsValid);
        Require(world.WhereAny(new GroupingValue(1)).WhereAny(id, new GroupingValue(2)).First().Invoke() == entity);
        Require(layouts.GetComponentIds<GroupingValue>().Length == 2);
        Require(layouts.TryGetComponentId(new SchemaId(939_001), out ComponentId resolved) && resolved == id);
    }

    private record struct GroupingValue(int Value);
}
