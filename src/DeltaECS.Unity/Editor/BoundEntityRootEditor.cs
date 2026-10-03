using System;
using System.Collections.Generic;
using Delta.ECS;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    internal static class BoundEntityRootEditorSelector
    {
        private static bool _creatingDefaultGameObjectEditor;

        [RootEditor(supportsAddComponent: true)]
        private static Type SelectRootEditor(UnityEngine.Object[] objects)
        {
            if (_creatingDefaultGameObjectEditor)
            {
                return null;
            }

            if (objects == null || objects.Length != 1 || objects[0] is not GameObject gameObject)
            {
                return null;
            }

            if (SceneWorld.TryFindLiveWorld(gameObject, out _, out _))
            {
                return typeof(BoundEntityRootEditor);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            SceneAuthoring authoring = SceneAuthoring.FindForScene(gameObject.scene);
            return authoring != null && authoring.TryGetStableId(gameObject, out _)
                ? typeof(BoundEntityRootEditor)
                : null;
        }

        internal static UnityEditor.Editor CreateDefaultGameObjectEditor(GameObject gameObject)
        {
            _creatingDefaultGameObjectEditor = true;
            try
            {
                return UnityEditor.Editor.CreateEditor(gameObject);
            }
            finally
            {
                _creatingDefaultGameObjectEditor = false;
            }
        }
    }

    internal sealed class BoundEntityRootEditor : UnityEditor.Editor
    {
        private const string ComponentHeadersStyleResourcePath =
            "Inspector/BoundEntityRootEditor";

        private readonly List<UnityEditor.Editor> _componentEditors = new();
        private readonly List<Component> _componentTargets = new();
        private UnityEditor.Editor _gameObjectEditor;
        private SceneEntityInspectorView _view;
        private VisualElement _componentInspectorRoot;

        private void OnEnable()
        {
            if (target is GameObject gameObject)
            {
                _gameObjectEditor = BoundEntityRootEditorSelector.CreateDefaultGameObjectEditor(gameObject);
            }

            _view = new SceneEntityInspectorView(ResolveTarget, embedded: true);
            EditorApplication.hierarchyChanged += RefreshComponentInspectors;
            Undo.undoRedoPerformed += RefreshComponentInspectors;
        }

        private void OnDisable()
        {
            _view?.Dispose();
            _view = null;
            if (_gameObjectEditor != null)
            {
                DestroyImmediate(_gameObjectEditor);
                _gameObjectEditor = null;
            }

            EditorApplication.hierarchyChanged -= RefreshComponentInspectors;
            Undo.undoRedoPerformed -= RefreshComponentInspectors;
            _componentInspectorRoot = null;
            DisposeComponentEditors();
        }

        protected override void OnHeaderGUI()
        {
            // The root editor's generic object header is replaced by the native
            // GameObjectInspector header embedded at the top of CreateInspectorGUI.
        }

        private void DisposeComponentEditors()
        {
            foreach (UnityEditor.Editor componentEditor in _componentEditors)
            {
                if (componentEditor != null)
                {
                    DestroyImmediate(componentEditor);
                }
            }

            _componentEditors.Clear();
            _componentTargets.Clear();
        }

        public override bool UseDefaultMargins() => false;

        public override bool RequiresConstantRepaint()
        {
            if (EditorApplication.isPlaying)
                return true;

            foreach (UnityEditor.Editor componentEditor in _componentEditors)
            {
                if (componentEditor != null && componentEditor.RequiresConstantRepaint())
                    return true;
            }

            return false;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            StyleSheet componentStyles = Resources.Load<StyleSheet>(ComponentHeadersStyleResourcePath);
            if (componentStyles != null)
            {
                root.styleSheets.Add(componentStyles);
            }

            if (_gameObjectEditor != null)
            {
                root.Add(new IMGUIContainer(() =>
                {
                    if (_gameObjectEditor != null)
                    {
                        _gameObjectEditor.DrawHeader();
                    }
                }));
            }

            VisualElement ecsContent = _view.CreateGUI();
            bool ecsExpanded = SessionState.GetBool("DeltaECS.EntityInspector.Expanded", true);
            ecsContent.style.display = ecsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            root.Add(new IMGUIContainer(() =>
            {
                GUIStyle titlebar = GUI.skin.GetStyle("IN Title");
                GUIStyle foldoutStyle = GUI.skin.GetStyle("Titlebar Foldout");
                Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, titlebar,
                    GUILayout.ExpandWidth(true));
                bool expanded = GUI.Toggle(headerRect, ecsExpanded, GUIContent.none, GUIStyle.none);
                if (Event.current.type == EventType.Repaint)
                {
                    titlebar.Draw(headerRect, GUIContent.none, false, false, false, false);
                    Rect arrowRect = new Rect(headerRect.x + foldoutStyle.margin.left + 1f,
                        headerRect.y + (headerRect.height - 13) / 2 + titlebar.padding.top, 13, 13);
                    foldoutStyle.Draw(arrowRect, false, false, expanded, false);
                }
                Rect textRect = new Rect(headerRect.x + 28, headerRect.y,
                    headerRect.width - 28, headerRect.height);
                GUI.Label(textRect, "Delta ECS Entity", GUI.skin.GetStyle("IN TitleText"));
                if (expanded == ecsExpanded) return;
                ecsExpanded = expanded;
                SessionState.SetBool("DeltaECS.EntityInspector.Expanded", expanded);
                ecsContent.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }));
            root.Add(ecsContent);
            _componentInspectorRoot = new VisualElement();
            root.Add(_componentInspectorRoot);
            RebuildComponentInspectors();
            return root;
        }

        private void RefreshComponentInspectors()
        {
            if (_componentInspectorRoot != null && !HaveSameComponents())
            {
                RebuildComponentInspectors();
            }
        }

        private bool HaveSameComponents()
        {
            if (target is not GameObject gameObject || gameObject == null)
            {
                return _componentTargets.Count == 0;
            }

            int index = 0;
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!ShouldDrawComponent(component))
                {
                    continue;
                }

                if (index >= _componentTargets.Count || _componentTargets[index] != component)
                {
                    return false;
                }

                index++;
            }

            return index == _componentTargets.Count;
        }

        private void RebuildComponentInspectors()
        {
            DisposeComponentEditors();
            _componentInspectorRoot.Clear();
            if (target is not GameObject gameObject || gameObject == null)
            {
                return;
            }

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (!ShouldDrawComponent(component))
                {
                    continue;
                }

                UnityEditor.Editor componentEditor = CreateEditor(component);
                if (componentEditor == null)
                {
                    continue;
                }

                _componentEditors.Add(componentEditor);
                _componentTargets.Add(component);
                var inspector = new InspectorElement(componentEditor);
                string expandedKey = $"DeltaECS.ComponentInspector.{component.GetEntityId()}.Expanded";
                bool expanded = SessionState.GetBool(expandedKey, true);
                inspector.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                _componentInspectorRoot.Add(new IMGUIContainer(() =>
                {
                    if (component == null) return;
                    bool next = EditorGUILayout.InspectorTitlebar(expanded, component);
                    if (next == expanded) return;
                    expanded = next;
                    SessionState.SetBool(expandedKey, next);
                    inspector.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                }));
                _componentInspectorRoot.Add(inspector);
            }
        }

        private static bool ShouldDrawComponent(Component component) => component != null
            && (component.hideFlags & HideFlags.HideInInspector) == 0;

        private EntityInspectorTarget ResolveTarget()
        {
            if (target is not GameObject gameObject || gameObject == null)
            {
                return default;
            }

            if (SceneWorld.TryFindLiveWorld(gameObject, out SceneWorld world, out Entity entity))
            {
                SceneAuthoring liveAuthoring = SceneAuthoring.FindForScene(gameObject.scene);
                return new EntityInspectorTarget(liveAuthoring, null, world, entity);
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SceneAuthoring authoring = SceneAuthoring.FindForScene(gameObject.scene);
                if (authoring != null && authoring.TryGetStableId(gameObject, out string stableId))
                {
                    return new EntityInspectorTarget(authoring, stableId);
                }
            }

            return default;
        }
    }
}
