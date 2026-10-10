#nullable enable

using System.Collections.Generic;
using Delta.ECS;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Delta.ECS.Unity
{
    /// <summary>Creates and binds prefab views for entities carrying an unbound <see cref="ViewToken"/>.</summary>
    public sealed class ViewBindingSystem
    {
        private readonly SceneWorld _sceneWorld;
        private readonly World _world;
        private readonly ViewPrefabCatalog _catalog;
        private readonly ComponentId _viewBoundId;
        private readonly EcsOperation<ForEachEntityOperationInvoker> _bindOperation;
        private readonly List<Entity> _entitiesToMark = new();

        /// <summary>Creates a view binder for one scene world and prefab catalog.</summary>
        public ViewBindingSystem(SceneWorld sceneWorld, ViewPrefabCatalog catalog)
        {
            _sceneWorld = sceneWorld ?? UnityThrowHelpers.ThrowArgumentNull<SceneWorld>(nameof(sceneWorld));
            _catalog = catalog ?? UnityThrowHelpers.ThrowArgumentNull<ViewPrefabCatalog>(nameof(catalog));
            _world = _sceneWorld.World;

            ComponentId viewTokenId = _world.Layouts.GetPrimary<ViewToken>();
            _viewBoundId = _world.Layouts.GetPrimary<ViewBound>();
            Query query = _world.WhereAll(stackalloc ComponentId[1] { viewTokenId })
                .WhereNone(stackalloc ComponentId[1] { _viewBoundId });
            _bindOperation = _world.ForEachEntity(in query, BindView);
        }

        /// <summary>Binds views for currently unbound entities with a matching token.</summary>
        public void BindPendingViews()
        {
            _entitiesToMark.Clear();
            _bindOperation.Invoke();

            for (int i = 0; i < _entitiesToMark.Count; i++)
            {
                _world.Add(_entitiesToMark[i], _viewBoundId);
            }

            _entitiesToMark.Clear();
        }

        private void BindView(EntityRef entityRef)
        {
            Entity entity = entityRef;
            if (_sceneWorld.TryGetView(entity, out _))
            {
                _entitiesToMark.Add(entity);
                return;
            }

            ViewToken token = entityRef.Get<ViewToken>();
            if (!_catalog.TryGetPrefab(token.Id, out GameObject? prefab))
            {
                return;
            }

            Object instance = Object.Instantiate((Object)prefab);
            GameObject? view = instance as GameObject;
            if (view == null)
            {
                string instanceType = instance == null ? "null" : instance.GetType().ToString();
                Debug.LogError(
                    $"View prefab '{token.Id}' ({prefab.GetType()}) instantiated as '{instanceType}'.",
                    prefab);
                if (instance != null)
                {
                    Object.Destroy(instance);
                }

                return;
            }

            Scene scene = _sceneWorld.UnityScene;
            if (scene.IsValid() && scene.isLoaded && view.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(view, scene);
            }

            if (!_sceneWorld.BindView(entity, view))
            {
                Object.Destroy(view);
                return;
            }

            _entitiesToMark.Add(entity);
        }
    }
}
