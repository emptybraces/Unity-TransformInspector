using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Emptybraces.Editor
{
	[CanEditMultipleObjects, CustomEditor(typeof(RectTransform))]
	public class RectTransformInspector : TransformInspectorBase<RectTransform>
	{
		protected override string TypeName => "UnityEditor.RectTransformEditor, UnityEditor";
		protected static bool _isShow;
		MethodInfo _onSceneGUI, _onValidate;
		protected override void OnEnable()
		{
			base.OnEnable();
			_onSceneGUI = _originalEditorType.GetMethod("OnSceneGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			_onValidate = _originalEditorType.GetMethod("OnValidate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		}
		void OnValidate() => _onValidate?.Invoke(_originalEditor, null);
		void OnSceneGUI() => _onSceneGUI?.Invoke(_originalEditor, null);
		public override void OnInspectorGUI()
		{
			_originalEditor.OnInspectorGUI();

			serializedObject.Update();

			_target = (RectTransform)target;
			_targets.Clear();
			for (int i = 0; i < targets.Length; ++i)
				_targets.Add((RectTransform)targets[i]);
			for (int i = 0; i < 3; ++i)
				_mixed[i] = false;

			// worlds
			_OnGUIWorldPosition();
			_OnGUIWorldEulerAngles();
			_OnGUILossyScale();

			// options;
			GUI.enabled = true;
			_isShow = EditorGUILayout.BeginFoldoutHeaderGroup(_isShow, "options");
			if (!_isShow)
				return;
			// HideFlags.HideInHierarchy
			GUI.enabled = 1 == _targets.Count && 0 < _target.childCount;
			var current = false;
			if (GUI.enabled)
			{
				foreach (Transform child in _target)
				{
					current = child.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy);
					if (current)
						break;
				}
			}
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				var value = EditorGUILayout.Toggle("HideInHierarchy children", current);
				if (scope.changed)
				{
					foreach (Transform child in _target)
					{
						Undo.RegisterCompleteObjectUndo(child.gameObject, "Update GameObject HideFlags");
						if (value)
							child.gameObject.hideFlags |= HideFlags.HideInHierarchy;
						else
							child.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
					}
					EditorApplication.DirtyHierarchyWindowSorting();
				}
			}
			// HideFlags.DontSaveInBuild
			_OnGUIHideFlags(HideFlags.DontSaveInBuild);
			// HideFlags.NotEditable
			_OnGUIHideFlags(HideFlags.NotEditable);
			// custom actions
			var selected = EditorGUILayout.Popup("Custom Action", -1, _customActionNames);
			if (-1 < selected)
			{
				_customActions[selected].Item2();
			}
		}
		protected override void _AddCustomAction()
		{
		}
	}
}