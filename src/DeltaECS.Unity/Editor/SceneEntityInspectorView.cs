using System;
using System.Collections.Generic;
using System.Linq;
using Delta;
using Delta.ECS;
using Delta.ECS.Integration;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    internal sealed class SceneEntityInspectorView : IDisposable
    {
        private const string UxmlResourcePath = "Inspector/SceneAuthoringEditor";
        private readonly List<ComponentEntry> _componentEntries = new();
        private readonly List<ComponentEntry> _componentItems = new();
        private readonly List<RuntimeValueBinding> _runtimeBindings = new();
        private readonly Func<EntityInspectorTarget> _getTarget;
        private readonly bool _embedded;
        private EntityInspectorTarget _target;
        private VisualElement _header;
        private VisualElement _footer;
        private VisualElement _root;
        private ListView _componentList;
        private SceneWorld _previewWorld;
        private EntityAuthoring _authoringEntity;
        private ComponentId[] _runtimeComponentBuffer;
        private ComponentId[] _runtimeComponentIds;
        private SceneAuthoring Authoring => _target.Authoring;

        public SceneEntityInspectorView(SelectionProxy proxy)
            : this(() => proxy != null ? proxy.Target : default)
        {
            HierarchySelection.Changed += OnEntitySelectionChanged;
        }

        public SceneEntityInspectorView(Func<EntityInspectorTarget> getTarget, bool embedded = false)
        {
            _getTarget = getTarget;
            _embedded = embedded;
            SyncSelection();
            Undo.undoRedoPerformed += RebuildAfterUndo;
            HierarchyMenu.AuthoringChanged += OnAuthoringChanged;
        }

        public void Dispose()
        {
            HierarchySelection.Changed -= OnEntitySelectionChanged;
            Undo.undoRedoPerformed -= RebuildAfterUndo;
            HierarchyMenu.AuthoringChanged -= OnAuthoringChanged;
            EditorApplication.update -= RefreshRuntimeView;
            _previewWorld?.Dispose();
            _previewWorld = null;
            _runtimeBindings.Clear();
            _runtimeComponentBuffer = null;
            _runtimeComponentIds = null;
        }

        public VisualElement CreateGUI()
        {
            _root = new VisualElement();
            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(UxmlResourcePath);
            if (tree == null)
            {
                _root.Add(new HelpBox($"Could not load Resources/{UxmlResourcePath}.uxml.", HelpBoxMessageType.Error));
                return _root;
            }

            tree.CloneTree(_root);
            if (_embedded)
            {
                _root.AddToClassList("ecs-bound-inspector");
            }
            _header = _root.Q<VisualElement>("inspectorHeader");
            _header.style.fontSize = EditorStyles.label.fontSize;
            _header.style.minHeight = StyleKeyword.Auto;
            _footer = _root.Q<VisualElement>("inspectorFooter");
            _componentList = _root.Q<ListView>("componentList");
            _componentList.SetViewController(new ComponentListController(this));
            _componentList.makeItem = () => new VisualElement();
            _componentList.bindItem = BindComponentItem;
            _componentList.unbindItem = UnbindComponentItem;
            _componentList.itemIndexChanged += OnComponentIndexChanged;
            _componentList.canStartDrag += args => args.id < _componentEntries.Count;
            _root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EditorApplication.update -= RefreshRuntimeView;
                EditorApplication.update += RefreshRuntimeView;
            });
            _root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.update -= RefreshRuntimeView);
            RefreshContent();
            return _root;
        }

        private void OnEntitySelectionChanged()
        {
            SyncSelection();
            RefreshContent();
        }

        internal int ComponentCount => _componentEntries.Count;

        internal void ScheduleRefreshContent() => _root.schedule.Execute(RefreshContent);

        private void OnAuthoringChanged(SceneAuthoring changedAuthoring)
        {
            if (changedAuthoring != Authoring || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RebuildPreviewAndView();
        }

        private bool SyncSelection(bool rebuildPreview = true)
        {
            EntityInspectorTarget next = _getTarget();
            bool authoringChanged = Authoring != next.Authoring;
            bool targetChanged = authoringChanged
                || !string.Equals(_target.StableId, next.StableId, StringComparison.Ordinal)
                || _target.RuntimeWorld != next.RuntimeWorld
                || _target.RuntimeEntity != next.RuntimeEntity;

            _target = next;
            if (targetChanged && !next.IsRuntime)
            {
                _runtimeBindings.Clear();
                _runtimeComponentBuffer = null;
                _runtimeComponentIds = null;
            }

            if (rebuildPreview && (authoringChanged || (next.Authoring != null && _previewWorld == null)))
            {
                RebuildPreviewWorld();
            }

            return targetChanged;
        }

        private void RefreshContent()
        {
            if (_header == null || _footer == null || _componentList == null)
            {
                return;
            }

            _header.Clear();
            _footer.Clear();
            _footer.style.display = _target.IsRuntime ? DisplayStyle.None : DisplayStyle.Flex;
            _componentList.itemsSource = null;
            _componentItems.Clear();
            _componentEntries.Clear();
            _runtimeBindings.Clear();
            _authoringEntity = null;
            if (_target.IsRuntime)
            {
                BuildRuntimeEntity();
                return;
            }

            if (Authoring == null || string.IsNullOrEmpty(_target.StableId))
            {
                _header.Add(new HelpBox("Select a Delta ECS entity in the Hierarchy to inspect its data.", HelpBoxMessageType.Info));
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _header.Add(new HelpBox("Scene-authored ECS data is read-only in Play Mode. Select a runtime entity in the Hierarchy to inspect live data.", HelpBoxMessageType.Info));
                return;
            }

            if (_previewWorld == null)
            {
                _header.Add(new HelpBox("Could not create a Delta ECS preview world. See the Console for details.", HelpBoxMessageType.Error));
                return;
            }

            if (!_previewWorld.TryGetAuthoring(_target.StableId, out EntityAuthoring authoring))
            {
                _header.Add(new HelpBox("The selected ECS entity no longer exists.", HelpBoxMessageType.Warning));
                return;
            }

            BuildAuthoringEntity(authoring);
        }

        private void BuildAuthoringEntity(EntityAuthoring authoring)
        {
            _authoringEntity = authoring;
            _header.Add(BuildEntityHeader(authoring));
            _componentEntries.AddRange(ProjectAuthoringComponents(authoring));
            RenderComponentRows();
            _footer.Add(BuildAddComponentMenu(authoring));
            var deleteButton = new Button(() => HierarchyMenu.DeleteEntity(Authoring, authoring.StableId))
            {
                text = "Delete Entity"
            };
            deleteButton.AddToClassList("ecs-action-button");
            deleteButton.AddToClassList("ecs-delete-entity");
            _footer.Add(deleteButton);
        }

        private VisualElement BuildEntityHeader(EntityAuthoring authoring)
        {
            var card = new VisualElement();
            card.AddToClassList("ecs-entity-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("ecs-entity-title-row");
            titleRow.Add(CreateEntityIcon());

            var nameField = new TextField { value = authoring.Name, isDelayed = true, label = string.Empty };
            nameField.AddToClassList("ecs-entity-name");
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (string.Equals(evt.previousValue, evt.newValue, StringComparison.Ordinal))
                {
                    return;
                }

                Undo.RecordObject(Authoring, "Rename Delta ECS Entity");
                Authoring.SceneData.RenameEntity(authoring.StableId, evt.newValue);
                MarkSceneDirty();
                HierarchySelection.RenameSelected(Authoring, authoring.StableId);
            });
            titleRow.Add(nameField);
            card.Add(titleRow);

            _previewWorld.TryGetEntity(authoring, out Entity entity);
            AddEntityInfo(card, entity, authoring.StableId);
            card.Add(BuildAuthoringViewField(Authoring, authoring.StableId));
            return card;
        }

        private VisualElement BuildAuthoringViewField(SceneAuthoring authoring, string stableId)
        {
            authoring.TryGetView(stableId, out GameObject currentView);
            return BuildViewField(currentView, view =>
            {
                if (view == currentView)
                {
                    return;
                }

                Undo.RecordObject(authoring, "Bind Unity View to Delta ECS Entity");
                if (!authoring.SetView(stableId, view))
                {
                    Debug.LogWarning("A Unity view must be a GameObject in the same scene as its Delta ECS entity.", authoring);
                    RefreshContent();
                    return;
                }

                MarkSceneDirty();
                HierarchyNodeHandler.RefreshAll();
            });
        }

        private VisualElement BuildRuntimeViewField(SceneWorld world, Entity entity)
        {
            world.TryGetView(entity, out GameObject currentView);
            return BuildViewField(currentView, view =>
            {
                if (view == currentView)
                {
                    return;
                }

                if (view == null)
                {
                    world.UnbindView(entity);
                    HierarchyNodeHandler.RefreshAll();
                }
                else if (!world.BindView(entity, view))
                {
                    RefreshContent();
                }
                else
                {
                    HierarchyNodeHandler.RefreshAll();
                }
            });
        }

        private static VisualElement BuildViewField(GameObject currentView, Action<GameObject> onChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("ecs-view-binding");

            var field = new ObjectField("Unity View")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                value = currentView
            };
            AddInspectorFieldClasses(field);
            row.Add(field);

            var unbind = new Button(() => field.value = null)
            {
                text = "Unbind",
                tooltip = "Remove the GameObject binding from this entity"
            };
            unbind.AddToClassList("ecs-action-button");
            unbind.SetEnabled(currentView != null);
            field.RegisterValueChangedCallback(evt =>
            {
                GameObject nextView = evt.newValue as GameObject;
                unbind.SetEnabled(nextView != null);
                onChanged(nextView);
            });
            row.Add(unbind);
            return row;
        }

        private static void AddEntityInfo(VisualElement card, Entity entity, string stableId)
        {
            var idRow = new VisualElement { pickingMode = PickingMode.Ignore };
            idRow.AddToClassList("ecs-entity-id-row");
            idRow.Add(CreateEntityMetadata($"EntityId: {entity.Index}"));
            idRow.Add(CreateEntityMetadata($"Generation: {entity.Generation}"));
            card.Add(idRow);
            card.Add(CreateEntityMetadata($"StableId: {(string.IsNullOrEmpty(stableId) ? "—" : stableId)}"));
        }

        private static Label CreateEntityMetadata(string text)
        {
            var label = CreateClassLabel(text, "ecs-entity-metadata");
            label.pickingMode = PickingMode.Ignore;
            label.style.minHeight = EditorGUIUtility.singleLineHeight;
            return label;
        }

        private VisualElement BuildAuthoringComponentCard(EntityAuthoring authoring, ComponentData data, ComponentDescriptor descriptor)
        {
            return BuildComponentCard(descriptor, body =>
            {
                if (descriptor.IsTag)
                {
                    body.Add(CreateClassLabel("Tag · membership only", "ecs-empty-component"));
                    return;
                }

                if (!_previewWorld.TryRead(authoring, descriptor, out ComponentSnapshot snapshot, out EcsReadError readError))
                {
                    body.Add(new HelpBox($"Could not read component: {readError.Code}.", HelpBoxMessageType.Error));
                    return;
                }

                DrawEditableValue(body, authoring, data, descriptor, snapshot.Value);
            },
            descriptor.IsTag ? null : () => ResetComponent(authoring, data, descriptor),
            () => RemoveComponent(authoring, data, descriptor));
        }

        private VisualElement BuildUnknownComponentCard(EntityAuthoring authoring, ComponentData data)
        {
            ulong? schema = GetSchemaId(data);
            bool expanded = !schema.HasValue || IsComponentExpanded(schema.Value);
            return BuildComponentFoldout("Unregistered component", schema, expanded, body =>
            {
                body.Add(CreateClassLabel($"Schema: {data?.SchemaIdText ?? "<null>"}", "ecs-schema-label"));
            }, header =>
            {
                var actions = CreateComponentMenu();
                actions.menu.AppendAction("Remove Component", _ => RemoveComponent(authoring, data));
                AddHeaderAction(header, actions);
                AddHeaderAction(header, CreateRemoveComponentButton(() => RemoveComponent(authoring, data)));
            });
        }

        private VisualElement BuildAddComponentMenu(EntityAuthoring authoring)
        {
            var button = new Button { text = "Add Component" };
            button.AddToClassList("ecs-action-button");
            button.AddToClassList("ecs-add-component");
            HashSet<ulong> existingSchemas = authoring.Components
                .Select(GetSchemaId)
                .Where(schema => schema.HasValue)
                .Select(schema => schema.Value)
                .ToHashSet();
            ComponentDescriptor[] available = Array.FindAll(
                _previewWorld.Integration.Catalog.Components.ToArray(),
                descriptor => (descriptor.Capabilities & ComponentCapabilities.Write) != 0
                    && !existingSchemas.Contains(descriptor.Schema.Value));
            button.SetEnabled(available.Length != 0);
            button.clicked += () => new ComponentDropdown(available, GetComponentMenuPath,
                    descriptor => AddComponent(authoring, descriptor))
                .Show(button.worldBound);
            return button;
        }

        private VisualElement BuildComponentCard(ComponentDescriptor descriptor, Action<VisualElement> buildBody,
            Action resetComponent = null, Action removeComponent = null)
        {
            bool expanded = IsComponentExpanded(descriptor.Schema.Value);
            return BuildComponentFoldout(GetShortTypeName(descriptor.ValueType), descriptor.Schema.Value, expanded, buildBody,
                header => AddComponentActions(header, descriptor, resetComponent, removeComponent));
        }

        private Foldout BuildComponentFoldout(string title, ulong? schema, bool expanded,
            Action<VisualElement> buildBody, Action<Toggle> buildHeader)
        {
            var foldout = new Foldout { name = "componentFoldout", text = title, value = expanded };
            foldout.AddToClassList("ecs-component-card");

            Toggle header = foldout.Q<Toggle>(className: Foldout.toggleUssClassName);
            header.AddToClassList("ecs-component-card-header");
            header.Q<Label>(className: Toggle.textUssClassName)?.AddToClassList("ecs-component-title");

            var body = new VisualElement { name = "componentBody" };
            body.AddToClassList("ecs-component-body");
            foldout.Add(body);
            buildHeader?.Invoke(header);

            if (expanded)
            {
                buildBody(body);
            }

            foldout.RegisterValueChangedCallback(evt =>
            {
                if (schema.HasValue)
                {
                    SetComponentExpanded(schema.Value, evt.newValue);
                }

                if (evt.newValue)
                {
                    if (body.childCount == 0) buildBody(body);
                }
                else
                {
                    body.Clear();
                    if (schema.HasValue)
                    {
                        _runtimeBindings.RemoveAll(binding => binding.Descriptor.Schema.Value == schema.Value);
                    }
                }
            });
            return foldout;
        }

        private void AddComponentActions(Toggle header, ComponentDescriptor descriptor,
            Action resetComponent, Action removeComponent)
        {
            var actions = CreateComponentMenu();
            actions.menu.AppendAction("Move Up", _ => MoveComponent(descriptor.Schema.Value, -1));
            actions.menu.AppendAction("Move Down", _ => MoveComponent(descriptor.Schema.Value, 1));
            if (resetComponent != null || removeComponent != null)
            {
                actions.menu.AppendSeparator();
                if (resetComponent != null) actions.menu.AppendAction("Reset to Default", _ => resetComponent());
                if (removeComponent != null) actions.menu.AppendAction("Remove Component", _ => removeComponent());
            }

            AddHeaderAction(header, actions);
            if (removeComponent != null)
            {
                AddHeaderAction(header, CreateRemoveComponentButton(removeComponent));
            }
        }

        private static ToolbarMenu CreateComponentMenu()
        {
            var menu = new ToolbarMenu { text = "⋮" };
            menu.AddToClassList("ecs-component-menu");
            menu.Q<VisualElement>(className: ToolbarMenu.arrowUssClassName)?.AddToClassList("ecs-component-menu-arrow");
            menu.RegisterCallback<AttachToPanelEvent>(_ =>
                menu.Q<VisualElement>(className: ToolbarMenu.arrowUssClassName)?.AddToClassList("ecs-component-menu-arrow"));
            menu.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            menu.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            return menu;
        }

        private static Button CreateRemoveComponentButton(Action removeComponent)
        {
            var button = new Button(removeComponent) { text = "×", tooltip = "Remove component" };
            button.AddToClassList("ecs-component-remove");
            button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            button.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            return button;
        }

        private static void AddHeaderAction(Toggle header, VisualElement action)
        {
            header.Add(action);
        }

        private void DrawEditableValue(VisualElement body, EntityAuthoring authoring, ComponentData data,
            ComponentDescriptor descriptor, object value)
        {
            bool canWrite = (descriptor.Capabilities & ComponentCapabilities.Write) != 0
                && descriptor.ValueType.IsValueType;
            int previousChildCount = body.childCount;
            ComponentPropertyBinding[] bindings = ComponentPropertyBagFields.Build(body, descriptor, value,
                canWrite,
                (path, next) =>
                {
                    object updated = value;
                    if (!PropertyBagValueAccess.TrySetValue(ref updated, descriptor.ValueType, path, next))
                    {
                        Debug.LogWarning($"Could not edit a property on {descriptor.Name}: the property path is unavailable.", Authoring);
                        RefreshContent();
                        return;
                    }

                    if (Equals(updated, value)) return;
                    Undo.RecordObject(Authoring, $"Edit {descriptor.Name}");
                    if (_previewWorld.TryWrite(authoring, data, descriptor, updated, out EcsWriteError writeError))
                    {
                        value = updated;
                        MarkSceneDirty();
                    }
                    else
                    {
                        Debug.LogError($"Could not write {descriptor.Name}: {writeError.Code}.", Authoring);
                        RefreshContent();
                    }
                },
                (label, current, onChanged) => CreateEntityReferenceField(label, current, onChanged));

            if (bindings.Length == 0 && body.childCount == previousChildCount)
            {
                body.Add(CreateClassLabel("Component has no exposed properties.", "ecs-empty-component"));
            }
        }

        private VisualElement CreateEntityReferenceField(string label, Entity current, Action<object> onChanged)
        {
            EntityAuthoring[] entities = Authoring.SceneData.Entities.ToArray();
            string[] names = Array.ConvertAll(entities, entity => entity?.Name ?? "<missing entity>");
            string[] stableIds = Array.ConvertAll(entities, entity => entity?.StableId ?? string.Empty);
            HashSet<string> duplicateNames = names
                .GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            _previewWorld.TryGetStableId(current, out string currentId);
            string[] ids = new[] { string.Empty }.Concat(stableIds).ToArray();
            List<string> options = names
                .Select((name, index) => duplicateNames.Contains(name) && entities[index] != null
                    ? $"{name} ({stableIds[index].Substring(0, Math.Min(8, stableIds[index].Length))})"
                    : name)
                .Prepend("None")
                .ToList();
            int selected = Mathf.Max(0, Array.IndexOf(ids, currentId ?? string.Empty));

            var fieldControl = new DropdownField(label, options, selected);
            AddInspectorFieldClasses(fieldControl);
            fieldControl.RegisterValueChangedCallback(evt =>
            {
                int index = options.IndexOf(evt.newValue);
                Entity next = index > 0 && index < ids.Length ? _previewWorld.ResolveEntity(ids[index]) : default;
                onChanged(next);
            });
            return fieldControl;
        }

        private static void AddInspectorFieldClasses(VisualElement field)
        {
            field.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            field.AddToClassList("ecs-value-field");
        }

        private void BuildRuntimeEntity()
        {
            _runtimeBindings.Clear();
            SceneWorld world = _target.RuntimeWorld;
            Entity entity = _target.RuntimeEntity;
            if (world == null || !world.IsBound || !world.Integration.IsAlive(entity))
            {
                _header.Add(new HelpBox("The selected runtime entity is no longer alive.", HelpBoxMessageType.Warning));
                return;
            }

            _header.Add(BuildRuntimeHeader(world, entity));
            ComponentDescriptor[] components = ProjectRuntimeComponents(world, entity);
            if (components == null)
            {
                _header.Add(new HelpBox("Could not read the selected entity's components.", HelpBoxMessageType.Error));
                return;
            }

            foreach (ComponentDescriptor descriptor in components)
            {
                _componentEntries.Add(new ComponentEntry(null, descriptor));
            }

            RenderComponentRows();
        }

        private VisualElement BuildRuntimeComponentCard(SceneWorld world, Entity entity, ComponentDescriptor descriptor)
        {
            return BuildComponentCard(descriptor, body =>
            {
                if (descriptor.IsTag)
                {
                    body.Add(CreateClassLabel("Tag · membership only", "ecs-empty-component"));
                    return;
                }

                if (!world.TryRead(entity, descriptor, out ComponentSnapshot snapshot, out EcsReadError error))
                {
                    body.Add(new HelpBox($"Could not read component: {error.Code}.", HelpBoxMessageType.Error));
                    return;
                }

                DrawRuntimeValue(body, world, entity, descriptor, snapshot);
            });
        }

        private void RenderComponentRows()
        {
            _componentItems.Clear();
            _componentItems.AddRange(_componentEntries);
            _componentList.itemsSource = _componentItems;
            _componentList.Rebuild();
        }

        private void BindComponentItem(VisualElement row, int index)
        {
            row.Clear();
            SetReorderableItemClass(row, "ecs-component-last-bound-row",
                _embedded && _target.IsRuntime && index == _componentEntries.Count - 1);
            ComponentEntry entry = _componentItems[index];
            row.EnableInClassList("ecs-component-row", true);
            row.userData = entry;
            row.Add(_target.IsRuntime
                ? BuildRuntimeComponentCard(_target.RuntimeWorld, _target.RuntimeEntity, entry.Descriptor.Value)
                : entry.Descriptor.HasValue
                    ? BuildAuthoringComponentCard(_authoringEntity, entry.Data, entry.Descriptor.Value)
                    : BuildUnknownComponentCard(_authoringEntity, entry.Data));
        }

        private void SetReorderableItemClass(VisualElement row, string className, bool enabled)
        {
            for (VisualElement ancestor = row.parent; ancestor != null && ancestor != _componentList; ancestor = ancestor.parent)
            {
                if (ancestor.Q<VisualElement>(className: "unity-list-view__reorderable-handle") == null)
                {
                    continue;
                }

                ancestor.EnableInClassList(className, enabled);
                return;
            }
        }

        private void UnbindComponentItem(VisualElement row, int index)
        {
            if (row.userData is ComponentEntry entry && entry.Descriptor.HasValue)
                _runtimeBindings.RemoveAll(binding => binding.Descriptor.Schema == entry.Descriptor.Value.Schema);
            row.userData = null;
            row.Clear();
        }

        private void OnComponentIndexChanged(int from, int to)
        {
            int componentCount = _componentEntries.Count;
            _componentEntries.Clear();
            _componentEntries.AddRange(_componentItems.Take(componentCount));
            PersistVisibleComponentOrder();
        }

        private void PersistVisibleComponentOrder()
        {
            InspectorState state = InspectorState.instance;
            List<ulong> componentOrder = state.GetComponentOrderSnapshot();

            ulong[] visibleOrder = _componentEntries
                .Select(entry => entry.Descriptor.HasValue
                    ? (ulong?)entry.Descriptor.Value.Schema.Value
                    : GetSchemaId(entry.Data))
                .Where(schema => schema.HasValue)
                .Select(schema => schema.Value)
                .Distinct()
                .ToArray();
            HashSet<ulong> visibleSchemas = visibleOrder.ToHashSet();

            IEnumerable<ComponentDescriptor> catalog = _previewWorld != null
                ? _previewWorld.Integration.Catalog.Components.ToArray()
                : _target.RuntimeWorld != null && _target.RuntimeWorld.IsBound
                    ? _target.RuntimeWorld.Integration.Catalog.Components.ToArray()
                    : Array.Empty<ComponentDescriptor>();
            foreach (ComponentDescriptor descriptor in catalog
                .OrderBy(descriptor => GetShortTypeName(descriptor.ValueType), StringComparer.Ordinal))
            {
                if (!componentOrder.Contains(descriptor.Schema.Value))
                {
                    componentOrder.Add(descriptor.Schema.Value);
                }
            }

            foreach (ulong schema in visibleOrder)
            {
                if (!componentOrder.Contains(schema))
                {
                    componentOrder.Add(schema);
                }
            }

            int nextVisible = 0;
            for (int i = 0; i < componentOrder.Count && nextVisible < visibleOrder.Length; i++)
            {
                if (visibleSchemas.Contains(componentOrder[i]))
                {
                    componentOrder[i] = visibleOrder[nextVisible++];
                }
            }

            state.SetComponentOrder(componentOrder);
        }

        private ComponentDescriptor[] ProjectRuntimeComponents(SceneWorld world, Entity entity)
        {
            ReadOnlyMemory<ComponentDescriptor> catalog = world.Integration.Catalog.Components;
            _runtimeComponentBuffer = new ComponentId[catalog.Length];
            if (!world.TryGetComponents(entity, _runtimeComponentBuffer, out int count))
            {
                _runtimeComponentIds = null;
                return null;
            }

            _runtimeComponentIds = new ComponentId[count];
            Array.Copy(_runtimeComponentBuffer, _runtimeComponentIds, count);
            Dictionary<ComponentId, ComponentDescriptor> descriptorsById = catalog.ToArray()
                .ToDictionary(descriptor => descriptor.Id);

            return OrderComponentDescriptors(_runtimeComponentIds
                .Where(descriptorsById.ContainsKey)
                .Select(id => descriptorsById[id]));
        }

        private ComponentDescriptor[] OrderComponentDescriptors(IEnumerable<ComponentDescriptor> descriptors)
        {
            return descriptors
                .OrderBy(descriptor => GetComponentOrder(descriptor.Schema.Value))
                .ThenBy(descriptor => GetShortTypeName(descriptor.ValueType), StringComparer.Ordinal)
                .ToArray();
        }

        private VisualElement BuildRuntimeHeader(SceneWorld world, Entity entity)
        {
            var card = new VisualElement();
            card.AddToClassList("ecs-entity-header");
            var titleRow = new VisualElement();
            titleRow.AddToClassList("ecs-entity-title-row");
            titleRow.Add(CreateEntityIcon());
            bool hasStableId = world.TryGetStableId(entity, out string stableId);
            string name = hasStableId && world.TryGetAuthoring(stableId, out EntityAuthoring authoring)
                ? authoring.Name
                : $"Entity {entity}";
            titleRow.Add(CreateClassLabel(name, "ecs-runtime-entity-name"));
            card.Add(titleRow);
            AddEntityInfo(card, entity, stableId);
            card.Add(BuildRuntimeViewField(world, entity));
            return card;
        }

        private void DrawRuntimeValue(VisualElement body, SceneWorld world, Entity entity,
            ComponentDescriptor descriptor, ComponentSnapshot snapshot)
        {
            bool canWrite = (descriptor.Capabilities & ComponentCapabilities.Write) != 0
                && descriptor.ValueType.IsValueType;
            int previousChildCount = body.childCount;
            ComponentPropertyBinding[] properties = ComponentPropertyBagFields.Build(body, descriptor,
                snapshot.Value, canWrite,
                (path, next) => WriteRuntimeField(world, entity, descriptor, path, next),
                (label, current, onChanged) => CreateRuntimeEntityReferenceField(world, label, current, onChanged));

            if (properties.Length > 0)
            {
                _runtimeBindings.Add(new RuntimeValueBinding(descriptor, properties));
            }
            else if (body.childCount == previousChildCount)
            {
                body.Add(CreateClassLabel("Component has no exposed properties.", "ecs-empty-component"));
            }
        }

        private VisualElement CreateRuntimeEntityReferenceField(SceneWorld world, string label, Entity current,
            Action<object> onChanged)
        {
            Entity[] authored = world.AuthoredEntities;
            var values = new List<Entity>(authored.Length + 2) { default };
            var options = new List<string>(authored.Length + 2) { "None" };
            var names = new List<string>(authored.Length);
            for (int i = 0; i < authored.Length; i++)
            {
                Entity candidate = authored[i];
                if (!world.Integration.IsAlive(candidate)) continue;
                names.Add(GetRuntimeEntityName(world, candidate));
            }

            HashSet<string> duplicateNames = names
                .GroupBy(name => name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            int currentIndex = current.IsValid ? -1 : 0;
            int nameIndex = 0;
            for (int i = 0; i < authored.Length; i++)
            {
                Entity candidate = authored[i];
                if (!world.Integration.IsAlive(candidate)) continue;
                string name = names[nameIndex++];
                string stableId = world.TryGetStableId(candidate, out string id) ? id : string.Empty;
                values.Add(candidate);
                options.Add(duplicateNames.Contains(name) && !string.IsNullOrEmpty(stableId)
                    ? $"{name} ({stableId.Substring(0, Math.Min(8, stableId.Length))})"
                    : name);
                if (candidate == current) currentIndex = values.Count - 1;
            }

            if (current.IsValid && currentIndex < 0)
            {
                values.Add(current);
                options.Add($"Current ({FormatValue(typeof(Entity), current, world)})");
                currentIndex = values.Count - 1;
            }

            var fieldControl = new DropdownField(label, options, currentIndex);
            AddInspectorFieldClasses(fieldControl);
            fieldControl.userData = new RuntimeEntityReferenceChoices(options.ToArray(), values.ToArray());
            fieldControl.RegisterValueChangedCallback(evt =>
            {
                int index = options.IndexOf(evt.newValue);
                Entity next = index >= 0 && index < values.Count ? values[index] : default;
                onChanged(next);
            });
            return fieldControl;
        }

        private string GetRuntimeEntityName(SceneWorld world, Entity entity)
        {
            return world.TryGetStableId(entity, out string stableId)
                && world.TryGetAuthoring(stableId, out EntityAuthoring authoring)
                    ? authoring.Name
                    : entity.ToString();
        }

        private void WriteRuntimeField(SceneWorld world, Entity entity, ComponentDescriptor descriptor,
            string[] path, object next)
        {
            if (!world.Integration.TryRead(entity, descriptor.Id, out ComponentSnapshot snapshot, out EcsReadError readError))
            {
                Debug.LogWarning($"Could not read {descriptor.Name} before editing: {readError.Code}.");
                RefreshRuntimeView();
                return;
            }

            object updated = snapshot.Value;
            if (!PropertyBagValueAccess.TrySetValue(ref updated, descriptor.ValueType, path, next))
            {
                Debug.LogWarning($"Could not edit {descriptor.Name}: the property path is unavailable.");
                RefreshRuntimeView();
                return;
            }

            if (!world.Integration.TryWrite(entity, descriptor.Id, updated, snapshot.Stamp,
                    out _, out EcsWriteError writeError))
            {
                Debug.LogWarning($"Could not edit {descriptor.Name}: {writeError.Code}.");
                RefreshRuntimeView();
            }
        }

        private string FormatValue(Type type, object value, SceneWorld world)
        {
            if (type == typeof(Entity) && value is Entity entity)
            {
                if (!entity.IsValid) return "None";
                if (world.TryGetStableId(entity, out string stableId) && world.TryGetAuthoring(stableId, out EntityAuthoring authoring))
                {
                    return authoring.Name;
                }

                return entity.ToString();
            }

            if (value is float3 vector)
            {
                return $"({vector.x:G}, {vector.y:G}, {vector.z:G})";
            }

            return value?.ToString() ?? "None";
        }

        private static Label CreateClassLabel(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        private static Label CreateEntityIcon() => CreateClassLabel("◆", "ecs-entity-icon");

        private void RefreshRuntimeView()
        {
            if (SyncSelection())
            {
                RefreshContent();
                return;
            }
            if (!_target.IsRuntime || !EditorApplication.isPlaying || _header == null || _componentList == null)
            {
                return;
            }

            SceneWorld world = _target.RuntimeWorld;
            Entity entity = _target.RuntimeEntity;
            if (world == null || !world.IsBound || !world.Integration.IsAlive(entity))
            {
                if (_runtimeComponentIds != null)
                {
                    _runtimeComponentIds = null;
                    _runtimeComponentBuffer = null;
                    _runtimeBindings.Clear();
                    RefreshContent();
                }

                return;
            }

            if (_runtimeComponentBuffer == null || _runtimeComponentIds == null)
            {
                RefreshContent();
                return;
            }

            if (!world.TryGetComponents(entity, _runtimeComponentBuffer, out int count)
                || count != _runtimeComponentIds.Length)
            {
                RefreshContent();
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (_runtimeComponentBuffer[i] != _runtimeComponentIds[i])
                {
                    RefreshContent();
                    return;
                }
            }

            for (int i = 0; i < _runtimeBindings.Count; i++)
            {
                RuntimeValueBinding binding = _runtimeBindings[i];
                if (!world.Integration.TryRead(entity, binding.Descriptor.Id, out ComponentSnapshot snapshot, out _))
                {
                    continue;
                }

                for (int fieldIndex = 0; fieldIndex < binding.Properties.Length; fieldIndex++)
                {
                    ComponentPropertyBinding property = binding.Properties[fieldIndex];
                    VisualElement control = property.Control;
                    VisualElement focused = control.panel?.focusController?.focusedElement as VisualElement;
                    if (focused != null && control.Contains(focused))
                    {
                        continue;
                    }

                    if (PropertyBagValueAccess.TryGetValue(snapshot.Value, binding.Descriptor.ValueType,
                            property.Path, out object value))
                    {
                        SetRuntimeFieldValue(control, property.ValueType, value, world);
                    }
                }
            }
        }

        private void SetRuntimeFieldValue(VisualElement control, Type type, object value, SceneWorld world)
        {
            if (control is FloatField floatField)
            {
                floatField.SetValueWithoutNotify(value == null ? 0f : (float)value);
            }
            else if (control is DoubleField doubleField)
            {
                doubleField.SetValueWithoutNotify(value == null ? 0d : (double)value);
            }
            else if (control is TextField textField)
            {
                textField.SetValueWithoutNotify(type == typeof(string) ? (string)value ?? string.Empty
                    : type == typeof(char) ? value == null ? string.Empty : value.ToString()
                    : FormatValue(type, value, world));
            }
            else if (control is IntegerField integerField)
            {
                integerField.SetValueWithoutNotify(value == null ? 0 : Convert.ToInt32(value));
            }
            else if (control is LongField longField)
            {
                longField.SetValueWithoutNotify(value == null ? 0L : Convert.ToInt64(value));
            }
            else if (control is Toggle toggle)
            {
                toggle.SetValueWithoutNotify(value != null && (bool)value);
            }
            else if (control is Vector2Field vector2Field && value is Vector2 vector2)
            {
                vector2Field.SetValueWithoutNotify(vector2);
            }
            else if (control is Vector3Field vectorField && value is Vector3 vector3)
            {
                vectorField.SetValueWithoutNotify(vector3);
            }
            else if (control is Vector3Field float3Field && value is float3 vector)
            {
                float3Field.SetValueWithoutNotify(new Vector3(vector.x, vector.y, vector.z));
            }
            else if (control is Vector4Field vector4Field && value is Vector4 vector4)
            {
                vector4Field.SetValueWithoutNotify(vector4);
            }
            else if (control is ColorField colorField && value is Color color)
            {
                colorField.SetValueWithoutNotify(color);
            }
            else if (control is EnumField enumField && value is Enum enumValue)
            {
                enumField.SetValueWithoutNotify(enumValue);
            }
            else if (control is ObjectField objectField)
            {
                objectField.SetValueWithoutNotify(value as UnityEngine.Object);
            }
            else if (control is DropdownField entityField
                && type == typeof(Entity)
                && entityField.userData is RuntimeEntityReferenceChoices choices)
            {
                int index = Array.IndexOf(choices.Entities, (Entity)value);
                if (index >= 0)
                {
                    entityField.SetValueWithoutNotify(choices.Labels[index]);
                }
            }
        }

        private ComponentEntry[] ProjectAuthoringComponents(EntityAuthoring authoring)
        {
            Dictionary<ulong, ComponentDescriptor> descriptors = _previewWorld.Integration.Catalog.Components
                .ToArray()
                .ToDictionary(descriptor => descriptor.Schema.Value);

            return authoring.Components
                .Select(data => new ComponentEntry(data, TryGetDescriptor(data, descriptors)))
                .OrderBy(entry => entry.Descriptor.HasValue ? GetComponentOrder(entry.Descriptor.Value.Schema.Value) : int.MaxValue)
                .ThenBy(entry => entry.Descriptor.HasValue
                    ? GetShortTypeName(entry.Descriptor.Value.ValueType)
                    : entry.Data?.SchemaIdText ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        private static ComponentDescriptor? TryGetDescriptor(ComponentData data,
            IReadOnlyDictionary<ulong, ComponentDescriptor> descriptors)
        {
            ulong? schema = GetSchemaId(data);
            return schema.HasValue && descriptors.TryGetValue(schema.Value, out ComponentDescriptor descriptor)
                ? descriptor
                : null;
        }

        private static string GetComponentMenuPath(ComponentDescriptor descriptor)
        {
            string namespacePath = descriptor.ValueType.Namespace?.Replace('.', '/') ?? string.Empty;
            string shortName = GetShortTypeName(descriptor.ValueType);
            return string.IsNullOrEmpty(namespacePath) ? shortName : $"{namespacePath}/{shortName}";
        }

        private static string GetShortTypeName(Type type)
        {
            string name = type.Name;
            int genericMarker = name.IndexOf('`');
            return genericMarker < 0 ? name : name.Substring(0, genericMarker);
        }

        private void AddComponent(EntityAuthoring authoring, ComponentDescriptor descriptor)
        {
            Undo.RecordObject(Authoring, $"Add {GetShortTypeName(descriptor.ValueType)}");
            ComponentData data = ComponentData.CreateDefault(descriptor);
            if (!_previewWorld.AddComponent(authoring, descriptor, data, out EcsWriteError error, out string failureReason))
            {
                Debug.LogError($"Could not add {GetShortTypeName(descriptor.ValueType)}: {failureReason ?? error.Code.ToString()}.", Authoring);
                RebuildPreviewAndView();
                return;
            }

            authoring.Components.Add(data);
            MarkSceneDirty();
            RebuildPreviewAndView();
        }

        private void RemoveComponent(EntityAuthoring authoring, ComponentData data,
            ComponentDescriptor? descriptor = null)
        {
            int index = authoring.Components.IndexOf(data);
            if (index < 0 || index >= authoring.Components.Count) return;
            if (!descriptor.HasValue && _previewWorld.TryGetDescriptor(data, out ComponentDescriptor resolved))
            {
                descriptor = resolved;
            }

            if (!descriptor.HasValue)
            {
                Undo.RecordObject(Authoring, "Remove Unregistered Delta ECS Component");
                authoring.Components.RemoveAt(index);
                MarkSceneDirty();
                RebuildPreviewAndView();
                return;
            }

            ComponentDescriptor component = descriptor.Value;
            Undo.RecordObject(Authoring, $"Remove {GetShortTypeName(component.ValueType)}");
            if (!_previewWorld.RemoveComponent(authoring, component, out EcsWriteError error))
            {
                Debug.LogError($"Could not remove {GetShortTypeName(component.ValueType)}: {error.Code}.", Authoring);
                return;
            }

            authoring.Components.RemoveAt(index);
            MarkSceneDirty();
            RebuildPreviewAndView();
        }

        private void ResetComponent(EntityAuthoring authoring, ComponentData data, ComponentDescriptor descriptor)
        {
            Undo.RecordObject(Authoring, $"Reset {GetShortTypeName(descriptor.ValueType)}");
            object value = Activator.CreateInstance(descriptor.ValueType);
            if (_previewWorld.TryWrite(authoring, data, descriptor, value, out EcsWriteError error)) MarkSceneDirty();
            else Debug.LogError($"Could not reset {GetShortTypeName(descriptor.ValueType)}: {error.Code}.", Authoring);
            RefreshContent();
        }

        private static ulong? GetSchemaId(ComponentData data)
        {
            return data != null && data.TryGetSchemaId(out SchemaId schema) ? schema.Value : null;
        }

        private static bool IsComponentExpanded(ulong schema) =>
            InspectorState.instance.IsComponentExpanded(schema);

        private static void SetComponentExpanded(ulong schema, bool expanded) =>
            InspectorState.instance.SetComponentExpanded(schema, expanded);

        private static int GetComponentOrder(ulong schema) =>
            InspectorState.instance.GetComponentOrder(schema);

        private void MoveComponent(ulong schema, int direction)
        {
            int index = _componentEntries.FindIndex(entry => entry.Descriptor?.Schema.Value == schema);
            int next = index + direction;
            if (index < 0 || next < 0 || next >= _componentEntries.Count) return;
            _componentList.viewController.Move(index, next);
        }

        private void RebuildAfterUndo()
        {
            SyncSelection(rebuildPreview: false);
            RebuildPreviewWorld();
            HierarchyNodeHandler.RefreshAll();
            RefreshContent();
        }

        private void RebuildPreviewWorld()
        {
            _previewWorld?.Dispose();
            _previewWorld = null;
            if (Authoring == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            try
            {
                _previewWorld = SceneWorld.CreateAuthoringPreview(Authoring);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, Authoring);
            }
        }

        private void RebuildPreviewAndView()
        {
            RebuildPreviewWorld();
            RefreshContent();
        }

        private void MarkSceneDirty()
        {
            HierarchyMenu.MarkSceneDirty(Authoring);
        }

        private readonly struct ComponentEntry
        {
            public readonly ComponentData Data;
            public readonly ComponentDescriptor? Descriptor;
            public ComponentEntry(ComponentData data, ComponentDescriptor? descriptor)
            {
                Data = data;
                Descriptor = descriptor;
            }
        }

        private readonly struct RuntimeValueBinding
        {
            public readonly ComponentDescriptor Descriptor;
            public readonly ComponentPropertyBinding[] Properties;

            public RuntimeValueBinding(ComponentDescriptor descriptor, ComponentPropertyBinding[] properties)
            {
                Descriptor = descriptor;
                Properties = properties;
            }
        }

        private sealed class RuntimeEntityReferenceChoices
        {
            public readonly string[] Labels;
            public readonly Entity[] Entities;

            public RuntimeEntityReferenceChoices(string[] labels, Entity[] entities)
            {
                Labels = labels;
                Entities = entities;
            }
        }
    }
}
