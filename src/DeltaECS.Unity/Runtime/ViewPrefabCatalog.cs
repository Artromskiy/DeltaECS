#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Delta.ECS.Unity
{
    /// <summary>Maps <see cref="ViewToken.Id"/> values to Unity view prefabs.</summary>
    [CreateAssetMenu(fileName = "ViewPrefabCatalog", menuName = "Delta ECS/View Prefab Catalog")]
    public sealed class ViewPrefabCatalog : ScriptableObject
    {
        [SerializeField] private List<ViewPrefabEntry> _entries = new();
        [NonSerialized] private Dictionary<string, GameObject>? _prefabsById;

        /// <summary>Resolves a token identifier to its configured prefab.</summary>
        public bool TryGetPrefab(string id, [NotNullWhen(true)] out GameObject? prefab)
        {
            EnsureIndex();
            if (!string.IsNullOrEmpty(id)
                && _prefabsById!.TryGetValue(id, out prefab)
                && prefab != null)
            {
                return true;
            }

            prefab = null;
            return false;
        }

        private void OnValidate() => _prefabsById = null;

        private void EnsureIndex()
        {
            if (_prefabsById != null)
            {
                return;
            }

            _prefabsById = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            _entries ??= new List<ViewPrefabEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                ViewPrefabEntry entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Prefab == null)
                {
                    continue;
                }

                if (!_prefabsById.TryAdd(entry.Id, entry.Prefab))
                {
                    Debug.LogError($"Duplicate view prefab identifier '{entry.Id}' in '{name}'.", this);
                }
            }
        }

        [Serializable]
        private sealed class ViewPrefabEntry
        {
            [SerializeField] private string _id = string.Empty;
            [SerializeField] private GameObject? _prefab;

            internal string Id => _id;
            internal GameObject? Prefab => _prefab;
        }
    }
}
