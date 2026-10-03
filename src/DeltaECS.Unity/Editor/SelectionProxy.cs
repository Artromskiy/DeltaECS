using UnityEngine;

namespace Delta.ECS.Unity.Editor
{
    internal sealed class SelectionProxy : ScriptableObject
    {
        internal EntityInspectorTarget Target { get; private set; }

        internal void SetTarget(EntityInspectorTarget target)
        {
            Target = target;
            name = target.Authoring != null
                ? target.Authoring.SceneData.TryGetEntity(target.StableId, out EntityAuthoring authoring)
                    ? authoring.Name
                    : "ECS Entity"
                : $"Entity {target.RuntimeEntity.Index}";
        }
    }
}
