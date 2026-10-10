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

        var layouts = new ComponentLayoutRegistry();
        layouts.Register(componentRegistration);
        layouts.Register(tagRegistration);

        ComponentId componentId = layouts.GetPrimary<CatalogComponent>();
        ComponentId tagId = layouts.GetPrimary<CatalogTag>();
        using var world = new World(layouts);
        Entity entity = world.Create(componentId);
        if (!world.Add(entity, tagId)
            || layouts.GetComponentType(componentId) != typeof(CatalogComponent)
            || layouts.GetComponentType(tagId) != typeof(CatalogTag))
        {
            throw new InvalidOperationException("Generated component catalog registration did not initialize the layout.");
        }
    }
}
