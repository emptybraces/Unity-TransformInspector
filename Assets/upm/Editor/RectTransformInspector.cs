using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Emptybraces.Editor
{
	[CanEditMultipleObjects, CustomEditor(typeof(RectTransform))]
	public class RectTransformInspector : TransformInspectorBase<RectTransform>
	{
		protected override string TypeName => "UnityEditor.RectTransformEditor, UnityEditor";
		MethodInfo _onSceneGUI, _onValidate;
		protected override void OnEnable()
		{
			base.OnEnable();
			_onSceneGUI = _originalEditorType.GetMethod("OnSceneGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			_onValidate = _originalEditorType.GetMethod("OnValidate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		}
		protected override void OnSceneGUI()
		{
			base.OnSceneGUI();
			_onSceneGUI?.Invoke(_originalEditor, null);
		}
		void OnValidate()
		{
			_onValidate?.Invoke(_originalEditor, null);
		}
	}
}
