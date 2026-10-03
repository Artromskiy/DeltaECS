using System;
using System.Collections.Generic;
using UnityEngine;

namespace Delta.ECS.Unity
{
    [Serializable]
    internal sealed class EntityAuthoring
    {
        [SerializeField] private string _stableId;
        [SerializeField] private string _name = "Entity";
        [SerializeField] private List<ComponentData> _components = new();

        public string StableId
        {
            get
            {
                EnsureStableId();
                return _stableId;
            }
        }

        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? "Entity" : _name;
            set => _name = string.IsNullOrWhiteSpace(value) ? "Entity" : value;
        }

        public IList<ComponentData> Components => _components ??= new List<ComponentData>();

        internal bool EnsureStableId()
        {
            if (!string.IsNullOrEmpty(_stableId))
            {
                return false;
            }

            _stableId = Guid.NewGuid().ToString("N");
            return true;
        }

        internal void ReplaceStableId()
        {
            _stableId = Guid.NewGuid().ToString("N");
        }
    }
}
