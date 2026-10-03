using System;
using UnityEngine;

namespace Delta.ECS.Unity
{
    [Serializable]
    internal sealed class EntityReference
    {
        [SerializeField] private string _fieldName;
        [SerializeField] private string _targetEntityId;

        public string FieldName => _fieldName;
        public string TargetEntityId => _targetEntityId;

        public EntityReference(string fieldName, string targetEntityId)
        {
            _fieldName = fieldName;
            _targetEntityId = targetEntityId;
        }
    }
}
