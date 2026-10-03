using System;
using System.Collections.Generic;
using UnityEngine;

namespace Delta.ECS.Unity
{
    /// <summary>Serializable Delta ECS authoring data stored by a scene authoring component.</summary>
    [Serializable]
    internal sealed class SceneData
    {
        [SerializeField] private List<EntityAuthoring> _entities = new();
        [NonSerialized] private Action<SceneDataChange> _changed;

        public IList<EntityAuthoring> Entities => _entities ??= new List<EntityAuthoring>();
        public event Action<SceneDataChange> Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public EntityAuthoring AddEntity(string name = "Entity")
        {
            var entity = new EntityAuthoring { Name = name };
            Entities.Add(entity);
            EnsureStableIds();
            _changed?.Invoke(new SceneDataChange(SceneDataChangeKind.EntityCreated, entity.StableId));
            return entity;
        }

        public bool RemoveEntity(string stableId)
        {
            IList<EntityAuthoring> entities = Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityAuthoring entity = entities[i];
                if (entity != null && string.Equals(entity.StableId, stableId, StringComparison.Ordinal))
                {
                    entities.RemoveAt(i);
                    _changed?.Invoke(new SceneDataChange(SceneDataChangeKind.EntityDestroyed, stableId));
                    return true;
                }
            }

            return false;
        }

        public bool RenameEntity(string stableId, string name)
        {
            if (!TryGetEntity(stableId, out EntityAuthoring entity)
                || string.Equals(entity.Name, name, StringComparison.Ordinal))
            {
                return false;
            }

            entity.Name = name;
            _changed?.Invoke(new SceneDataChange(SceneDataChangeKind.EntityRenamed, stableId));
            return true;
        }

        public bool TryGetEntity(string stableId, out EntityAuthoring entity)
        {
            IList<EntityAuthoring> entities = Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityAuthoring candidate = entities[i];
                if (candidate != null && string.Equals(candidate.StableId, stableId, StringComparison.Ordinal))
                {
                    entity = candidate;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        public bool EnsureStableIds()
        {
            bool changed = false;
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            IList<EntityAuthoring> entities = Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                EntityAuthoring entity = entities[i];
                if (entity == null)
                {
                    continue;
                }

                changed |= entity.EnsureStableId();
                while (!usedIds.Add(entity.StableId))
                {
                    entity.ReplaceStableId();
                    changed = true;
                }
            }

            return changed;
        }
    }

    internal enum SceneDataChangeKind
    {
        EntityCreated,
        EntityDestroyed,
        EntityRenamed
    }

    internal readonly struct SceneDataChange
    {
        public readonly SceneDataChangeKind Kind;
        public readonly string StableId;

        public SceneDataChange(SceneDataChangeKind kind, string stableId)
        {
            Kind = kind;
            StableId = stableId;
        }
    }
}
