using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Delta.ECS.Unity.Editor
{
    [InitializeOnLoad]
    internal static class EditorLifecycle
    {
        private static bool _sceneAuthoringSetupScheduled;

        static EditorLifecycle()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            ScheduleSceneAuthoringSetup();
        }

        private static void OnUndoRedo()
        {
            HierarchyNodeHandler.RefreshAll();
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            ScheduleSceneAuthoringSetup();
        }

        private static void OnSceneClosed(Scene scene)
        {
            HierarchyNodeHandler.RefreshAll();
        }

        private static void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            ScheduleSceneAuthoringSetup();
        }

        private static void ScheduleSceneAuthoringSetup()
        {
            if (_sceneAuthoringSetupScheduled)
            {
                return;
            }

            _sceneAuthoringSetupScheduled = true;
            EditorApplication.delayCall += EnsureAuthoringForLoadedScenes;
        }

        private static void EnsureAuthoringForLoadedScenes()
        {
            _sceneAuthoringSetupScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                EnsureSceneAuthoring(SceneManager.GetSceneAt(i));
            }

            HierarchyNodeHandler.RefreshAll();
        }

        private static void EnsureSceneAuthoring(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            {
                return;
            }

            SceneAuthoring existing = SceneAuthoring.FindForScene(scene);
            if (existing != null && IsDedicatedAuthoringObject(existing.gameObject))
            {
                GameObject existingObject = existing.gameObject;
                bool changed = false;
                if (!string.Equals(existingObject.name, SceneAuthoring.AuthoringObjectName, System.StringComparison.Ordinal))
                {
                    existingObject.name = SceneAuthoring.AuthoringObjectName;
                    changed = true;
                }

                if ((existingObject.hideFlags & HideFlags.HideInHierarchy) == 0)
                {
                    existingObject.hideFlags |= HideFlags.HideInHierarchy;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(existingObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                return;
            }

            var authoringObject = new GameObject(SceneAuthoring.AuthoringObjectName);
            SceneManager.MoveGameObjectToScene(authoringObject, scene);
            SceneAuthoring replacement = authoringObject.AddComponent<SceneAuthoring>();

            if (existing != null)
            {
                EditorUtility.CopySerialized(existing, replacement);
                replacement.SceneData.EnsureStableIds();
                UnityEngine.Object.DestroyImmediate(existing);
            }

            authoringObject.hideFlags |= HideFlags.HideInHierarchy;

            if (existing != null)
            {
                EditorUtility.SetDirty(replacement);
                EditorUtility.SetDirty(authoringObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static bool IsDedicatedAuthoringObject(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            if (components.Length != 2)
            {
                return false;
            }

            bool hasAuthoring = false;
            bool hasTransform = false;
            for (int i = 0; i < components.Length; i++)
            {
                hasAuthoring |= components[i] is SceneAuthoring;
                hasTransform |= components[i] is Transform;
            }

            return hasAuthoring && hasTransform;
        }
    }
}
