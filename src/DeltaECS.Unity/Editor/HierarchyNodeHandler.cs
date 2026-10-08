using System;
using System.Collections.Generic;
using Delta.ECS;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    [InitializeOnLoad]
    internal sealed class HierarchyNodeHandler : HierarchyNodeTypeHandler
    {
        private static readonly ComponentId[] NoRequiredComponents = Array.Empty<ComponentId>();
        private const double RuntimeHierarchyRefreshInterval = 0.5d;
        private static double _nextRuntimeHierarchyRefresh;
        private static bool _wasPlaying;

        private readonly Dictionary<EntityKey, EntityNode> _entityNodes = new();
        private readonly HashSet<EntityKey> _activeEntityKeys = new();
        private readonly List<EntityKey> _removedEntityKeys = new();
        private readonly Dictionary<HierarchyNode, NodeReference> _nodes = new();
        private readonly HashSet<HierarchyView> _boundViews = new();
        private readonly Dictionary<HierarchyView, BlockedDrag> _blockedDrags = new();
        private readonly Dictionary<SceneAuthoring, Action<SceneDataChange>> _sceneDataSubscriptions = new();
        private static readonly HashSet<HierarchyNodeHandler> Instances = new();

        static HierarchyNodeHandler()
        {
            HierarchyWindow.BindView += BindHierarchyView;
            EditorApplication.delayCall += BindExistingHierarchyViews;
            EditorApplication.update += UpdateRuntimeHierarchy;
        }

        private static void UpdateRuntimeHierarchy()
        {
            if (EditorApplication.isPlaying)
            {
                _wasPlaying = true;
                if (EditorApplication.timeSinceStartup < _nextRuntimeHierarchyRefresh)
                {
                    return;
                }

                _nextRuntimeHierarchyRefresh = EditorApplication.timeSinceStartup + RuntimeHierarchyRefreshInterval;
                RefreshAll();
                return;
            }

            if (_wasPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _wasPlaying = false;
                RefreshAll();
            }
        }

        private static void BindHierarchyView(HierarchyWindow window, HierarchyView view)
        {
            if (view == null)
            {
                return;
            }

            var handler =
                view.Source.GetOrCreateNodeTypeHandler<HierarchyNodeHandler>();
            handler.BindToView(view);
        }

        private static void BindExistingHierarchyViews()
        {
            var windows = Resources.FindObjectsOfTypeAll<HierarchyWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window != null)
                {
                    BindHierarchyView(window, window.View);
                }
            }
        }

        public override string GetNodeTypeName() => nameof(HierarchyNodeHandler);

        public override EntityId GetEntityIdFromNode(in HierarchyNode node) =>
            _nodes.TryGetValue(node, out var reference) && reference.IsEntity
                ? SelectionProxyBridge.GetEntityId(this, node,
                    new EntityInspectorTarget(reference.Authoring, reference.StableId, reference.RuntimeWorld, reference.RuntimeEntity))
                : EntityId.None;

        public override HierarchyNode GetNodeFromEntityId(EntityId entityId) =>
            SelectionProxyBridge.GetNode(this, entityId);

        public override int GetEntityIdsFromNodes(ReadOnlySpan<HierarchyNode> nodes, Span<EntityId> outEntityIds)
        {
            int remaining = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (outEntityIds[i] == EntityId.None)
                    outEntityIds[i] = GetEntityIdFromNode(in nodes[i]);
                if (outEntityIds[i] == EntityId.None) remaining++;
            }
            return remaining;
        }

        public override int GetNodesFromEntityIds(ReadOnlySpan<EntityId> entityIds, Span<HierarchyNode> outNodes)
        {
            int remaining = 0;
            for (int i = 0; i < entityIds.Length; i++)
            {
                if (outNodes[i] == HierarchyNode.Null)
                    outNodes[i] = GetNodeFromEntityId(entityIds[i]);
                if (outNodes[i] == HierarchyNode.Null) remaining++;
            }
            return remaining;
        }

        public override HierarchyNodeFlags GetDefaultNodeFlags(in HierarchyNode node, HierarchyNodeFlags defaultFlags = HierarchyNodeFlags.None) =>
            defaultFlags | HierarchyNodeFlags.Expanded;

        protected override void Initialize()
        {
            Instances.Add(this);
            Reconcile();
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var node in _nodes.Keys)
                SelectionProxyBridge.Remove(this, node);
            Instances.Remove(this);
            HierarchySelection.ClearHandler(this);
            foreach (var pair in _sceneDataSubscriptions)
            {
                if (pair.Key != null)
                {
                    pair.Key.SceneData.Changed -= pair.Value;
                }
            }
            _sceneDataSubscriptions.Clear();
            _entityNodes.Clear();
            _activeEntityKeys.Clear();
            _removedEntityKeys.Clear();
            _nodes.Clear();
            foreach (var view in _boundViews)
            {
                UnbindDragGuard(view);
                view.FlagsChanged -= OnHierarchyFlagsChanged;
                view.PopulateContextMenu -= PopulateContextMenu;
                HierarchySelection.Clear(view);
            }
            _boundViews.Clear();

            base.Dispose(disposing);
        }

        protected override void OnBindView(HierarchyView view)
        {
            if (!_boundViews.Add(view))
            {
                return;
            }

            BindDragGuard(view);
            view.FlagsChanged += OnHierarchyFlagsChanged;
            view.PopulateContextMenu += PopulateContextMenu;
        }

        protected override void OnBindItem(HierarchyViewItem item)
        {
            item.Name.RegisterValueChangedCallback(OnEntityNameChanged);
        }

        protected override void OnUnbindItem(HierarchyViewItem item)
        {
            item.Name.UnregisterValueChangedCallback(OnEntityNameChanged);
        }

        private void BindToView(HierarchyView view) => OnBindView(view);

        private void OnEntityNameChanged(ChangeEvent<string> evt)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || string.Equals(evt.previousValue, evt.newValue))
            {
                return;
            }

            var element = evt.currentTarget as VisualElement;
            while (element != null && element is not HierarchyViewItem)
            {
                element = element.parent;
            }

            if (element is not HierarchyViewItem item
                || !_nodes.TryGetValue(item.Node, out var reference)
                || !reference.IsEntity
                || !reference.Authoring.SceneData.TryGetEntity(reference.StableId, out var entity)
                || string.Equals(entity.Name, evt.newValue))
            {
                return;
            }

            Undo.RecordObject(reference.Authoring, "Rename Delta ECS Entity");
            if (reference.Authoring.SceneData.RenameEntity(reference.StableId, evt.newValue))
            {
                HierarchyMenu.MarkSceneDirty(reference.Authoring);
            }
        }

        protected override void OnUnbindView(HierarchyView view)
        {
            UnbindDragGuard(view);
            view.FlagsChanged -= OnHierarchyFlagsChanged;
            view.PopulateContextMenu -= PopulateContextMenu;
            _boundViews.Remove(view);
            HierarchySelection.Clear(view);
        }

        private void BindDragGuard(HierarchyView view)
        {
            view.RegisterCallback<PointerDownEvent>(OnHierarchyPointerDown, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerMoveEvent>(OnHierarchyPointerMove, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerUpEvent>(OnHierarchyPointerUp, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerCancelEvent>(OnHierarchyPointerCancel, TrickleDown.TrickleDown);
        }

        private void UnbindDragGuard(HierarchyView view)
        {
            view.UnregisterCallback<PointerDownEvent>(OnHierarchyPointerDown, TrickleDown.TrickleDown);
            view.UnregisterCallback<PointerMoveEvent>(OnHierarchyPointerMove, TrickleDown.TrickleDown);
            view.UnregisterCallback<PointerUpEvent>(OnHierarchyPointerUp, TrickleDown.TrickleDown);
            view.UnregisterCallback<PointerCancelEvent>(OnHierarchyPointerCancel, TrickleDown.TrickleDown);
            _blockedDrags.Remove(view);
        }

        private void OnHierarchyPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not HierarchyView view)
            {
                return;
            }

            var clickedEntity = PointerDownOnEntity(evt.target as VisualElement);
            var extendsSelection = evt.ctrlKey || evt.commandKey || evt.shiftKey;
            if (clickedEntity || (extendsSelection && HasSelectedEntity(view)))
            {
                var position = evt.position;
                _blockedDrags[view] = new BlockedDrag(evt.pointerId, new Vector2(position.x, position.y));
            }
            else
            {
                _blockedDrags.Remove(view);
            }
        }

        private bool HasSelectedEntity(HierarchyView view)
        {
            foreach (ref readonly var node in view.ViewModel.EnumerateNodesWithFlags(HierarchyNodeFlags.Selected))
            {
                if (_nodes.TryGetValue(node, out var reference) && reference.IsEntity)
                {
                    return true;
                }
            }

            return false;
        }

        private bool PointerDownOnEntity(VisualElement target)
        {
            for (var element = target; element != null; element = element.parent)
            {
                if (element is HierarchyViewItem item && _nodes.ContainsKey(item.Node))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnHierarchyPointerMove(PointerMoveEvent evt)
        {
            if (evt.currentTarget is not HierarchyView view
                || !_blockedDrags.TryGetValue(view, out var blockedDrag)
                || evt.pointerId != blockedDrag.PointerId)
            {
                return;
            }

            var position = evt.position;
            var pointerPosition = new Vector2(position.x, position.y);
            if (!blockedDrag.ThresholdReached && (pointerPosition - blockedDrag.StartPosition).sqrMagnitude < 16f)
            {
                return;
            }

            blockedDrag.ThresholdReached = true;
            _blockedDrags[view] = blockedDrag;
            evt.StopImmediatePropagation();
        }

        private void OnHierarchyPointerUp(PointerUpEvent evt)
        {
            ClearBlockedDrag(evt.currentTarget as HierarchyView, evt.pointerId);
        }

        private void OnHierarchyPointerCancel(PointerCancelEvent evt)
        {
            ClearBlockedDrag(evt.currentTarget as HierarchyView, evt.pointerId);
        }

        private void ClearBlockedDrag(HierarchyView view, int pointerId)
        {
            if (view != null && _blockedDrags.TryGetValue(view, out var blockedDrag)
                && blockedDrag.PointerId == pointerId)
            {
                _blockedDrags.Remove(view);
            }
        }

        private struct BlockedDrag
        {
            public readonly int PointerId;
            public readonly Vector2 StartPosition;
            public bool ThresholdReached;

            public BlockedDrag(int pointerId, Vector2 startPosition)
            {
                PointerId = pointerId;
                StartPosition = startPosition;
                ThresholdReached = false;
            }
        }

        internal static void RefreshAll()
        {
            var changed = false;
            foreach (var instance in Instances)
            {
                changed |= instance.Reconcile();
            }

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        internal static SceneAuthoring FindAuthoringForScene(Scene scene)
            => SceneAuthoring.FindForScene(scene);

        private static bool IsHierarchyScene(Scene scene) =>
            scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);

        private bool Reconcile()
        {
            if (!Hierarchy.IsCreated)
            {
                return false;
            }

            var changed = false;
            _activeEntityKeys.Clear();

            var sceneHandler = Hierarchy.GetNodeTypeHandler<HierarchySceneHandler>();
            var authoringsInScenes = Resources.FindObjectsOfTypeAll<SceneAuthoring>();
            SynchronizeSceneSubscriptions(authoringsInScenes);
            for (var i = 0; i < authoringsInScenes.Length; i++)
            {
                var authoringHost = authoringsInScenes[i];
                if (authoringHost == null || !IsHierarchyScene(authoringHost.gameObject.scene))
                {
                    continue;
                }

                var sceneNode = sceneHandler.GetOrCreateNode(authoringHost.gameObject.scene);
                if (sceneNode == HierarchyNode.Null)
                {
                    continue;
                }

                SceneWorld runtimeWorld = null;
                if (EditorApplication.isPlaying)
                {
                    SceneWorld.TryFindLiveWorld(authoringHost.SceneData, out runtimeWorld);
                }

                var entities = authoringHost.SceneData.Entities;
                for (var entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                {
                    var authoring = entities[entityIndex];
                    if (authoring == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(authoring.StableId))
                    {
                        continue;
                    }

                    Entity runtimeEntity = runtimeWorld != null
                        ? runtimeWorld.ResolveEntity(authoring.StableId)
                        : default;
                    bool hasView = authoringHost.TryGetView(authoring.StableId, out _)
                        || (runtimeWorld != null
                            && runtimeWorld.World.IsAlive(runtimeEntity)
                            && runtimeWorld.TryGetView(runtimeEntity, out _));
                    if (hasView)
                    {
                        continue;
                    }

                    var key = new EntityKey(authoringHost.GetEntityId(), authoring.StableId);
                    _activeEntityKeys.Add(key);
                    var reference = runtimeWorld != null && runtimeWorld.World.IsAlive(runtimeEntity)
                        ? new NodeReference(authoringHost, authoring.StableId, runtimeWorld, runtimeEntity)
                        : new NodeReference(authoringHost, authoring.StableId);
                    if (_entityNodes.TryGetValue(key, out var entityNode))
                    {
                        if (entityNode.Reference.RuntimeWorld != reference.RuntimeWorld
                            || entityNode.Reference.RuntimeEntity != reference.RuntimeEntity)
                        {
                            changed = true;
                        }

                        entityNode.Reference = reference;
                        if (!string.Equals(entityNode.Name, authoring.Name))
                        {
                            CommandList.SetName(entityNode.Node, authoring.Name);
                            entityNode.Name = authoring.Name;
                            changed = true;
                        }

                        _entityNodes[key] = entityNode;
                        _nodes[entityNode.Node] = reference;
                        HierarchySelection.UpdateSelectedTarget(
                            this,
                            entityNode.Node,
                            authoringHost,
                            authoring.StableId,
                            reference.RuntimeWorld,
                            reference.RuntimeEntity);
                        continue;
                    }

                    CommandList.Add(sceneNode, out var newNode);
                    CommandList.SetName(newNode, authoring.Name);
                    var newReference = reference;
                    _entityNodes.Add(key, new EntityNode(newNode, newReference, authoring.Name));
                    _nodes.Add(newNode, newReference);
                    HierarchySelection.UpdateSelectedTarget(
                        this,
                        newNode,
                        authoringHost,
                        authoring.StableId,
                        newReference.RuntimeWorld,
                        newReference.RuntimeEntity);
                    changed = true;
                }

                if (runtimeWorld != null)
                {
                    changed |= SynchronizeRuntimeEntities(authoringHost, sceneNode, runtimeWorld);
                }
            }

            _removedEntityKeys.Clear();
            foreach (var pair in _entityNodes)
            {
                if (!_activeEntityKeys.Contains(pair.Key))
                {
                    _removedEntityKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < _removedEntityKeys.Count; i++)
            {
                var key = _removedEntityKeys[i];
                var entityNode = _entityNodes[key];
                if (entityNode.Reference.IsRuntimeOnly)
                {
                    HierarchySelection.ClearIfSelected(
                        entityNode.Reference.RuntimeWorld,
                        entityNode.Reference.RuntimeEntity);
                }
                else
                {
                    HierarchySelection.ClearIfSelected(this, entityNode.Reference.Authoring, entityNode.Reference.StableId);
                }
                if (Hierarchy.Exists(entityNode.Node))
                {
                    CommandList.Remove(entityNode.Node);
                }

                SelectionProxyBridge.Remove(this, entityNode.Node);
                _nodes.Remove(entityNode.Node);
                _entityNodes.Remove(key);
                changed = true;
            }

            if (CommandList.Size > 0)
            {
                CommandList.Execute();
            }

            if (changed)
            {
                Hierarchy.Update();
            }

            return changed;
        }

        private bool SynchronizeRuntimeEntities(
            SceneAuthoring authoring,
            HierarchyNode sceneNode,
            SceneWorld runtimeWorld)
        {
            Query query = runtimeWorld.World.WhereAll(NoRequiredComponents);
            bool changed = false;
            runtimeWorld.World.ForEachEntity(in query, entity =>
            {
                if (runtimeWorld.TryGetStableId(entity, out _) || runtimeWorld.TryGetView(entity, out _))
                {
                    return;
                }

                var key = new EntityKey(runtimeWorld, entity);
                _activeEntityKeys.Add(key);
                string name = $"ECS Entity {entity.Index}:{entity.Generation}";
                var reference = new NodeReference(authoring, null, runtimeWorld, entity);
                if (_entityNodes.TryGetValue(key, out EntityNode entityNode))
                {
                    if (entityNode.Reference.RuntimeWorld != reference.RuntimeWorld
                        || entityNode.Reference.RuntimeEntity != reference.RuntimeEntity)
                    {
                        entityNode.Reference = reference;
                        changed = true;
                    }

                    if (!string.Equals(entityNode.Name, name, StringComparison.Ordinal))
                    {
                        CommandList.SetName(entityNode.Node, name);
                        entityNode.Name = name;
                        changed = true;
                    }

                    _entityNodes[key] = entityNode;
                    _nodes[entityNode.Node] = reference;
                    return;
                }

                CommandList.Add(sceneNode, out HierarchyNode node);
                CommandList.SetName(node, name);
                _entityNodes.Add(key, new EntityNode(node, reference, name));
                _nodes.Add(node, reference);
                changed = true;
            }).Invoke();

            return changed;
        }

        private void SynchronizeSceneSubscriptions(SceneAuthoring[] authorings)
        {
            var loadedAuthorings = new HashSet<SceneAuthoring>();
            for (var i = 0; i < authorings.Length; i++)
            {
                var authoring = authorings[i];
                if (authoring == null || !IsHierarchyScene(authoring.gameObject.scene))
                {
                    continue;
                }

                loadedAuthorings.Add(authoring);
                if (!_sceneDataSubscriptions.ContainsKey(authoring))
                {
                    var capturedAuthoring = authoring;
                    Action<SceneDataChange> callback = change => OnSceneDataChanged(capturedAuthoring, change);
                    _sceneDataSubscriptions.Add(authoring, callback);
                    authoring.SceneData.Changed += callback;
                }
            }

            if (_sceneDataSubscriptions.Count == loadedAuthorings.Count)
            {
                return;
            }

            var removed = new List<SceneAuthoring>();
            foreach (var authoring in _sceneDataSubscriptions.Keys)
            {
                if (!loadedAuthorings.Contains(authoring))
                {
                    removed.Add(authoring);
                }
            }

            for (var i = 0; i < removed.Count; i++)
            {
                var authoring = removed[i];
                if (authoring != null)
                {
                    authoring.SceneData.Changed -= _sceneDataSubscriptions[authoring];
                }
                _sceneDataSubscriptions.Remove(authoring);
            }
        }

        private void OnSceneDataChanged(SceneAuthoring authoring, SceneDataChange change)
        {
            var changed = change.Kind switch
            {
                SceneDataChangeKind.EntityCreated => authoring.SceneData.TryGetEntity(change.StableId, out var entity) &&
                                                             AddEntity(authoring, entity),
                SceneDataChangeKind.EntityDestroyed => RemoveEntity(authoring, change.StableId),
                SceneDataChangeKind.EntityRenamed => RenameEntity(authoring, change.StableId),
                _ => false
            };

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private bool AddEntity(SceneAuthoring authoring, EntityAuthoring entity)
        {
            if (!Hierarchy.IsCreated || authoring == null || !IsHierarchyScene(authoring.gameObject.scene)
                || string.IsNullOrEmpty(entity.StableId))
            {
                return false;
            }

            var key = new EntityKey(authoring.GetEntityId(), entity.StableId);
            if (_entityNodes.ContainsKey(key))
            {
                return RenameEntity(authoring, entity.StableId);
            }

            var sceneHandler = Hierarchy.GetNodeTypeHandler<HierarchySceneHandler>();
            var sceneNode = sceneHandler.GetOrCreateNode(authoring.gameObject.scene);
            if (sceneNode == HierarchyNode.Null)
            {
                return false;
            }

            CommandList.Add(sceneNode, out var newNode);
            CommandList.SetName(newNode, entity.Name);
            CommandList.Execute();

            var reference = new NodeReference(authoring, entity.StableId);
            _entityNodes.Add(key, new EntityNode(newNode, reference, entity.Name));
            _nodes.Add(newNode, reference);
            Hierarchy.Update();
            return true;
        }

        private bool RemoveEntity(SceneAuthoring authoring, string stableId)
        {
            if (authoring == null || string.IsNullOrEmpty(stableId))
            {
                return false;
            }

            var key = new EntityKey(authoring.GetEntityId(), stableId);
            if (!_entityNodes.TryGetValue(key, out var entityNode))
            {
                return false;
            }

            HierarchySelection.ClearIfSelected(this, authoring, stableId);
            if (Hierarchy.IsCreated && Hierarchy.Exists(entityNode.Node))
            {
                CommandList.Remove(entityNode.Node);
                CommandList.Execute();
            }

            SelectionProxyBridge.Remove(this, entityNode.Node);
            _nodes.Remove(entityNode.Node);
            _entityNodes.Remove(key);
            if (Hierarchy.IsCreated)
            {
                Hierarchy.Update();
            }

            return true;
        }

        private bool RenameEntity(SceneAuthoring authoring, string stableId)
        {
            if (authoring == null || string.IsNullOrEmpty(stableId))
            {
                return false;
            }

            var key = new EntityKey(authoring.GetEntityId(), stableId);
            if (!_entityNodes.TryGetValue(key, out var entityNode)
                || !authoring.SceneData.TryGetEntity(stableId, out var entity)
                || string.Equals(entityNode.Name, entity.Name))
            {
                return false;
            }

            CommandList.SetName(entityNode.Node, entity.Name);
            CommandList.Execute();
            entityNode.Name = entity.Name;
            _entityNodes[key] = entityNode;
            HierarchySelection.RenameSelected(authoring, stableId);
            Hierarchy.Update();
            return true;
        }

        internal static void SelectEntity(HierarchyView view, SceneAuthoring authoring, string stableId)
        {
            var key = new EntityKey(authoring.GetEntityId(), stableId);
            foreach (var instance in Instances)
            {
                if (instance._entityNodes.ContainsKey(key))
                {
                    if (view != null)
                    {
                        if (instance._boundViews.Contains(view))
                        {
                            TrySelectEntity(instance, view, key);
                        }
                    }
                    else
                    {
                        foreach (var boundView in instance._boundViews)
                        {
                            if (TrySelectEntity(instance, boundView, key))
                            {
                                break;
                            }
                        }
                    }

                    return;
                }
            }
        }

        private static bool TrySelectEntity(
            HierarchyNodeHandler handler,
            HierarchyView view,
            EntityKey key)
        {
            if (view == null || !handler._boundViews.Contains(view)
                || !handler._entityNodes.TryGetValue(key, out var entityNode)
                || !handler.Hierarchy.Exists(entityNode.Node))
            {
                return false;
            }

            if (view.UpdateNeeded)
            {
                view.Update();
            }

            var viewModel = view.ViewModel;
            if (!viewModel.Contains(in entityNode.Node))
            {
                return false;
            }

            var node = entityNode.Node;
            view.SetSelection(in node);
            return true;
        }

        private void PopulateContextMenu(HierarchyView view, HierarchyViewItem item, DropdownMenu menu)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (item != null && _nodes.TryGetValue(item.Node, out var node))
            {
                if (node.IsEntity)
                {
                    var entityNode = item.Node;
                    menu.AppendAction("Select ECS Entity", _ => view.SetSelection(in entityNode),
                        _ => DropdownMenuAction.Status.Normal);
                    menu.AppendAction("Delete ECS Entity", _ => HierarchyMenu.DeleteEntity(node.Authoring, node.StableId),
                        _ => DropdownMenuAction.Status.Normal);
                }

                menu.AppendAction("Create ECS Entity", _ => HierarchyMenu.CreateEntity(node.Authoring, view),
                    _ => DropdownMenuAction.Status.Normal);
                return;
            }

            var scene = item != null
                        && view.Source.GetNodeTypeHandler(item.Node) is HierarchySceneHandler sceneHandler
                    ? sceneHandler.GetScene(item.Node)
                    : SceneManager.GetActiveScene();
            var authoringHost = FindAuthoringForScene(scene);
            if (authoringHost != null)
            {
                menu.AppendAction("Create Delta ECS Entity", _ => HierarchyMenu.CreateEntity(authoringHost, view),
                    _ => DropdownMenuAction.Status.Normal);
            }
        }

        private void OnHierarchyFlagsChanged(HierarchyView view, HierarchyNodeFlags changedFlags)
        {
            if ((changedFlags & HierarchyNodeFlags.Selected) == 0)
            {
                return;
            }

            NodeReference selectedReference = default;
            var selectedEntityCount = 0;
            foreach (ref readonly var node in view.ViewModel.EnumerateNodesWithFlags(HierarchyNodeFlags.Selected))
            {
                if (!_nodes.TryGetValue(node, out var reference) || !reference.IsEntity)
                {
                    continue;
                }

                selectedEntityCount++;
                if (selectedEntityCount == 1)
                {
                    selectedReference = reference;
                }
            }

            if (selectedEntityCount == 1)
            {
                EntityKey selectedKey = selectedReference.StableId != null
                    ? new EntityKey(selectedReference.Authoring.GetEntityId(), selectedReference.StableId)
                    : new EntityKey(selectedReference.RuntimeWorld, selectedReference.RuntimeEntity);
                if (!_entityNodes.TryGetValue(selectedKey, out EntityNode selectedNode))
                {
                    HierarchySelection.DeferClear(view);
                    return;
                }

                HierarchySelection.SetFromHierarchy(
                    this,
                    view,
                    selectedReference.Authoring,
                    selectedReference.StableId,
                    selectedReference.RuntimeWorld,
                    selectedReference.RuntimeEntity,
                    selectedNode.Node);
                SelectionProxyBridge.Select(this, selectedNode.Node,
                    new EntityInspectorTarget(selectedReference.Authoring, selectedReference.StableId,
                        selectedReference.RuntimeWorld, selectedReference.RuntimeEntity));
            }
            else if (selectedEntityCount > 1)
            {
                HierarchySelection.Clear(view);
            }
            else
            {
                HierarchySelection.DeferClear(view);
            }
        }

        private readonly struct NodeReference
        {
            public readonly SceneAuthoring Authoring;
            public readonly string StableId;
            public readonly SceneWorld RuntimeWorld;
            public readonly Entity RuntimeEntity;
            public bool IsEntity => !string.IsNullOrEmpty(StableId) || RuntimeWorld != null;
            public bool IsRuntimeOnly => string.IsNullOrEmpty(StableId) && RuntimeWorld != null;

            public NodeReference(SceneAuthoring authoring, string stableId)
                : this(authoring, stableId, null, default)
            {
            }

            public NodeReference(
                SceneAuthoring authoring,
                string stableId,
                SceneWorld runtimeWorld,
                Entity runtimeEntity)
            {
                Authoring = authoring;
                StableId = stableId;
                RuntimeWorld = runtimeWorld;
                RuntimeEntity = runtimeEntity;
            }
        }

        private struct EntityNode
        {
            public readonly HierarchyNode Node;
            public NodeReference Reference;
            public string Name;

            public EntityNode(HierarchyNode node, NodeReference reference, string name)
            {
                Node = node;
                Reference = reference;
                Name = name;
            }
        }

        private readonly struct EntityKey : IEquatable<EntityKey>
        {
            private readonly EntityId _authoringId;
            private readonly string _stableId;
            private readonly SceneWorld _runtimeWorld;
            private readonly Entity _runtimeEntity;

            public EntityKey(EntityId authoringId, string stableId)
            {
                _authoringId = authoringId;
                _stableId = stableId;
                _runtimeWorld = null;
                _runtimeEntity = default;
            }

            public EntityKey(SceneWorld runtimeWorld, Entity runtimeEntity)
            {
                _authoringId = default;
                _stableId = null;
                _runtimeWorld = runtimeWorld;
                _runtimeEntity = runtimeEntity;
            }

            public bool Equals(EntityKey other)
            {
                if (_runtimeWorld != null || other._runtimeWorld != null)
                {
                    return ReferenceEquals(_runtimeWorld, other._runtimeWorld)
                        && _runtimeEntity.Equals(other._runtimeEntity);
                }

                return _authoringId.Equals(other._authoringId)
                    && string.Equals(_stableId, other._stableId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is EntityKey other && Equals(other);
            public override int GetHashCode() => _runtimeWorld != null
                ? unchecked((_runtimeWorld.GetHashCode() * 397) ^ _runtimeEntity.GetHashCode())
                : unchecked((_authoringId.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(_stableId ?? string.Empty));
        }
    }

}
