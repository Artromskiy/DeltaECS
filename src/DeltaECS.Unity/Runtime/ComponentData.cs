using System;
using System.Collections.Generic;

using Delta.ECS;

using UnityEngine;

namespace Delta.ECS.Unity
{
    [Serializable]
    internal sealed class ComponentData
    {
        [SerializeField] private string _schemaId;
        [SerializeField, TextArea(2, 8)] private string _json;
        [SerializeField] private List<EntityReference> _entityReferences = new();

        public string SchemaIdText => _schemaId;
        public string Json => _json;
        public IReadOnlyList<EntityReference> EntityReferences => _entityReferences ??= new List<EntityReference>();

        private List<EntityReference> EntityReferenceList =>
            _entityReferences ??= new List<EntityReference>();

        public ComponentData(ulong schemaId, string json)
        {
            _schemaId = schemaId.ToString("X16");
            _json = json;
        }

        public static ComponentData CreateDefault(ComponentRegistry components, ComponentId componentId)
        {
            if (components.IsTag(componentId))
            {
                return new ComponentData(components.GetSchemaId(componentId).Value, "{}");
            }

            object value = Activator.CreateInstance(components.GetComponentType(componentId));
            return new ComponentData(components.GetSchemaId(componentId).Value, JsonUtility.ToJson(value));
        }

        public bool TryGetSchemaId(out SchemaId schemaId)
        {
            if (ulong.TryParse(_schemaId, System.Globalization.NumberStyles.HexNumber, null, out ulong value))
            {
                schemaId = new SchemaId(value);
                return true;
            }

            schemaId = default;
            return false;
        }

        public object CreateValue(Type componentType, Func<string, Entity> resolveEntity)
        {
            object value = string.IsNullOrEmpty(_json)
                ? Activator.CreateInstance(componentType)
                : JsonUtility.FromJson(_json, componentType);

            if (value == null)
            {
                value = Activator.CreateInstance(componentType);
            }

            List<EntityReference> references = EntityReferenceList;
            for (int i = 0; i < references.Count; i++)
            {
                EntityReference reference = references[i];
                object resolvedValue = resolveEntity(reference.TargetEntityId);
                object current = value;
                if (PropertyBagValueAccess.TrySetValue(ref current, componentType,
                        PropertyBagValueAccess.ParsePath(reference.FieldName), resolvedValue))
                {
                    value = current;
                }
            }

            return value;
        }

        public void StoreValue(object value, Func<Entity, string> stableIdResolver)
        {
            if (value == null)
            {
                UnityThrowHelpers.ThrowArgumentNull(nameof(value));
            }

            _json = JsonUtility.ToJson(value);
            EntityReferenceList.Clear();
            PropertyBagValueAccess.CollectEntityReferences(value, value.GetType(), stableIdResolver, EntityReferenceList);
        }

        public void StoreDefaultTag()
        {
            _json = "{}";
            EntityReferenceList.Clear();
        }

        public string GetEntityReference(string fieldName)
        {
            List<EntityReference> references = EntityReferenceList;
            for (int i = 0; i < references.Count; i++)
            {
                if (string.Equals(references[i].FieldName, fieldName, StringComparison.Ordinal))
                {
                    return references[i].TargetEntityId;
                }
            }

            return string.Empty;
        }
    }
}
