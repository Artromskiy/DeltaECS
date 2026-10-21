namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyEntityData()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(936_001));
        ComponentId velocityId = layouts.Register<Velocity>(new SchemaId(936_002));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(936_003));
        using var world = new World(layouts);

        var data = new EntityData();
        Position initialPosition = new() { Value = 10 };
        Require(data.Count == 0);
        Require(data.Add(positionId, in initialPosition));
        Require(!data.Add(positionId, in initialPosition));
        data.AddOrSet(positionId, in initialPosition);
        ref Position storedPosition = ref data.GetRef<Position>(positionId);
        storedPosition.Value = 20;
        ref readonly Position readPosition = ref data.GetReadRef<Position>(positionId);
        Require(readPosition.Value == 20);
        Require(data.TryGet<Position>(positionId, out Position copiedPosition));
        Require(copiedPosition.Value == 20 && data.Get<Position>(positionId).Value == 20);
        Require(data.AddTag(markerId));
        ReadOnlySpan<ComponentId> dataIds = data.ComponentIds;
        Require(dataIds.Length == 2 && dataIds[0] == positionId && dataIds[1] == markerId);

        Entity entity = world.Create(data);
        Require(world.Get<Position>(entity, positionId).Value == 20);
        Require(world.Has(entity, markerId));

        var additions = new EntityData();
        Position ignoredPosition = new() { Value = 30 };
        Velocity velocity = new() { Value = 4 };
        additions.Add(positionId, in ignoredPosition);
        additions.Add(velocityId, in velocity);
        additions.AddTag(markerId);
        Require(world.Add(entity, additions) == 1);
        Require(world.Get<Position>(entity, positionId).Value == 20);

        Position updatedPosition = new() { Value = 40 };
        additions.Set(positionId, in updatedPosition);
        Require(world.Set(entity, additions) == 3);
        Require(world.Get<Position>(entity, positionId).Value == 40);

        var captured = new EntityData();
        Require(captured.Add(world, entity) == 3);
        Require(captured.Has(positionId) && captured.Has(velocityId) && captured.Has(markerId));
        Require(captured.Get<Position>(positionId).Value == 40);
        Require(captured.Set(world, entity) == 3);
        captured.AddOrSet(world, entity);

        world.AddOrSet(entity, data);
        Require(world.Get<Position>(entity, positionId).Value == 20);
        Require(data.Remove(markerId));
        data.Clear();
        Require(data.Count == 0 && data.ComponentIds.IsEmpty);
    }
}
