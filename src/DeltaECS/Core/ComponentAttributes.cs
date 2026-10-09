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

        /// <summary>Gets whether the component is a data-less tag.</summary>
        bool IsTag { get; }

        /// <summary>Registers this component using its generated concrete registration route.</summary>
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
        internal static IGeneratedComponentRegistration[] GetRegistrations()
        {
            lock (Gate)
            {
                return Registrations.ToArray();
            }
        }

        internal static IGeneratedComponentRegistration GetRegistration(Type componentType)
        {
            lock (Gate)
            {
                IGeneratedComponentRegistration? result = null;
                foreach (IGeneratedComponentRegistration registration in Registrations)
                {
                    if (registration.ComponentType != componentType)
                    {
                        continue;
                    }

                    if (result is not null)
                    {
                        ThrowHelper.ThrowGeneratedComponentRegistrationConflict(componentType);
                    }

                    result = registration;
                }

                return result ?? ThrowHelper.ThrowGeneratedComponentRegistrationMissing(componentType);
            }
        }
    }

    /// <summary>Provides component registrations emitted by DeltaECS.Generators.</summary>
    public static class GeneratedComponentCatalog
    {
        /// <summary>Gets a snapshot of the generated component registrations in deterministic type-name order.</summary>
        public static IGeneratedComponentRegistration[] GetRegistrations()
        {
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

            return registrations;
        }

        /// <summary>Gets the generated registration for a component type.</summary>
        public static IGeneratedComponentRegistration GetRegistration<T>()
            => GeneratedComponentRegistrationRegistry.GetRegistration(typeof(T));

    }
}
