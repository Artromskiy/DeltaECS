using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


using UnityEditor.IMGUI.Controls;

using UnityEngine;

namespace Delta.ECS.Unity.Editor
{
    internal sealed class ComponentDropdown : AdvancedDropdown
    {
        private readonly ComponentId[] _components;
        private readonly Func<ComponentId, string> _getMenuPath;
        private readonly Action<ComponentId> _add;
        private readonly Dictionary<int, ComponentId> _items = new();

        public ComponentDropdown(ComponentId[] components,
            Func<ComponentId, string> getMenuPath, Action<ComponentId> add)
            : base(new AdvancedDropdownState())
        {
            _components = components;
            _getMenuPath = getMenuPath;
            _add = add;
            minimumSize = new Vector2(230, 320);
            // Unity keeps this bound internal; set it before Show captures the popup bounds.
            typeof(AdvancedDropdown).GetProperty("maximumSize", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(this, new Vector2(4000, 400));
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Component");
            var folders = new Dictionary<string, AdvancedDropdownItem> { [string.Empty] = root };
            int id = 0;

            foreach (var entry in _components
                .Select(component => (Component: component, Path: _getMenuPath(component)))
                .OrderBy(entry => entry.Path, StringComparer.Ordinal))
            {
                string[] path = entry.Path.Split('/');
                string folderPath = string.Empty;
                AdvancedDropdownItem parent = root;
                foreach (string folder in path.Take(path.Length - 1))
                {
                    folderPath += "/" + folder;
                    if (!folders.TryGetValue(folderPath, out AdvancedDropdownItem item))
                    {
                        item = new AdvancedDropdownItem(folder) { id = ++id };
                        folders.Add(folderPath, item);
                        parent.AddChild(item);
                    }

                    parent = item;
                }

                var componentItem = new AdvancedDropdownItem(path[path.Length - 1]) { id = ++id };
                parent.AddChild(componentItem);
                _items.Add(componentItem.id, entry.Component);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (_items.TryGetValue(item.id, out ComponentId componentId))
            {
                _add(componentId);
            }
        }
    }
}
