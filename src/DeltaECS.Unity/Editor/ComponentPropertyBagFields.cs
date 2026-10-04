using System;
using System.Collections.Generic;
using Delta;
using Delta.ECS;
using Delta.ECS.Integration;
using Delta.ECS.Unity;
using Unity.Properties;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    internal readonly struct ComponentPropertyBinding
    {
        internal readonly string[] Path;
        internal readonly Type ValueType;
        internal readonly VisualElement Control;

        internal ComponentPropertyBinding(string[] path, Type valueType, VisualElement control)
        {
            Path = path;
            ValueType = valueType;
            Control = control;
        }
    }

    internal static class ComponentPropertyBagFields
    {
        internal static ComponentPropertyBinding[] Build(VisualElement parent, ComponentDescriptor descriptor,
            object value, bool canWrite, Action<string[], object> onChanged,
            Func<string, Entity, Action<object>, VisualElement> createEntityField)
        {
            if (value == null)
            {
                return Array.Empty<ComponentPropertyBinding>();
            }

            IPropertyBag propertyBag = PropertyBag.GetPropertyBag(descriptor.ValueType);
            if (propertyBag == null)
            {
                return Array.Empty<ComponentPropertyBinding>();
            }

            var bindings = new List<ComponentPropertyBinding>();
            var visitor = new PropertyFieldVisitor(parent, canWrite, Array.Empty<string>(), 0,
                bindings, onChanged, createEntityField);
            propertyBag.Accept(visitor, ref value);
            return bindings.ToArray();
        }

        private sealed class PropertyFieldVisitor : IPropertyBagVisitor, IPropertyVisitor
        {
            private readonly VisualElement _parent;
            private readonly bool _canWrite;
            private readonly string[] _path;
            private readonly int _depth;
            private readonly List<ComponentPropertyBinding> _bindings;
            private readonly Action<string[], object> _onChanged;
            private readonly Func<string, Entity, Action<object>, VisualElement> _createEntityField;

            internal PropertyFieldVisitor(VisualElement parent, bool canWrite, string[] path, int depth,
                List<ComponentPropertyBinding> bindings, Action<string[], object> onChanged,
                Func<string, Entity, Action<object>, VisualElement> createEntityField)
            {
                _parent = parent;
                _canWrite = canWrite;
                _path = path;
                _depth = depth;
                _bindings = bindings;
                _onChanged = onChanged;
                _createEntityField = createEntityField;
            }

            public void Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
            {
                foreach (var property in properties.GetProperties(ref container))
                {
                    ((IPropertyAccept<TContainer>)property).Accept(this, ref container);
                }
            }

            public void Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
            {
                TValue value = property.GetValue(ref container);
                Type valueType = typeof(TValue);
                string[] path = Append(_path, property.Name);
                string label = GetLabel(property.Name);
                bool canWrite = _canWrite && !property.IsReadOnly;

                VisualElement control = CreateFieldControl(label, valueType, value,
                    next => _onChanged(path, next), canWrite, _createEntityField);
                if (control != null)
                {
                    _parent.Add(control);
                    _bindings.Add(new ComponentPropertyBinding(path, valueType, control));
                    return;
                }

                if (value != null && _depth < 24)
                {
                    object nestedValue = value;
                    IPropertyBag nestedBag = PropertyBag.GetPropertyBag(nestedValue.GetType());
                    if (nestedBag != null)
                    {
                        var foldout = new Foldout { text = label, value = true };
                        foldout.AddToClassList("ecs-property-foldout");
                        _parent.Add(foldout);
                        int previousCount = _bindings.Count;
                        bool canWriteNested = canWrite && valueType.IsValueType;
                        var nestedVisitor = new PropertyFieldVisitor(foldout, canWriteNested, path,
                            _depth + 1, _bindings, _onChanged, _createEntityField);
                        nestedBag.Accept(nestedVisitor, ref nestedValue);
                        if (_bindings.Count == previousCount)
                        {
                            var empty = new Label("No exposed properties.");
                            empty.AddToClassList("ecs-empty-component");
                            foldout.Add(empty);
                        }

                        return;
                    }
                }

                string detail = value == null ? "Null" : $"Unsupported: {valueType.Name}";
                var unsupported = new Label($"{label}  ·  {detail}");
                unsupported.AddToClassList("ecs-unsupported-field");
                _parent.Add(unsupported);
            }
        }

        private static VisualElement CreateFieldControl(string label, Type type, object value,
            Action<object> onChanged, bool canWrite,
            Func<string, Entity, Action<object>, VisualElement> createEntityField)
        {
            VisualElement control;
            if (type == typeof(float))
            {
                var field = new FloatField(label) { value = value == null ? 0f : (float)value, isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(double))
            {
                var field = new DoubleField(label) { value = value == null ? 0d : (double)value, isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(string))
            {
                var field = new TextField(label) { value = (string)value ?? string.Empty, isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(char))
            {
                var field = new TextField(label) { value = value == null ? string.Empty : value.ToString(), isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(string.IsNullOrEmpty(evt.newValue) ? '\0' : evt.newValue[0]));
                control = field;
            }
            else if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short)
                || type == typeof(ushort) || type == typeof(int))
            {
                var field = new IntegerField(label) { value = value == null ? 0 : Convert.ToInt32(value), isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(ConvertInteger(type, evt.newValue)));
                control = field;
            }
            else if (type == typeof(uint) || type == typeof(long))
            {
                var field = new LongField(label) { value = value == null ? 0L : Convert.ToInt64(value), isDelayed = true };
                field.RegisterValueChangedCallback(evt => onChanged(type == typeof(uint)
                    ? (object)(uint)Math.Max(0L, Math.Min(uint.MaxValue, evt.newValue))
                    : evt.newValue));
                control = field;
            }
            else if (type == typeof(ulong))
            {
                var field = new TextField(label) { value = value == null ? "0" : ((ulong)value).ToString(), isDelayed = true };
                field.RegisterValueChangedCallback(evt =>
                {
                    if (ulong.TryParse(evt.newValue, out ulong parsed)) onChanged(parsed);
                });
                control = field;
            }
            else if (type == typeof(bool))
            {
                var field = new Toggle(label) { value = value != null && (bool)value };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(float3))
            {
                float3 vector = value == null ? default : (float3)value;
                var field = new Vector3Field(label) { value = new Vector3(vector.x, vector.y, vector.z) };
                field.RegisterValueChangedCallback(evt => onChanged(new float3(evt.newValue.x, evt.newValue.y, evt.newValue.z)));
                control = field;
            }
            else if (type == typeof(Vector2))
            {
                var field = new Vector2Field(label) { value = value == null ? default : (Vector2)value };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(Vector3))
            {
                var field = new Vector3Field(label) { value = value == null ? default : (Vector3)value };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(Vector4))
            {
                var field = new Vector4Field(label) { value = value == null ? default : (Vector4)value };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(Color))
            {
                var field = new ColorField(label) { value = value == null ? default : (Color)value };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (type == typeof(Entity))
            {
                Entity current = value == null ? default : (Entity)value;
                control = createEntityField(label, current, onChanged);
            }
            else if (type.IsEnum)
            {
                var field = new EnumField(label, (Enum)value);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var field = new ObjectField(label)
                {
                    objectType = type,
                    allowSceneObjects = true,
                    value = value as UnityEngine.Object
                };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                control = field;
            }
            else
            {
                return null;
            }

            control.AddToClassList(BaseField<float>.alignedFieldUssClassName);
            control.AddToClassList("ecs-value-field");
            control.SetEnabled(canWrite);
            return control;
        }

        private static object ConvertInteger(Type type, int value)
        {
            if (type == typeof(byte)) return (byte)Mathf.Clamp(value, byte.MinValue, byte.MaxValue);
            if (type == typeof(sbyte)) return (sbyte)Mathf.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
            if (type == typeof(short)) return (short)Mathf.Clamp(value, short.MinValue, short.MaxValue);
            if (type == typeof(ushort)) return (ushort)Mathf.Clamp(value, ushort.MinValue, ushort.MaxValue);
            return value;
        }

        private static string[] Append(string[] path, string name)
        {
            var result = new string[path.Length + 1];
            Array.Copy(path, result, path.Length);
            result[path.Length] = name;
            return result;
        }

        private static string GetLabel(string name)
        {
            return int.TryParse(name, out int index)
                ? $"Element {index}"
                : ObjectNames.NicifyVariableName(name);
        }
    }
}
