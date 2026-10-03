using System;
using System.Collections.Generic;
using System.Reflection;
using Delta.ECS;
using Delta.ECS.Integration;
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

        public ComponentData(ulong schemaId, string json)
        {
            _schemaId = schemaId.ToString("X16");
            _json = json;
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

            FieldInfo[] fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            List<EntityReference> references = EntityReferenceList;
            for (int i = 0; i < references.Count; i++)
            {
                EntityReference reference = references[i];
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (field.Name == reference.FieldName && field.FieldType == typeof(Entity))
                    {
                        field.SetValue(value, resolveEntity(reference.TargetEntityId));
                        break;
                    }
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

            FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType != typeof(Entity))
                {
                    continue;
                }

                Entity target = (Entity)field.GetValue(value);
                string targetId = stableIdResolver(target) ?? string.Empty;
                if (!string.IsNullOrEmpty(targetId))
                {
                    EntityReferenceList.Add(new EntityReference(field.Name, targetId));
                }
            }
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

        private List<EntityReference> EntityReferenceList =>
            _entityReferences ??= new List<EntityReference>();

        public static ComponentData CreateDefault(ComponentDescriptor descriptor)
        {
            object value = Activator.CreateInstance(descriptor.ValueType);
            return new ComponentData(descriptor.Schema.Value, JsonUtility.ToJson(value));
        }
    }
}
