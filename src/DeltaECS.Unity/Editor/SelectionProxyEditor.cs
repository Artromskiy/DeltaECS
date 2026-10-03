using UnityEditor;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    [CustomEditor(typeof(SelectionProxy))]
    internal sealed class SelectionProxyEditor : UnityEditor.Editor
    {
        private SceneEntityInspectorView _view;

        public override bool UseDefaultMargins() => false;

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        private void OnEnable()
        {
            _view?.Dispose();
            _view = new SceneEntityInspectorView(ResolveTarget);
        }

        private EntityInspectorTarget ResolveTarget() => target is SelectionProxy proxy && proxy != null
            ? proxy.Target
            : default;

        private void OnDisable()
        {
            _view?.Dispose();
            _view = null;
        }

        public override VisualElement CreateInspectorGUI() => _view.CreateGUI();
    }
}
