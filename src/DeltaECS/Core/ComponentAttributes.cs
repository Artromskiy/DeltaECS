using System.ComponentModel;

namespace Delta.ECS
{
    /// <summary>Marks a component for generated registration and optionally assigns a stable schema ID.</summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ComponentSchemaAttribute : Attribute
    {

        /// <summary>An explicitly assigned stable schema ID, or zero when the ID is name-derived.</summary>
        public ulong SchemaId { get; }
        /// <summary>Marks a component whose schema ID is derived from its qualified name.</summary>
        public ComponentSchemaAttribute() { }

        /// <summary>Marks a component with an explicitly assigned stable schema ID.</summary>
        public ComponentSchemaAttribute(ulong schemaId)
        {
            SchemaId = schemaId;
        }
    }

    /// <summary>Assigns a catalog-resolvable name to a generated component registration.</summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ComponentAliasAttribute : Attribute
    {

        /// <summary>Gets the assigned alias.</summary>
        public string Alias { get; }
        /// <summary>Assigns one alias to a component.</summary>
        public ComponentAliasAttribute(string alias)
        {
            Alias = alias;
        }
    }

    /// <summary>Assigns a component to enum groups without a generic attribute type.</summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class ComponentGroupAttribute : Attribute
    {

        /// <summary>Gets the enum values that identify the component groups.</summary>
        public object[] GroupIds { get; }
        /// <summary>Assigns a component to enum values or arrays of enum values.</summary>
        public ComponentGroupAttribute(params object[] groupIds)
        {
            GroupIds = groupIds;
        }
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
        ComponentId Register(ComponentRegistry layouts);
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

        /// <summary>Copies generated registrations for one-time layout initialization.</summary>
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
