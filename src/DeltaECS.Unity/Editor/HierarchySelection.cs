using System;
using Delta.ECS;
using Unity.Hierarchy;
using UnityEditor;

namespace Delta.ECS.Unity.Editor
{
    internal static class HierarchySelection
    {
        internal static EntityInspectorTarget Current { get; private set; }
        internal static SceneAuthoring Authoring => Current.Authoring;
        internal static string StableId => Current.StableId;
        internal static SceneWorld RuntimeWorld => Current.RuntimeWorld;
        internal static Entity RuntimeEntity => Current.RuntimeEntity;
        internal static event Action Changed;

        private static HierarchyNodeHandler _handler;
        private static HierarchyNode _node;
        private static HierarchyView _view;
        private static bool _selectionCheckScheduled;

        internal static void SetFromHierarchy(
            HierarchyNodeHandler handler,
            HierarchyView view,
            SceneAuthoring authoring,
            string stableId,
            SceneWorld runtimeWorld,
            Entity runtimeEntity,
            HierarchyNode node)
        {
            bool changed = Current.Authoring != authoring
                || !string.Equals(Current.StableId, stableId, StringComparison.Ordinal)
                || Current.RuntimeWorld != runtimeWorld
                || Current.RuntimeEntity != runtimeEntity;

            Current = new EntityInspectorTarget(authoring, stableId, runtimeWorld, runtimeEntity);
            _handler = handler;
            _node = node;
            _view = view;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        internal static void UpdateSelectedTarget(
            HierarchyNodeHandler handler,
            HierarchyNode node,
            SceneAuthoring authoring,
            string stableId,
            SceneWorld runtimeWorld,
            Entity runtimeEntity)
        {
            if (_handler != handler || _node != node)
            {
                return;
            }

            bool changed = Current.Authoring != authoring
                || !string.Equals(Current.StableId, stableId, StringComparison.Ordinal)
                || Current.RuntimeWorld != runtimeWorld
                || Current.RuntimeEntity != runtimeEntity;
            Current = new EntityInspectorTarget(authoring, stableId, runtimeWorld, runtimeEntity);
            SelectionProxyBridge.UpdateTarget(handler, node, Current);
            if (changed)
            {
                Changed?.Invoke();
            }
        }

        internal static void Clear(HierarchyView view)
        {
            if (_view == view)
            {
                SetEmpty();
            }
        }

        internal static void DeferClear(HierarchyView view)
        {
            if (_view == view)
            {
                ScheduleSelectionValidation();
            }
        }

        internal static void ClearHandler(HierarchyNodeHandler handler)
        {
            if (_handler == handler)
            {
                SetEmpty();
            }
        }

        internal static void ClearIfSelected(
            HierarchyNodeHandler handler,
            SceneAuthoring authoring,
            string stableId)
        {
            if (_handler == handler && Authoring == authoring
                && string.Equals(StableId, stableId, StringComparison.Ordinal))
            {
                SetEmpty();
            }
        }

        internal static void ClearIfSelected(SceneWorld runtimeWorld, Entity runtimeEntity)
        {
            if (Current.RuntimeWorld == runtimeWorld && Current.RuntimeEntity == runtimeEntity)
            {
                SetEmpty();
            }
        }

        internal static void RenameSelected(SceneAuthoring authoring, string stableId)
        {
            if (Authoring == authoring && string.Equals(StableId, stableId, StringComparison.Ordinal))
            {
                Changed?.Invoke();
            }
        }

        private static void SetEmpty()
        {
            if (Authoring == null && string.IsNullOrEmpty(StableId))
            {
                _handler = null;
                _node = default;
                _view = null;
                return;
            }

            Current = default;
            _handler = null;
            _node = default;
            _view = null;
            Changed?.Invoke();
        }

        private static void ScheduleSelectionValidation()
        {
            if (_selectionCheckScheduled)
            {
                return;
            }

            _selectionCheckScheduled = true;
            EditorApplication.delayCall += ValidateHierarchySelection;
        }

        private static void ValidateHierarchySelection()
        {
            _selectionCheckScheduled = false;
            if (_view == null)
            {
                return;
            }

            foreach (ref readonly HierarchyNode selectedNode in _view.ViewModel.EnumerateNodesWithFlags(HierarchyNodeFlags.Selected))
            {
                if (selectedNode == _node)
                {
                    return;
                }
            }

            SetEmpty();
        }
    }
}
