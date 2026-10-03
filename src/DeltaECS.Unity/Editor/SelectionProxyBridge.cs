using System.Collections.Generic;
using Unity.Hierarchy;
using UnityEditor;
using UnityEngine;

namespace Delta.ECS.Unity.Editor
{
    [InitializeOnLoad]
    internal static class SelectionProxyBridge
    {
        private static readonly Dictionary<(HierarchyNodeHandler Handler, HierarchyNode Node), SelectionProxy> Proxies = new();

        static SelectionProxyBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
        }

        internal static EntityId GetEntityId(HierarchyNodeHandler handler, HierarchyNode node, EntityInspectorTarget target)
        {
            var key = (handler, node);
            if (!Proxies.TryGetValue(key, out var proxy))
            {
                proxy = ScriptableObject.CreateInstance<SelectionProxy>();
                proxy.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                Proxies.Add(key, proxy);
            }
            proxy.SetTarget(target);
            return proxy.GetEntityId();
        }

        internal static HierarchyNode GetNode(HierarchyNodeHandler handler, EntityId entityId)
        {
            foreach (var pair in Proxies)
                if (pair.Key.Handler == handler && pair.Value.GetEntityId() == entityId)
                    return pair.Key.Node;
            return HierarchyNode.Null;
        }

        internal static void UpdateTarget(HierarchyNodeHandler handler, HierarchyNode node, EntityInspectorTarget target)
        {
            if (Proxies.TryGetValue((handler, node), out var proxy))
                proxy.SetTarget(target);
        }

        internal static void Select(HierarchyNodeHandler handler, HierarchyNode node, EntityInspectorTarget target)
        {
            GetEntityId(handler, node, target);
            var proxy = Proxies[(handler, node)];
            if (Selection.activeObject != proxy) Selection.activeObject = proxy;
        }

        internal static void Remove(HierarchyNodeHandler handler, HierarchyNode node)
        {
            if (Proxies.Remove((handler, node), out var proxy))
                Object.DestroyImmediate(proxy);
        }

        private static void DisposeAll()
        {
            foreach (var proxy in Proxies.Values) Object.DestroyImmediate(proxy);
            Proxies.Clear();
        }
    }
}
