using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Delta.ECS.Unity
{
    /// <summary>
    /// Stores entities authored for a Delta ECS scene. The Unity editor integration
    /// creates a hidden scene object for it automatically; it does not own a Delta ECS World.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Delta ECS/Scene Authoring")]
    public sealed class SceneAuthoring : MonoBehaviour
    {
        internal const string AuthoringObjectName = "Delta ECS Scene Authoring";

        [SerializeField, HideInInspector]
        private SceneData _sceneData = new();
        [SerializeField, HideInInspector]
        private List<SceneViewBinding> _viewBindings = new();

        [NonSerialized] private Dictionary<string, GameObject> _viewsByStableId;
        [NonSerialized] private Dictionary<UnityEngine.EntityId, string> _stableIdsByView;

        internal SceneData SceneData => _sceneData ??= new SceneData();

        internal bool TryGetView(string stableId, out GameObject view)
        {
            EnsureBindingIndex();
            if (!string.IsNullOrEmpty(stableId) && _viewsByStableId.TryGetValue(stableId, out view) && view != null)
            {
                return true;
            }

            view = null;
            return false;
        }

        internal bool TryGetStableId(GameObject view, out string stableId)
        {
            EnsureBindingIndex();
            if (view != null && _stableIdsByView.TryGetValue(view.GetEntityId(), out stableId))
            {
                return true;
            }

            stableId = null;
            return false;
        }

        /// <summary>Persists a same-scene Unity view binding for an authored ECS entity.</summary>
        internal bool SetView(string stableId, GameObject view)
        {
            if (string.IsNullOrEmpty(stableId)
                || (view != null && !SceneData.TryGetEntity(stableId, out _))
                || (view != null && view.scene != gameObject.scene))
            {
                return false;
            }

            EnsureBindingIndex();
            UnityEngine.EntityId viewId = view != null ? view.GetEntityId() : UnityEngine.EntityId.None;
            for (int i = _viewBindings.Count - 1; i >= 0; i--)
            {
                SceneViewBinding binding = _viewBindings[i];
                bool sameEntity = string.Equals(binding.StableId, stableId, StringComparison.Ordinal);
                bool sameView = view != null && binding.View != null && binding.View.GetEntityId() == viewId;
                if (sameEntity || sameView)
                {
                    _viewBindings.RemoveAt(i);
                }
            }

            if (view != null)
            {
                _viewBindings.Add(new SceneViewBinding(stableId, view));
            }

            InvalidateBindingIndex();
            EnsureBindingIndex();
            return true;
        }

        internal bool RemoveView(string stableId) => SetView(stableId, null);

        internal static SceneAuthoring FindForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            SceneAuthoring[] authorings = Resources.FindObjectsOfTypeAll<SceneAuthoring>();
            for (int i = 0; i < authorings.Length; i++)
            {
                SceneAuthoring authoring = authorings[i];
                if (authoring != null && authoring.gameObject.scene == scene)
                {
                    return authoring;
                }
            }

            return null;
        }

        internal static SceneAuthoring GetOrCreateForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                UnityThrowHelpers.ThrowArgument("Scene authoring requires a valid, loaded scene.", nameof(scene));
            }

            SceneAuthoring existing = FindForScene(scene);
            if (existing != null)
            {
                return existing;
            }

            var authoringObject = new GameObject(AuthoringObjectName);
            SceneManager.MoveGameObjectToScene(authoringObject, scene);
            SceneAuthoring authoring = authoringObject.AddComponent<SceneAuthoring>();
            authoringObject.hideFlags |= HideFlags.HideInHierarchy;
            return authoring;
        }

        private void OnValidate()
        {
            SceneData.EnsureStableIds();
            InvalidateBindingIndex();
        }

        private void OnEnable()
        {
            SceneData.Changed -= OnSceneDataChanged;
            SceneData.Changed += OnSceneDataChanged;
            InvalidateBindingIndex();
        }

        private void OnDisable()
        {
            if (_sceneData != null)
            {
                _sceneData.Changed -= OnSceneDataChanged;
            }
        }

        private void OnSceneDataChanged(SceneDataChange change)
        {
            if (change.Kind == SceneDataChangeKind.EntityDestroyed)
            {
                RemoveView(change.StableId);
            }
        }

        private void EnsureBindingIndex()
        {
            if (_viewsByStableId != null && _stableIdsByView != null)
            {
                return;
            }

            _viewsByStableId = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            _stableIdsByView = new Dictionary<UnityEngine.EntityId, string>();
            _viewBindings ??= new List<SceneViewBinding>();
            for (int i = 0; i < _viewBindings.Count; i++)
            {
                SceneViewBinding binding = _viewBindings[i];
                if (string.IsNullOrEmpty(binding.StableId) || binding.View == null)
                {
                    continue;
                }

                UnityEngine.EntityId viewId = binding.View.GetEntityId();
                _viewsByStableId[binding.StableId] = binding.View;
                _stableIdsByView[viewId] = binding.StableId;
            }
        }

        private void InvalidateBindingIndex()
        {
            _viewsByStableId = null;
            _stableIdsByView = null;
        }
    }

    [Serializable]
    internal struct SceneViewBinding
    {
        [SerializeField] private string _stableId;
        [SerializeField] private GameObject _view;

        public string StableId => _stableId;
        public GameObject View => _view;

        public SceneViewBinding(string stableId, GameObject view)
        {
            _stableId = stableId;
            _view = view;
        }
    }
}
