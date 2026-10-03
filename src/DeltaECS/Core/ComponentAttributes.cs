using System.ComponentModel;

namespace Delta.ECS
{
    /// <summary>Marks a component for generated registration.</summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class DeltaEcsComponentAttribute : Attribute
    {
        /// <summary>An explicitly assigned stable schema ID; zero requests a generated name-based ID.</summary>
        public ulong SchemaId { get; set; }
    }

    /// <summary>Generated strongly typed registration for one marked component.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IGeneratedComponentRegistration
    {
        /// <summary>Gets the CLR type registered by this entry.</summary>
        Type ComponentType { get; }

        /// <summary>Gets the stable schema ID assigned to the component.</summary>
        SchemaId SchemaId { get; }

        /// <summary>Registers the component with a layout registry.</summary>
        ComponentId Register(ComponentLayoutRegistry layouts);
    }

    /// <summary>Receives component registration factories emitted by DeltaECS.Generators.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class GeneratedComponentRegistrationRegistry
    {
        private static readonly object Gate = new();
        private static readonly List<IGeneratedComponentRegistration> Registrations = new();

        /// <summary>Registers a generated component factory during module initialization.</summary>
        public static void Register(IGeneratedComponentRegistration registration)
        {
            ThrowHelper.ThrowIfNull(registration, nameof(registration));

            lock (Gate)
            {
                Registrations.Add(registration);
            }
        }

        /// <summary>Copies generated registrations for one-time Unity world initialization.</summary>
        public static IGeneratedComponentRegistration[] GetRegistrations()
        {
            lock (Gate)
            {
                return Registrations.ToArray();
            }
        }
    }

    /// <summary>Creates layout registries from component registrations emitted by DeltaECS.Generators.</summary>
    public static class GeneratedComponentCatalog
    {
        /// <summary>Creates a registry containing every component in the generated catalog.</summary>
        public static ComponentLayoutRegistry CreateLayoutRegistry()
        {
            var layouts = new ComponentLayoutRegistry();
            RegisterLayouts(layouts);
            return layouts;
        }

        /// <summary>Adds every generated component layout to an existing registry.</summary>
        public static void RegisterLayouts(ComponentLayoutRegistry layouts)
        {
            ThrowHelper.ThrowIfNull(layouts, nameof(layouts));

            IGeneratedComponentRegistration[] registrations = GeneratedComponentRegistrationRegistry.GetRegistrations();
            Array.Sort(registrations, static (left, right) => StringComparer.Ordinal.Compare(
                left.ComponentType.FullName,
                right.ComponentType.FullName));

            var registeredTypes = new HashSet<Type>();
            var typesBySchema = new Dictionary<SchemaId, Type>();
            for (int index = 0; index < registrations.Length; index++)
            {
                IGeneratedComponentRegistration registration = registrations[index];
                Type componentType = registration.ComponentType;
                if (registration.SchemaId.Value == 0)
                {
                    ThrowHelper.ThrowGeneratedComponentSchemaIdZero(componentType);
                }

                if (!registeredTypes.Add(componentType))
                {
                    ThrowHelper.ThrowGeneratedComponentRegistrationConflict(componentType);
                }

                if (typesBySchema.TryGetValue(registration.SchemaId, out Type? existingType))
                {
                    ThrowHelper.ThrowGeneratedComponentSchemaIdConflict(registration.SchemaId, existingType, componentType);
                }

                typesBySchema.Add(registration.SchemaId, componentType);
            }

            for (int index = 0; index < registrations.Length; index++)
            {
                IGeneratedComponentRegistration registration = registrations[index];
                registration.Register(layouts);
            }
        }
    }
}
