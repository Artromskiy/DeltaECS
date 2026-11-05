namespace Delta.ECS.Runtime.Consumer;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static partial class RuntimeApiGrammarProof
{
    private static void VerifyComponentChanges()
    {
        var layouts = new ComponentRegistry();
        ComponentId positionId = layouts.Register<Position>(new SchemaId(937_001));
        using var world = new World(layouts);
        Entity existing = world.Create(positionId);
        var changes = new List<(Entity Entity, ChangeKind Kind, int Value, bool HasValue)>();
        ComponentChangeOperation<Position> operation = world.ForEachChange<Position>(positionId,
            (Entity entity, ChangeKind kind, ref Position component) =>
            {
                bool hasValue = !Unsafe.IsNullRef(ref component);
                changes.Add((entity, kind, hasValue ? component.Value : 0, hasValue));
            });

        operation.Invoke();
        Require(changes.Count == 1);
        Require(changes[0] == (existing, ChangeKind.Added, 0, true));

        operation.Invoke();
        Require(changes.Count == 1);

        Entity added = world.Create(positionId);
        operation.Invoke();
        Require(changes.Count == 2);
        Require(changes[1] == (added, ChangeKind.Added, 0, true));

        world.GetRef<Position>(existing, positionId).Value = 12;
        operation.Invoke();
        Require(changes.Count == 3);
        Require(changes[2] == (existing, ChangeKind.Modified, 12, true));

        Require(world.Remove(existing, positionId));
        operation.Invoke();
        Require(changes.Count == 4);
        Require(changes[3] == (existing, ChangeKind.Removed, 0, false));

        ComponentId markerId = layouts.Register<Marker>(new SchemaId(937_002));
        Query active = world.WhereNone(markerId);
        changes.Clear();
        ComponentChangeOperation<Position> filtered = world.ForEachChange<Position>(in active, positionId,
            (Entity entity, ChangeKind kind, ref Position component) =>
            {
                bool hasValue = !Unsafe.IsNullRef(ref component);
                changes.Add((entity, kind, hasValue ? component.Value : 0, hasValue));
            });
        filtered.Invoke();
        Require(changes.Count == 1 && changes[0] == (added, ChangeKind.Added, 0, true));
        world.Add(added, markerId);
        filtered.Invoke();
        Require(changes.Count == 2 && changes[1] == (added, ChangeKind.Removed, 0, false));
        world.Remove(added, markerId);
        filtered.Invoke();
        Require(changes.Count == 3 && changes[2] == (added, ChangeKind.Added, 0, true));
    }
}
