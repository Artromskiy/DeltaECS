using System;
using System.Collections.Generic;
using Delta.ECS;
using Unity.Properties;

namespace Delta.ECS.Unity
{
    internal static class PropertyBagValueAccess
    {
        internal static bool TryGetValue(object container, Type containerType, string[] path, out object value)
        {
            value = null;
            if (container == null || path == null || path.Length == 0)
            {
                return false;
            }

            IPropertyBag propertyBag = PropertyBag.GetPropertyBag(containerType);
            if (propertyBag == null)
            {
                return false;
            }

            var visitor = new ReadPathVisitor(path, 0);
            propertyBag.Accept(visitor, ref container);
            value = visitor.Value;
            return visitor.Found;
        }

        internal static bool TrySetValue(ref object container, Type containerType, string[] path, object value)
        {
            if (container == null || path == null || path.Length == 0)
            {
                return false;
            }

            IPropertyBag propertyBag = PropertyBag.GetPropertyBag(containerType);
            if (propertyBag == null)
            {
                return false;
            }

            var visitor = new WritePathVisitor(path, 0, value);
            propertyBag.Accept(visitor, ref container);
            return visitor.Succeeded;
        }

        internal static string FormatPath(IReadOnlyList<string> path)
        {
            return string.Join("/", path);
        }

        internal static string[] ParsePath(string path)
        {
            return string.IsNullOrEmpty(path) ? Array.Empty<string>() : path.Split('/');
        }

        internal static void CollectEntityReferences(object container, Type containerType,
            Func<Entity, string> stableIdResolver, IList<EntityReference> references)
        {
            if (container == null)
            {
                return;
            }

            IPropertyBag propertyBag = PropertyBag.GetPropertyBag(containerType);
            if (propertyBag == null)
            {
                return;
            }

            var visitor = new EntityReferenceVisitor(stableIdResolver, references);
            propertyBag.Accept(visitor, ref container);
        }

        private sealed class ReadPathVisitor : IPropertyBagVisitor
        {
            private readonly string[] _path;
            private readonly int _pathIndex;

            internal object Value;
            internal bool Found;

            internal ReadPathVisitor(string[] path, int pathIndex)
            {
                _path = path;
                _pathIndex = pathIndex;
            }

            public void Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
            {
                foreach (var property in properties.GetProperties(ref container))
                {
                    if (!string.Equals(property.Name, _path[_pathIndex], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    object value = property.GetValue(ref container);
                    if (_pathIndex == _path.Length - 1)
                    {
                        Value = value;
                        Found = true;
                    }
                    else if (value != null)
                    {
                        IPropertyBag nestedBag = PropertyBag.GetPropertyBag(value.GetType());
                        if (nestedBag != null)
                        {
                            var nestedVisitor = new ReadPathVisitor(_path, _pathIndex + 1);
                            nestedBag.Accept(nestedVisitor, ref value);
                            Value = nestedVisitor.Value;
                            Found = nestedVisitor.Found;
                        }
                    }

                    return;
                }
            }
        }

        private sealed class WritePathVisitor : IPropertyBagVisitor
        {
            private readonly string[] _path;
            private readonly int _pathIndex;
            private readonly object _value;

            internal bool Succeeded;

            internal WritePathVisitor(string[] path, int pathIndex, object value)
            {
                _path = path;
                _pathIndex = pathIndex;
                _value = value;
            }

            public void Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
            {
                TContainer updated = container;
                foreach (var property in properties.GetProperties(ref updated))
                {
                    if (!string.Equals(property.Name, _path[_pathIndex], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (property.IsReadOnly)
                    {
                        return;
                    }

                    if (_pathIndex == _path.Length - 1)
                    {
                        property.SetValue(ref updated, _value);
                        Succeeded = true;
                    }
                    else
                    {
                        object nestedValue = property.GetValue(ref updated);
                        if (nestedValue == null)
                        {
                            return;
                        }

                        IPropertyBag nestedBag = PropertyBag.GetPropertyBag(nestedValue.GetType());
                        if (nestedBag == null)
                        {
                            return;
                        }

                        var nestedVisitor = new WritePathVisitor(_path, _pathIndex + 1, _value);
                        nestedBag.Accept(nestedVisitor, ref nestedValue);
                        if (nestedVisitor.Succeeded)
                        {
                            property.SetValue(ref updated, nestedValue);
                            Succeeded = true;
                        }
                    }

                    container = updated;
                    return;
                }
            }
        }

        private sealed class EntityReferenceVisitor : IPropertyBagVisitor, IPropertyVisitor
        {
            private readonly Func<Entity, string> _stableIdResolver;
            private readonly IList<EntityReference> _references;
            private readonly List<string> _path = new();
            private readonly HashSet<object> _activeReferences = new HashSet<object>(new ReferenceSet());

            internal EntityReferenceVisitor(Func<Entity, string> stableIdResolver, IList<EntityReference> references)
            {
                _stableIdResolver = stableIdResolver;
                _references = references;
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
                _path.Add(property.Name);
                try
                {
                    if (typeof(TValue) == typeof(Entity))
                    {
                        string targetId = _stableIdResolver((Entity)(object)value);
                        if (!string.IsNullOrEmpty(targetId))
                        {
                            _references.Add(new EntityReference(FormatPath(_path), targetId));
                        }
                    }
                    else if (_path.Count < 24 && value != null)
                    {
                        object activeReference = value;
                        bool trackReference = !typeof(TValue).IsValueType;
                        if (!trackReference || _activeReferences.Add(activeReference))
                        {
                            try
                            {
                                object nestedValue = value;
                                if (PropertyBag.TryGetPropertyBagForValue(ref nestedValue, out IPropertyBag nestedBag))
                                {
                                    nestedBag.Accept(this, ref nestedValue);
                                }
                            }
                            finally
                            {
                                if (trackReference)
                                {
                                    _activeReferences.Remove(activeReference);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    _path.RemoveAt(_path.Count - 1);
                }
            }
        }

        private sealed class ReferenceSet : IEqualityComparer<object>
        {
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
