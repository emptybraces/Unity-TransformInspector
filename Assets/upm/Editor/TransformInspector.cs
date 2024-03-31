using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Emptybraces.Editor
{
	[CanEditMultipleObjects, CustomEditor(typeof(Transform))]
	public class TransformInspector : TransformInspectorBase<Transform>
	{
		protected override string TypeName => "UnityEditor.TransformInspector, UnityEditor";
		protected static bool _isShow;

		public override void OnInspectorGUI()
		{
			_originalEditor.OnInspectorGUI();

			serializedObject.Update();

			_target = (Transform)target;
			_targets.Clear();
			for (int i = 0; i < targets.Length; ++i)
				_targets.Add((Transform)targets[i]);
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
			// 選択しているオブジェクトを接地
			_customActions.Add(
				("Shift to ground", new System.Action(() =>
				{
					Undo.RecordObjects(_targets.ToArray(), "Shift to ground");
					foreach (var child in _targets)
						if (Physics.Raycast(child.position, Vector3.down, out var hit))
							child.position = hit.point;
				}))
			);
			// トルネード上に配置
			_customActions.Add(
				("Positioning/FibonacciSphere", new System.Action(() =>
				{
					Undo.RecordObjects(_targets.ToArray(), "Positioning FibonacciSphere");
					var cp = _GetCenterPosition(_targets);
					var max_distance = _targets.Aggregate(0f, (a, e) => { var d = Vector3.Distance(e.position, cp); return a < d ? d : a; });
					var fib = _GetFibonacciSphere(_targets.Count);
					for (int i = 0; i < _targets.Count; ++i)
						_targets[i].position = cp + fib[i] * max_distance;
					Vector3[] _GetFibonacciSphere(int samples)
					{
						var rnd = 1;
						var points = new Vector3[samples];
						var offset = 2f / samples;
						var increment = Mathf.PI * (3f - Mathf.Sqrt(5f));
						for (int i = 0; i < samples; ++i)
						{
							var y = ((i * offset) - 1) + (offset / 2);
							var r = Mathf.Sqrt(1 - Mathf.Pow(y, 2));
							var phi = ((i + rnd) % samples) * increment;
							points[i].x = Mathf.Cos(phi) * r;
							points[i].y = y;
							points[i].z = Mathf.Sin(phi) * r;
						}
						return points;
					}
				}))
			);
		}
	}
}

