using System;
using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

/// <summary>
/// Compile-time call-site matrix for generated API grammar branches in API-GRAMMAR.md.
/// The consumer project must compile these calls before the generator test suite can run.
/// </summary>
public static class GeneratedApiGrammarProof
{
    public const int Compiled = 1;

    /// <summary>Registers concrete fixture rows so the generic-binding generator discovers them.</summary>
    private static ComponentLayoutRegistry RegisterComponents()
    {
        var layouts = new ComponentLayoutRegistry();
        layouts.Register<Cmp1>(new SchemaId(920001));
        layouts.Register<Cmp2>(new SchemaId(920002));
        layouts.Register<Cmp3>(new SchemaId(920003));
        layouts.Register<Cmp4>(new SchemaId(920004));
        layouts.Register<Dead>(new SchemaId(920005));
        layouts.Register<Alive>(new SchemaId(920006));
        layouts.Register<NeedsRespawn>(new SchemaId(920007));
        return layouts;
    }

    /// <summary>Executes the generated grammar sections against a small, valid world.</summary>
    public static void Run()
    {
        var layouts = RegisterComponents();
        ComponentId cmp1Id = layouts.GetPrimary<Cmp1>();
        ComponentId cmp2Id = layouts.GetPrimary<Cmp2>();
        ComponentId cmp3Id = layouts.GetPrimary<Cmp3>();
        ComponentId cmp4Id = layouts.GetPrimary<Cmp4>();
        ComponentId deadId = layouts.GetPrimary<Dead>();
        ComponentId aliveId = layouts.GetPrimary<Alive>();
        GeneratedComponentVisitorGrammarProof.Run(layouts);
        ReadOnlySpan<ComponentId> allComponentIds = stackalloc ComponentId[]
        {
            cmp1Id,
            cmp2Id,
            cmp3Id,
            cmp4Id
        };
        ReadOnlySpan<ComponentId> componentIds = stackalloc ComponentId[] { cmp1Id, cmp2Id };
        ReadOnlySpan<ComponentId> singleComponentId = stackalloc ComponentId[] { cmp1Id };

        using var world = new World(layouts);
        var entityArray = new Entity[2];
        world.Create(allComponentIds, entityArray.Length, entityArray);
        Span<Entity> additionalEntity = stackalloc Entity[1];
        world.Create(allComponentIds, 1, additionalEntity);
        Entity entity = additionalEntity[0];

        for (int index = 0; index < entityArray.Length; index++)
        {
            world.GetRef<Cmp3>(entityArray[index], cmp3Id).Value = 2;
            world.GetRef<Cmp4>(entityArray[index], cmp4Id).Value = 1;
        }

        world.GetRef<Cmp3>(entity, cmp3Id).Value = 2;
        world.GetRef<Cmp4>(entity, cmp4Id).Value = 1;

        Query query = world.CreateQuery(QuerySpec.WhereAll(allComponentIds));
        ReadOnlySpan<Entity> entities = entityArray;
        Context context = default;
        Context readContext = new() { Value = 1 };

        GeneratedIterationGrammarProof.Run(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, singleComponentId, ref context, in readContext);
        GeneratedStampGrammarProof.Run(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, singleComponentId, ref context, in readContext);
        GeneratedQueryGrammarProof.Run(world, in query, cmp1Id, cmp2Id, componentIds);
        GeneratedWherePipelineGrammarProof.Run(world, in query, deadId, aliveId, ref context);
        GeneratedOpenGenericFunctorGrammarProof.Run(world, in query, entities, entityArray, cmp1Id, cmp2Id,
            componentIds, ref context);
        GeneratedOpenGenericRegistrationGrammarProof.Run();
        GeneratedOrderedIterationGrammarProof.Run(world, in query, cmp1Id, cmp2Id, componentIds, ref context);

        // Structural mutations consume and destroy the fixture entities, so run last.
        GeneratedStructuralGrammarProof.Run(world, in query, entities, entity, cmp1Id, cmp2Id, componentIds);
    }
}
