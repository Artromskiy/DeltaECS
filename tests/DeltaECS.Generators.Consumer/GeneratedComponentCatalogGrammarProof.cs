using System;

using Delta.ECS;

namespace Delta.ECS.Generators.Consumer;

internal static class GeneratedComponentCatalogGrammarProof
{
    internal static void Run()
    {
        IGeneratedComponentRegistration[] registrations = GeneratedComponentCatalog.GetRegistrations();
        int componentRegistrationCount = 0;
        int tagRegistrationCount = 0;
        foreach (IGeneratedComponentRegistration registration in registrations)
        {
            if (registration.ComponentType == typeof(CatalogComponent))
            {
                componentRegistrationCount++;
            }

            if (registration.ComponentType == typeof(CatalogTag))
            {
                tagRegistrationCount++;
            }
        }

        if (componentRegistrationCount != 1 || tagRegistrationCount != 1)
        {
            throw new InvalidOperationException("The generated component catalog did not return exactly one registration for each fixture type.");
        }

        IGeneratedComponentRegistration componentRegistration = GeneratedComponentCatalog.GetRegistration<CatalogComponent>();
        IGeneratedComponentRegistration tagRegistration = GeneratedComponentCatalog.GetRegistration<CatalogTag>();
        if (componentRegistration.ComponentType != typeof(CatalogComponent)
            || tagRegistration.ComponentType != typeof(CatalogTag))
        {
            throw new InvalidOperationException("The generated component catalog returned an unexpected component registration.");
        }

        var layouts = new ComponentRegistry();
        ComponentId componentId = layouts.Register(componentRegistration);
        ComponentId tagId = layouts.Register(tagRegistration);
        layouts.AddGroup(CatalogTargets.Client, componentId);
        using var world = new World(layouts);
        ReadOnlySpan<ComponentId> historyGroup = world.GetGroup(CatalogGroups.History);
        ReadOnlySpan<ComponentId> replicatedGroup = world.GetGroup(CatalogGroups.Replicated);
        ReadOnlySpan<ComponentId> serverGroup = world.GetGroup(CatalogTargets.Server);
        ReadOnlySpan<ComponentId> clientGroup = world.GetGroup(CatalogTargets.Client);
        Entity entity = world.Create(componentId);
        if (!world.Add(entity, tagId)
            || world.Components.GetComponentType(componentId) != typeof(CatalogComponent)
            || world.Components.GetComponentType(tagId) != typeof(CatalogTag)
            || world.GetAlias("catalog.component") != componentId
            || historyGroup.Length != 2
            || !historyGroup.Contains(componentId)
            || !historyGroup.Contains(tagId)
            || replicatedGroup.Length != 1
            || !replicatedGroup.Contains(componentId)
            || serverGroup.Length != 1
            || !serverGroup.Contains(componentId))
        {
            throw new InvalidOperationException("Generated component catalog registration did not initialize component groups.");
        }

        layouts.AddAlias("catalog.component.manual", componentId);
        if (clientGroup.Length != 1 || !clientGroup.Contains(componentId)
            || !world.TryGetAlias("catalog.component.manual", out ComponentId manualAlias)
            || manualAlias != componentId)
        {
            throw new InvalidOperationException("The world component alias or group APIs returned unexpected values.");
        }
    }
}
