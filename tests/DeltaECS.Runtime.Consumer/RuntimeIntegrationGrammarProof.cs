namespace Delta.ECS.Runtime.Consumer;

using Delta.ECS.Integration;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyIntegration()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(935_001));
        ComponentId markerId = layouts.Register<Marker>(new SchemaId(935_002));
        using var world = new World(layouts);
        IEcsWorld integration = world;

        RuntimeComponentCatalog catalog = integration.Catalog;
        Require(catalog.Components.Span.Length == 2);
        Require(catalog.Components.Span[0].Id == positionId);
        Require(catalog.Components.Span[1].Id == markerId);
        Require(catalog.Components.Span[1].IsTag);
        ComponentDescriptor position = catalog.Components.Span[0];
        Require(position.Schema == new SchemaId(935_001));
        Require(position.Name == typeof(Position).FullName);
        Require(position.ValueType == typeof(Position));
        Require(position.Capabilities == (ComponentCapabilities.Read | ComponentCapabilities.Write));
        Require(!position.AllowsNull && !position.IsTag);

        integration.Initialize();
        integration.Update();
        Entity entity = integration.Create([positionId]);
        Require(integration.IsAlive(entity));

        Span<ComponentId> components = stackalloc ComponentId[2];
        Require(integration.TryGetComponents(entity, components, out int componentCount));
        Require(componentCount == 1);
        Require(components[0] == positionId);

        Require(integration.TryRead(entity, positionId, out ComponentSnapshot snapshot, out EcsReadError readError));
        Require(readError.Code == EcsReadErrorCode.None);
        Position value = (Position)snapshot.Value!;
        value.Value = 77;
        Require(integration.TryWrite(
            entity,
            positionId,
            value,
            out EcsWriteError writeError));
        Require(writeError.Code == EcsWriteErrorCode.None);
        Require(integration.TryRead(entity, positionId, out ComponentSnapshot updated, out _));
        Require(((Position)updated.Value!).Value == 77);

        Require(integration.Add(entity, [markerId]));
        Require(integration.Remove(entity, [markerId]));
        Require(integration.Destroy(entity));
        Require(!integration.IsAlive(entity));
        integration.Shutdown();
    }
}
