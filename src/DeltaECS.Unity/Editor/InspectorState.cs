using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Delta.ECS.Unity.Editor
{
    [FilePath("Library/InspectorState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class InspectorState : ScriptableSingleton<InspectorState>
    {
        private const string LegacyOrderPreferencePrefix = "DeltaEcs.Inspector.ComponentOrder.";
        private const string LegacyCollapsePreferencePrefix = "DeltaEcs.Inspector.ComponentCollapsed.";

        [Serializable]
        private struct ComponentFoldout
        {
            public string Schema;
            public bool Expanded;
        }

        [SerializeField] private List<string> _componentOrder = new();
        [SerializeField] private List<ComponentFoldout> _componentFoldouts = new();

        [NonSerialized] private bool _legacyOrderLoaded;
        [NonSerialized] private Dictionary<ulong, int> _componentOrderIndices;

        internal int GetComponentOrder(ulong schema)
        {
            EnsureLegacyOrderLoaded();
            EnsureOrderIndices();
            return _componentOrderIndices.TryGetValue(schema, out int index) ? index : int.MaxValue;
        }

        internal List<ulong> GetComponentOrderSnapshot()
        {
            EnsureLegacyOrderLoaded();
            var order = new List<ulong>(_componentOrder.Count);
            for (int i = 0; i < _componentOrder.Count; i++)
            {
                if (TryParseSchema(_componentOrder[i], out ulong schema))
                {
                    order.Add(schema);
                }
            }

            return order;
        }

        internal void SetComponentOrder(IEnumerable<ulong> schemas)
        {
            EnsureLegacyOrderLoaded();
            var nextOrder = schemas
                .Distinct()
                .Select(schema => schema.ToString("X16", CultureInfo.InvariantCulture))
                .ToList();

            if (_componentOrder.SequenceEqual(nextOrder, StringComparer.Ordinal))
            {
                return;
            }

            _componentOrder = nextOrder;
            _componentOrderIndices = null;
            Save(true);
        }

        internal void EnsureComponentOrder(IEnumerable<ulong> schemas)
        {
            List<ulong> order = GetComponentOrderSnapshot();
            HashSet<ulong> knownSchemas = order.ToHashSet();
            bool changed = false;
            foreach (ulong schema in schemas)
            {
                if (knownSchemas.Add(schema))
                {
                    order.Add(schema);
                    changed = true;
                }
            }

            if (changed)
            {
                SetComponentOrder(order);
            }
        }

        internal bool MoveComponent(ulong schema, int direction, IReadOnlyList<ulong> visibleOrder)
        {
            List<ulong> order = GetComponentOrderSnapshot();
            var visibleSchemas = visibleOrder.Distinct().ToList();
            int index = visibleSchemas.IndexOf(schema);
            int nextIndex = index + direction;
            if (index < 0 || nextIndex < 0 || nextIndex >= visibleSchemas.Count)
            {
                return false;
            }

            (visibleSchemas[index], visibleSchemas[nextIndex]) = (visibleSchemas[nextIndex], visibleSchemas[index]);
            var visibleSet = visibleSchemas.ToHashSet();
            int nextVisibleIndex = 0;
            for (int i = 0; i < order.Count && nextVisibleIndex < visibleSchemas.Count; i++)
            {
                if (visibleSet.Contains(order[i]))
                {
                    order[i] = visibleSchemas[nextVisibleIndex++];
                }
            }

            SetComponentOrder(order);
            return true;
        }

        internal bool IsComponentExpanded(ulong schema)
        {
            int index = FindFoldout(schema);
            if (index >= 0)
            {
                return _componentFoldouts[index].Expanded;
            }

            bool expanded = EditorPrefs.GetBool(GetLegacyCollapseKey(schema), true);
            _componentFoldouts.Add(new ComponentFoldout { Schema = FormatSchema(schema), Expanded = expanded });
            Save(true);
            return expanded;
        }

        internal void SetComponentExpanded(ulong schema, bool expanded)
        {
            int index = FindFoldout(schema);
            if (index >= 0)
            {
                ComponentFoldout foldout = _componentFoldouts[index];
                if (foldout.Expanded == expanded)
                {
                    return;
                }

                foldout.Expanded = expanded;
                _componentFoldouts[index] = foldout;
            }
            else
            {
                _componentFoldouts.Add(new ComponentFoldout { Schema = FormatSchema(schema), Expanded = expanded });
            }

            Save(true);
        }

        private int FindFoldout(ulong schema)
        {
            string schemaText = FormatSchema(schema);
            for (int i = 0; i < _componentFoldouts.Count; i++)
            {
                if (string.Equals(_componentFoldouts[i].Schema, schemaText, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureLegacyOrderLoaded()
        {
            if (_legacyOrderLoaded)
            {
                return;
            }

            _legacyOrderLoaded = true;
            if (_componentOrder.Count > 0)
            {
                return;
            }

            string legacyOrder = EditorPrefs.GetString($"{LegacyOrderPreferencePrefix}{Application.dataPath}", string.Empty);
            string[] schemas = legacyOrder.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < schemas.Length; i++)
            {
                if (TryParseSchema(schemas[i], out ulong schema))
                {
                    _componentOrder.Add(FormatSchema(schema));
                }
            }

            if (_componentOrder.Count > 0)
            {
                _componentOrderIndices = null;
                Save(true);
            }
        }

        private void EnsureOrderIndices()
        {
            if (_componentOrderIndices != null)
            {
                return;
            }

            _componentOrderIndices = new Dictionary<ulong, int>(_componentOrder.Count);
            for (int i = 0; i < _componentOrder.Count; i++)
            {
                if (TryParseSchema(_componentOrder[i], out ulong schema))
                {
                    _componentOrderIndices.TryAdd(schema, i);
                }
            }
        }

        private static string GetLegacyCollapseKey(ulong schema) =>
            $"{LegacyCollapsePreferencePrefix}{Application.dataPath}:{FormatSchema(schema)}";

        private static string FormatSchema(ulong schema) => schema.ToString("X16", CultureInfo.InvariantCulture);

        private static bool TryParseSchema(string value, out ulong schema) =>
            ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out schema);
    }
}
