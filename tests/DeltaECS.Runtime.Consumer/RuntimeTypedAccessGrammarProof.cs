namespace Delta.ECS.Runtime.Consumer;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyTypedAccess()
    {
        var layouts = new ComponentLayoutRegistry();
        ComponentId primaryPositionId = layouts.Register<Position>(new SchemaId(933_001));
        ComponentId secondaryPositionId = layouts.Register<Position>(new SchemaId(933_002));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(933_003));
        using var world = new World(layouts);

        Entity primary = world.Create<Position>();
        ref Position primaryValue = ref world.GetRef<Position>(primary);
        primaryValue.Value = 10;
        Require(world.Get<Position>(primary).Value == 10);
        Require(world.TryGet<Position>(primary, out Position primaryCopy));
        Require(primaryCopy.Value == 10);
        Require(world.Has<Position>(primary));
        ref readonly Position primaryReadOnly = ref world.GetReadRef<Position>(primary);
        Require(primaryReadOnly.Value == 10);
        Require(world.TryGetComponentStamp<Position>(primary, out Stamp primaryStamp));
        Require(primaryStamp.Value > 0);

        Entity secondary = world.Create(secondaryPositionId);
        world.GetRef<Position>(secondary, secondaryPositionId).Value = 20;
        Require(world.TryGet<Position>(secondary, secondaryPositionId, out Position secondaryCopy));
        Require(secondaryCopy.Value == 20);
        Require(world.Has<Position>(secondary, secondaryPositionId));
        Require(world.Get<Position>(secondary, secondaryPositionId).Value == 20);
        ref readonly Position secondaryReadOnly = ref world.GetReadRef<Position>(secondary, secondaryPositionId);
        Require(secondaryReadOnly.Value == 20);
        Require(world.TryGetComponentStamp<Position>(secondary, secondaryPositionId, out Stamp secondaryStamp));
        Require(secondaryStamp.Value > 0);
        Require(!world.TryGet<Position>(secondary, primaryPositionId, out _));
        Require(!world.Has<Position>(secondary, primaryPositionId));

        Span<Entity> primaryBatch = stackalloc Entity[2];
        Require(world.Create<Position>(primaryBatch.Length, primaryBatch) == primaryBatch.Length);
        Require(world.Create<Position>(1) == 1);
        Span<Entity> secondaryBatch = stackalloc Entity[2];
        Require(world.Create<Position>(secondaryPositionId, secondaryBatch.Length, secondaryBatch) == secondaryBatch.Length);
        Require(world.Create<Position>(secondaryPositionId, 1) == 1);

        Entity added = world.Create(markerId);
        Position addedValue = new() { Value = 31 };
        Require(world.Add(added, in addedValue));
        Require(world.TryGet<Position>(added, out Position addedCopy));
        Require(addedCopy.Value == 31);

        Entity[] batch = [world.Create(markerId), world.Create(markerId)];
        Position batchValue = new() { Value = 40 };
        Require(world.Add(batch, in batchValue) == batch.Length);
        Require(world.Get<Position>(batch[0]).Value == 40);
        Require(world.Get<Position>(batch[1]).Value == 40);

        Require(world.Remove<Position>(added));
        Require(!world.Has<Position>(added));
        Require(world.Remove<Position>(batch) == batch.Length);
        Entity explicitPrimary = world.Create(markerId);
        Require(world.Add(explicitPrimary, in batchValue));
        Require(world.Remove<Position>(explicitPrimary, primaryPositionId));
        Require(world.Remove<Position>(secondaryBatch, secondaryPositionId) == secondaryBatch.Length);

        Entity tagged = world.Create<Marker>();
        Require(world.Has<Marker>(tagged));
        Require(world.TryGet<Marker>(tagged, out Marker markerValue));
        Require(markerValue.Equals(default(Marker)));
        Require(world.Get<Marker>(tagged).Equals(default(Marker)));
        Require(world.TryGetComponentStamp<Marker>(tagged, out Stamp tagStamp));
        Require(tagStamp == new Stamp(1));
        ref Marker markerReference = ref world.GetRef<Marker>(tagged);
        markerReference = default;
        Require(world.Add(tagged, in markerReference) == false);
        Require(world.Remove<Marker>(tagged));
        Require(!world.TryGetComponentStamp<Marker>(tagged, out _));

        Require(world.Layouts.GetComponentType(primaryPositionId) == typeof(Position));
    }
}
