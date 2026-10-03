using UnityEditor.UIElements;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace Delta.ECS.Unity.Editor
{
    internal sealed class ComponentListController : ListViewController
    {
        private readonly SceneEntityInspectorView _view;

        public ComponentListController(SceneEntityInspectorView view) => _view = view;

        public override void Move(int index, int newIndex)
        {
            int componentCount = _view.ComponentCount;
            if (index < 0 || index >= componentCount || componentCount == 0)
            {
                return;
            }

            base.Move(index, Mathf.Clamp(newIndex, 0, componentCount - 1));
            _view.ScheduleRefreshContent();
        }
    }
}
