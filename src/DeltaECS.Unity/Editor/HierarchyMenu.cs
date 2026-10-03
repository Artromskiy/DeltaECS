using System;
using Unity.Hierarchy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Delta.ECS.Unity.Editor
{
    internal static class HierarchyMenu
    {
        internal static event Action<SceneAuthoring> AuthoringChanged;

        [MenuItem("GameObject/Delta ECS Entity", false, 10)]
        private static void CreateEntityFromHierarchyMenu(MenuCommand command)
        {
            GameObject contextObject = command.context as GameObject;
            Scene scene = contextObject != null
                ? contextObject.scene
                : SceneManager.GetActiveScene();

            SceneAuthoring authoring = SceneAuthoring.GetOrCreateForScene(scene);
            CreateEntity(authoring);
        }

        [MenuItem("GameObject/Delta ECS Entity", true)]
        private static bool ValidateCreateEntityFromHierarchyMenu() =>
            !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/Delta ECS/Create Entity", false, 31)]
        private static void CreateEntityMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneAuthoring authoring = SceneAuthoring.GetOrCreateForScene(scene);
            CreateEntity(authoring);
        }

        [MenuItem("Tools/Delta ECS/Create Entity", true)]
        private static bool ValidateCreateEntityMenu() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static EntityAuthoring CreateEntity(
            SceneAuthoring authoring,
            HierarchyView view = null)
        {
            if (authoring == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return null;
            }

            Undo.RecordObject(authoring, "Create Delta ECS Entity");
            EntityAuthoring entity = authoring.SceneData.AddEntity();
            MarkSceneDirty(authoring);
            AuthoringChanged?.Invoke(authoring);
            HierarchyNodeHandler.SelectEntity(view, authoring, entity.StableId);
            return entity;
        }

        internal static void DeleteEntity(SceneAuthoring authoring, string stableId)
        {
            if (authoring == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (authoring.SceneData.TryGetEntity(stableId, out _))
            {
                Undo.RecordObject(authoring, "Delete Delta ECS Entity");
                authoring.SceneData.RemoveEntity(stableId);
                MarkSceneDirty(authoring);
                AuthoringChanged?.Invoke(authoring);
            }
        }

        internal static void MarkSceneDirty(SceneAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
        }
    }
}
