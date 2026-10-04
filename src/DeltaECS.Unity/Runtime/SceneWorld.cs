using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Delta.ECS;
using Delta.ECS.Integration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Delta.ECS.Unity
{
    /// <summary>
    /// Binds serialized scene authoring data to a DeltaECS world. This is deliberately
    /// a plain C# object: it does not add a world GameObject to the scene.
    /// </summary>
    public sealed class SceneWorld : IDisposable
    {
        private const int DefaultEntityCapacity = 256;
        private const int RuntimeEntityCapacity = 4096;
        private static readonly List<SceneWorld> LiveWorlds = new();

        private readonly Dictionary<string, Entity> _entitiesByStableId = new(StringComparer.Ordinal);
        private readonly Dictionary<Entity, string> _stableIdsByEntity = new();
        private readonly Dictionary<Entity, GameObject> _viewsByEntity = new();
        private readonly Dictionary<UnityEngine.EntityId, Entity> _entitiesByView = new();
        private readonly World _world;
        private readonly IEcsWorld _integration;
        private readonly SceneData _sceneData;
        private readonly SceneAuthoring _authoring;
        private readonly bool _ownsWorld;
        private bool _disposed;

        public World World => _world;
        internal IEcsWorld Integration => _integration;
        internal SceneData SceneData => _sceneData;
        internal bool IsBound => !_disposed;

        /// <summary>Returns the integration-owned runtime context for a loaded Unity scene.</summary>
        public static SceneWorld GetForScene(Scene scene) => SceneWorldRuntime.GetForScene(scene);

        internal static bool TryFindLiveWorld(SceneData sceneData, out SceneWorld sceneWorld)
        {
            for (int i = LiveWorlds.Count - 1; i >= 0; i--)
            {
                SceneWorld candidate = LiveWorlds[i];
                if (!candidate._disposed && ReferenceEquals(candidate._sceneData, sceneData))
                {
                    sceneWorld = candidate;
                    return true;
                }
            }

            sceneWorld = null;
            return false;
        }

        internal static bool TryFindLiveWorld(GameObject view, out SceneWorld sceneWorld, out Entity entity)
        {
            for (int i = LiveWorlds.Count - 1; i >= 0; i--)
            {
                SceneWorld candidate = LiveWorlds[i];
                if (!candidate._disposed && candidate.TryGetEntity(view, out entity))
                {
                    sceneWorld = candidate;
                    return true;
                }
            }

            sceneWorld = null;
            entity = default;
            return false;
        }

        /// <summary>Resolves the live ECS world and entity currently associated with a Unity view.</summary>
        public static bool TryGetBoundEntity(GameObject view, [NotNullWhen(true)] out World world, out Entity entity)
        {
            if (view != null && TryFindLiveWorld(view, out SceneWorld sceneWorld, out entity))
            {
                world = sceneWorld.World;
                return true;
            }

            world = null;
            entity = default;
            return false;
        }

        public Entity[] AuthoredEntities
        {
            get
            {
                var entities = new Entity[_entitiesByStableId.Count];
                _entitiesByStableId.Values.CopyTo(entities, 0);
                return entities;
            }
        }

        internal SceneWorld(World world, SceneData sceneData)
            : this(world, sceneData, null, ownsWorld: false, registerLiveWorld: true)
        {
        }

        private SceneWorld(World world, SceneData sceneData,
            SceneAuthoring authoring, bool ownsWorld, bool registerLiveWorld)
        {
            _world = world ?? UnityThrowHelpers.ThrowArgumentNull<World>(nameof(world));
            _sceneData = sceneData ?? UnityThrowHelpers.ThrowArgumentNull<SceneData>(nameof(sceneData));
            _authoring = authoring;
            _integration = world;
            _ownsWorld = ownsWorld;

            _sceneData.EnsureStableIds();
            _integration.Initialize();
            RebuildAuthoringEntities();
            if (registerLiveWorld)
            {
                LiveWorlds.Add(this);
            }
        }

        internal static SceneWorld CreatePreview(SceneData sceneData)
        {
            var world = new World(CreateComponentLayouts(), DefaultEntityCapacity);
            try
            {
                return new SceneWorld(world, sceneData, null,
                    ownsWorld: true, registerLiveWorld: false);
            }
            catch (Exception exception)
            {
                world.Dispose();
                return UnityThrowHelpers.Rethrow<SceneWorld>(exception);
            }
        }

        internal static SceneWorld CreateAuthoringPreview(SceneAuthoring authoring)
        {
            if (authoring == null)
            {
                UnityThrowHelpers.ThrowArgumentNull(nameof(authoring));
            }

            var world = new World(CreateComponentLayouts(), DefaultEntityCapacity);
            try
            {
                return new SceneWorld(world, authoring.SceneData, authoring,
                    ownsWorld: true, registerLiveWorld: false);
            }
            catch (Exception exception)
            {
                world.Dispose();
                return UnityThrowHelpers.Rethrow<SceneWorld>(exception);
            }
        }

        internal static SceneWorld CreateRuntime(SceneAuthoring authoring)
        {
            if (authoring == null)
            {
                UnityThrowHelpers.ThrowArgumentNull(nameof(authoring));
            }

            var world = new World(CreateComponentLayouts(), RuntimeEntityCapacity);
            try
            {
                return new SceneWorld(world, authoring.SceneData, authoring,
                    ownsWorld: true, registerLiveWorld: true);
            }
            catch (Exception exception)
            {
                world.Dispose();
                return UnityThrowHelpers.Rethrow<SceneWorld>(exception);
            }
        }

        private static ComponentLayoutRegistry CreateComponentLayouts()
        {
            var layouts = new ComponentLayoutRegistry();
            foreach (IGeneratedComponentRegistration registration in GeneratedComponentCatalog.GetRegistrations())
            {
                layouts.Register(registration);
            }

            return layouts;
        }

        internal void RebuildAuthoringEntities()
        {
            ThrowIfDisposed();
            DestroyCurrentAuthoringEntities();
            _entitiesByStableId.Clear();
            _stableIdsByEntity.Clear();

            _sceneData.EnsureStableIds();
            IList<EntityAuthoring> authorings = _sceneData.Entities;
            for (int i = 0; i < authorings.Count; i++)
            {
                EntityAuthoring authoring = authorings[i];
                if (authoring == null)
                {
                    continue;
                }

                var componentIds = new List<ComponentId>(authoring.Components.Count);
                var seenSchemas = new HashSet<ulong>();
                for (int componentIndex = 0; componentIndex < authoring.Components.Count; componentIndex++)
                {
                    ComponentData data = authoring.Components[componentIndex];
                    if (TryGetDescriptor(data, out ComponentDescriptor descriptor)
                        && seenSchemas.Add(descriptor.Schema.Value))
                    {
                        componentIds.Add(descriptor.Id);
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown, duplicate, or invalid component schema '{data?.SchemaIdText}' on '{authoring.Name}'.");
                    }
                }

                Entity entity = _integration.Create(componentIds.ToArray());
                _entitiesByStableId.Add(authoring.StableId, entity);
                _stableIdsByEntity.Add(entity, authoring.StableId);
            }

            // Create every entity before loading values so stable-ID references can resolve.
            for (int i = 0; i < authorings.Count; i++)
            {
                EntityAuthoring authoring = authorings[i];
                if (authoring == null || !_entitiesByStableId.TryGetValue(authoring.StableId, out Entity entity))
                {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < authoring.Components.Count; componentIndex++)
                {
                    ComponentData data = authoring.Components[componentIndex];
                    if (!TryGetDescriptor(data, out ComponentDescriptor descriptor))
                    {
                        continue;
                    }

                    if (descriptor.IsTag)
                    {
                        continue;
                    }

                    object value = data.CreateValue(descriptor.ValueType, ResolveEntity);
                    if (_integration.TryRead(entity, descriptor.Id, out ComponentSnapshot snapshot, out _)
                        && !_integration.TryWrite(entity, descriptor.Id, value, snapshot.Stamp, out _, out EcsWriteError error))
                    {
                        Debug.LogError($"Failed to load {descriptor.Name} on '{authoring.Name}': {error.Code}.");
                    }
                }
            }
        }

        internal bool TryGetEntity(EntityAuthoring authoring, out Entity entity)
        {
            if (authoring != null)
            {
                return _entitiesByStableId.TryGetValue(authoring.StableId, out entity);
            }

            entity = default;
            return false;
        }

        internal Entity ResolveEntity(string stableId)
        {
            return !string.IsNullOrEmpty(stableId)
                && _entitiesByStableId.TryGetValue(stableId, out Entity entity)
                    ? entity
                    : default;
        }

        internal bool TryGetStableId(Entity entity, out string stableId) =>
            _stableIdsByEntity.TryGetValue(entity, out stableId);

        /// <summary>Resolves the runtime view first, then the scene-authored stable-ID binding.</summary>
        internal bool TryGetView(Entity entity, out GameObject view)
        {
            if (_viewsByEntity.TryGetValue(entity, out view) && view != null)
            {
                return true;
            }

            if (_viewsByEntity.ContainsKey(entity))
            {
                _viewsByEntity.Remove(entity);
            }

            return _authoring != null
                && TryGetStableId(entity, out string stableId)
                && _authoring.TryGetView(stableId, out view);
        }

        /// <summary>Resolves a view through the runtime registry or the scene-authored stable-ID binding.</summary>
        internal bool TryGetEntity(GameObject view, out Entity entity)
        {
            if (view != null && _entitiesByView.TryGetValue(view.GetEntityId(), out entity)
                && _integration.IsAlive(entity))
            {
                return true;
            }

            if (view != null && _authoring != null
                && _authoring.TryGetStableId(view, out string stableId)
                && _entitiesByStableId.TryGetValue(stableId, out entity)
                && _integration.IsAlive(entity))
            {
                return true;
            }

            entity = default;
            return false;
        }

        /// <summary>Registers a transient runtime association; it is not written into scene data.</summary>
        public bool BindView(Entity entity, GameObject view)
        {
            if (view == null || !_integration.IsAlive(entity))
            {
                return false;
            }

            UnityEngine.EntityId viewId = view.GetEntityId();
            if (_entitiesByView.TryGetValue(viewId, out Entity previousEntity))
            {
                _viewsByEntity.Remove(previousEntity);
            }

            if (_viewsByEntity.TryGetValue(entity, out GameObject previousView) && previousView != null)
            {
                _entitiesByView.Remove(previousView.GetEntityId());
            }

            _viewsByEntity[entity] = view;
            _entitiesByView[viewId] = entity;
            return true;
        }

        public void UnbindView(Entity entity)
        {
            if (_viewsByEntity.Remove(entity, out GameObject view) && view != null)
            {
                _entitiesByView.Remove(view.GetEntityId());
            }
        }

        internal bool TryGetDescriptor(ComponentData data, out ComponentDescriptor descriptor)
        {
            descriptor = default;
            if (data == null || !data.TryGetSchemaId(out SchemaId schema))
            {
                return false;
            }

            ReadOnlyMemory<ComponentDescriptor> catalog = _integration.Catalog.Components;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog.Span[i].Schema == schema)
                {
                    descriptor = catalog.Span[i];
                    return true;
                }
            }

            return false;
        }

        internal bool TryRead(EntityAuthoring authoring, ComponentDescriptor descriptor,
            out ComponentSnapshot snapshot, out EcsReadError error)
        {
            if (TryGetEntity(authoring, out Entity entity))
            {
                return TryRead(entity, descriptor, out snapshot, out error);
            }

            snapshot = default;
            error = default;
            return false;
        }

        internal bool TryRead(Entity entity, ComponentDescriptor descriptor,
            out ComponentSnapshot snapshot, out EcsReadError error)
            => _integration.TryRead(entity, descriptor.Id, out snapshot, out error);

        internal bool TryGetComponents(Entity entity, Span<ComponentId> destination, out int totalCount)
        {
            totalCount = 0;
            if (!_integration.IsAlive(entity))
            {
                return false;
            }

            ReadOnlyMemory<ComponentDescriptor> components = _integration.Catalog.Components;
            for (int index = 0; index < components.Length; index++)
            {
                ComponentId component = components.Span[index].Id;
                if (!_world.Has(entity, component))
                {
                    continue;
                }

                if (totalCount < destination.Length)
                {
                    destination[totalCount] = component;
                }

                totalCount++;
            }

            return true;
        }

        internal bool TryWrite(EntityAuthoring authoring, ComponentData data,
            ComponentDescriptor descriptor, object value, out EcsWriteError error)
        {
            if (!TryGetEntity(authoring, out Entity entity) || !_integration.IsAlive(entity))
            {
                error = new EcsWriteError(EcsWriteErrorCode.EntityNotAlive);
                return false;
            }

            if (!_integration.TryRead(entity, descriptor.Id, out ComponentSnapshot snapshot, out _))
            {
                error = new EcsWriteError(EcsWriteErrorCode.ComponentMissing);
                return false;
            }

            if (!_integration.TryWrite(entity, descriptor.Id, value, snapshot.Stamp, out _, out error))
            {
                return false;
            }

            if (descriptor.IsTag)
            {
                data.StoreDefaultTag();
            }
            else
            {
                data.StoreValue(value, GetStableIdOrEmpty);
            }

            return true;
        }

        internal bool AddComponent(EntityAuthoring authoring, ComponentDescriptor descriptor,
            ComponentData data, out EcsWriteError error, out string failureReason)
        {
            failureReason = null;
            if (!TryGetEntity(authoring, out Entity entity))
            {
                error = new EcsWriteError(EcsWriteErrorCode.EntityNotAlive);
                failureReason = "The authored entity is not bound to the preview world. Rebuild the preview and try again.";
                return false;
            }

            if (!_integration.IsAlive(entity))
            {
                error = new EcsWriteError(EcsWriteErrorCode.EntityNotAlive);
                failureReason = "The entity is no longer alive in the preview world.";
                return false;
            }

            if (!_integration.Add(entity, new[] { descriptor.Id }))
            {
                error = default;
                failureReason = _world.Has(entity, descriptor.Id)
                    ? "The component is already present in the preview world."
                    : "DeltaECS rejected the structural add without reporting a write error.";
                return false;
            }

            if (descriptor.IsTag)
            {
                data.StoreDefaultTag();
                error = default;
                return true;
            }

            object value = data.CreateValue(descriptor.ValueType, ResolveEntity);
            if (!_integration.TryRead(entity, descriptor.Id, out ComponentSnapshot snapshot, out EcsReadError readError))
            {
                _integration.Remove(entity, new[] { descriptor.Id });
                error = new EcsWriteError(readError.Code switch
                {
                    EcsReadErrorCode.EntityNotAlive => EcsWriteErrorCode.EntityNotAlive,
                    EcsReadErrorCode.ComponentUnknown => EcsWriteErrorCode.ComponentUnknown,
                    EcsReadErrorCode.ComponentMissing => EcsWriteErrorCode.ComponentMissing,
                    EcsReadErrorCode.Unsupported => EcsWriteErrorCode.Unsupported,
                    _ => EcsWriteErrorCode.None
                });
                failureReason = $"The new component could not be read from the preview world ({readError.Code}).";
                return false;
            }

            if (!_integration.TryWrite(entity, descriptor.Id, value, snapshot.Stamp, out _, out error))
            {
                _integration.Remove(entity, new[] { descriptor.Id });
                failureReason = $"The default component value could not be written ({error.Code}).";
                return false;
            }

            data.StoreValue(value, GetStableIdOrEmpty);
            return true;
        }

        internal bool RemoveComponent(EntityAuthoring authoring, ComponentDescriptor descriptor,
            out EcsWriteError error)
        {
            error = default;
            return TryGetEntity(authoring, out Entity entity)
                && _integration.Remove(entity, new[] { descriptor.Id });
        }

        public bool TryFindFirstEntity<T>(out Entity entity) where T : struct
        {
            if (_world.Layouts.TryGetPrimary(typeof(T), out ComponentId componentId))
            {
                foreach (Entity candidate in _entitiesByStableId.Values)
                {
                    if (_integration.IsAlive(candidate) && _world.Has(candidate, componentId))
                    {
                        entity = candidate;
                        return true;
                    }
                }
            }

            entity = default;
            return false;
        }

        public bool HasComponent<T>(Entity entity) where T : struct =>
            _world.IsAlive(entity)
            && _world.Layouts.TryGetPrimary(typeof(T), out ComponentId componentId)
            && _world.Has(entity, componentId);

        internal bool TryGetAuthoring(string stableId, out EntityAuthoring authoring)
        {
            IList<EntityAuthoring> entities = _sceneData.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityAuthoring candidate = entities[i];
                if (candidate != null && string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    authoring = candidate;
                    return true;
                }
            }

            authoring = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LiveWorlds.Remove(this);
            DestroyCurrentAuthoringEntities();
            _entitiesByStableId.Clear();
            _stableIdsByEntity.Clear();
            _viewsByEntity.Clear();
            _entitiesByView.Clear();
            try
            {
                _integration.Shutdown();
            }
            catch (InvalidOperationException)
            {
                // The ECS integration may already have been shut down during domain teardown.
            }

            if (_ownsWorld)
            {
                _world.Dispose();
            }
        }

        private void DestroyCurrentAuthoringEntities()
        {
            foreach (Entity entity in _entitiesByStableId.Values)
            {
                if (_integration.IsAlive(entity))
                {
                    _integration.Destroy(entity);
                }
            }
        }

        private string GetStableIdOrEmpty(Entity entity) =>
            _stableIdsByEntity.TryGetValue(entity, out string stableId) ? stableId : string.Empty;

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                UnityThrowHelpers.ThrowObjectDisposed(nameof(SceneWorld));
            }
        }
    }

    /// <summary>Owns runtime world creation and teardown for loaded Unity scenes.</summary>
    internal static class SceneWorldRuntime
    {
        private static readonly Dictionary<Scene, SceneWorld> WorldsByScene = new();
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            Shutdown();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInitialized();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                EnsureForScene(SceneManager.GetSceneAt(i));
            }
        }

        internal static SceneWorld GetForScene(Scene scene)
        {
            if (!Application.isPlaying)
            {
                UnityThrowHelpers.ThrowInvalidOperation("Runtime scene worlds are only available in Play Mode.");
            }

            EnsureInitialized();
            return EnsureForScene(scene);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.quitting += Shutdown;
        }

        private static SceneWorld EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                UnityThrowHelpers.ThrowArgument("A runtime ECS world requires a valid, loaded scene.", nameof(scene));
            }

            if (WorldsByScene.TryGetValue(scene, out SceneWorld existing)
                && existing != null && existing.IsBound)
            {
                return existing;
            }

            SceneAuthoring authoring = SceneAuthoring.GetOrCreateForScene(scene);
            SceneWorld sceneWorld = SceneWorld.CreateRuntime(authoring);
            WorldsByScene[scene] = sceneWorld;
            return sceneWorld;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (WorldsByScene.Remove(scene, out SceneWorld sceneWorld))
            {
                sceneWorld.Dispose();
            }
        }

        private static void Shutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= Shutdown;
            _initialized = false;

            foreach (SceneWorld sceneWorld in WorldsByScene.Values)
            {
                sceneWorld.Dispose();
            }

            WorldsByScene.Clear();
        }
    }
}
