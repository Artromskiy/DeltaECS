using Delta.ECS;

namespace Delta.ECS.Unity.Editor
{
    internal readonly struct EntityInspectorTarget
    {
        internal readonly SceneAuthoring Authoring;
        internal readonly string StableId;
        internal readonly SceneWorld RuntimeWorld;
        internal readonly Entity RuntimeEntity;

        internal EntityInspectorTarget(
            SceneAuthoring authoring,
            string stableId,
            SceneWorld runtimeWorld = null,
            Entity runtimeEntity = default)
        {
            Authoring = authoring;
            StableId = stableId;
            RuntimeWorld = runtimeWorld;
            RuntimeEntity = runtimeEntity;
        }

        internal bool IsRuntime => RuntimeWorld != null && RuntimeEntity.IsValid;
    }
}
