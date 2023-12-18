using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Linq;

namespace Emptybraces.Editor
{
	[CanEditMultipleObjects, CustomEditor(typeof(Transform))]
	public class TransformInspector : UnityEditor.Editor
	{
		UnityEditor.Editor _originalInspector;
		static Transform _target;
		static List<Transform> _targets = new List<Transform>();
		static (string, Action)[] _customActions;
		static string[] _customActionNames;
		static bool _isShow;
		void OnEnable()
		{
			_originalInspector = CreateEditor(targets, System.Type.GetType("UnityEditor.TransformInspector, UnityEditor"));
			_customActions ??= new (string, Action)[]
			{
				("Reset HideFlags", () => {
					foreach (var i in _targets)
					{
						Undo.RegisterCompleteObjectUndo(i.gameObject, "Reset HideFlags");
						i.gameObject.hideFlags = HideFlags.None;
					}
				}),
				("Convert scale to positive", () => {
					foreach (var i in _targets)
					{
						Undo.RegisterCompleteObjectUndo(i, "Convert scale to positive");
						i.localScale = new Vector3(Mathf.Abs(i.localScale.x), Mathf.Abs(i.localScale.y), Mathf.Abs(i.localScale.z));
					}
				}),
			};
			_customActionNames ??= _customActions.Select(e => e.Item1).ToArray();
		}
		void OnDisable()
		{
			DestroyImmediate(_originalInspector);
		}
		public override void OnInspectorGUI()
		{
			_originalInspector.OnInspectorGUI();

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

		static string[] _XYZ = new[] { "X", "Y", "Z" };
		static bool[] _mixed = new bool[3];
		void _OnGUIWorldPosition()
		{
			var width_default = EditorGUIUtility.labelWidth;
			foreach (var i in _targets)
				for (int j = 0; j < 3; ++j)
					_mixed[j] |= !Mathf.Approximately(i.position[j], _target.position[j]);
			using (var scope = new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("W Position");
				EditorGUIUtility.labelWidth = 12;//EditorStyles.label.CalcSize(new GUIContent("X")).x;
				for (int i = 0; i < 3; ++i)
				{
					EditorGUI.showMixedValue = _mixed[i];
					var value = EditorGUILayout.FloatField(_XYZ[i], _target.position[i]);
					if (GUI.changed)
					{
						Undo.RegisterCompleteObjectUndo(targets, $"Set Position in {(Selection.count == 1 ? target.name : "Selected Objects")}");
						foreach (var j in _targets)
						{
							var p = j.position;
							p[i] = value;
							j.position = p;
						}
						serializedObject.SetIsDifferentCacheDirty();
						break;
					}
				}
				if (Event.current.type == EventType.ContextClick && scope.rect.Contains(Event.current.mousePosition))
				{
					var context = new GenericMenu();
					context.AddItem(new GUIContent("Copy"), false, () => _WriteVector3(_target.position));
					context.AddItem(new GUIContent("Paste"), false, () => { if (_ParseVector3(out var vec3)) _target.position = vec3; });
					context.ShowAsContext();
					Event.current.Use();
				}
			}
			EditorGUI.showMixedValue = false;
			EditorGUIUtility.labelWidth = width_default;
		}

		void _OnGUIWorldEulerAngles()
		{
			var width_default = EditorGUIUtility.labelWidth;
			foreach (var i in _targets)
				for (int j = 0; j < 3; ++j)
					_mixed[j] |= !Mathf.Approximately(i.eulerAngles[j], _target.eulerAngles[j]);
			using (var scope = new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("W Rotation");
				EditorGUIUtility.labelWidth = 12;

				for (int i = 0; i < 3; ++i)
				{
					EditorGUI.showMixedValue = _mixed[i];
					var value = EditorGUILayout.FloatField(_XYZ[i], _target.eulerAngles[i]);
					if (GUI.changed)
					{
						Undo.RegisterCompleteObjectUndo(targets, $"Set Ratation in {(Selection.count == 1 ? target.name : "Selected Objects")}");
						foreach (var j in _targets)
						{
							var p = j.eulerAngles;
							p[i] = value;
							j.eulerAngles = p;
						}
						serializedObject.SetIsDifferentCacheDirty();
						break;
					}
				}
				if (Event.current.type == EventType.ContextClick && scope.rect.Contains(Event.current.mousePosition))
				{
					var context = new GenericMenu();
					context.AddItem(new GUIContent("Copy"), false, () => _WriteVector3(_target.eulerAngles));
					context.AddItem(new GUIContent("Paste"), false, () => { if (_ParseVector3(out var vec3)) _target.eulerAngles = vec3; });
					context.ShowAsContext();
					Event.current.Use();
				}
			}
			EditorGUI.showMixedValue = false;
			EditorGUIUtility.labelWidth = width_default;
		}

		void _OnGUILossyScale()
		{
			var width_default = EditorGUIUtility.labelWidth;
			foreach (var i in _targets)
				for (int j = 0; j < 3; ++j)
					_mixed[j] |= !Mathf.Approximately(i.lossyScale[j], _target.lossyScale[j]);
			using (var scope = new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("LossyScale");
				EditorGUIUtility.labelWidth = 12;
				using (new EditorGUI.DisabledScope(true))
				{
					for (int i = 0; i < 3; ++i)
					{
						EditorGUI.showMixedValue = _mixed[i];
						EditorGUILayout.FloatField(_XYZ[i], _target.lossyScale[i]);
					}
				}
				if (Event.current.type == EventType.ContextClick && scope.rect.Contains(Event.current.mousePosition))
				{
					var context = new GenericMenu();
					context.AddItem(new GUIContent("Copy"), false, () => _WriteVector3(_target.lossyScale));
					context.ShowAsContext();
					Event.current.Use();
				}
			}
			EditorGUI.showMixedValue = false;
			EditorGUIUtility.labelWidth = width_default;
		}

		void _OnGUIHideFlags(HideFlags hideFlags)
		{
			GUI.enabled = true;
			EditorGUI.showMixedValue = false;
			var current = _target.gameObject.hideFlags.HasFlag(hideFlags);
			foreach (var target in _targets)
				EditorGUI.showMixedValue |= target.gameObject.hideFlags.HasFlag(hideFlags) != current;
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				var value = EditorGUILayout.Toggle(hideFlags.ToString(), current);
				if (scope.changed)
				{
					foreach (var i in _targets)
					{
						Undo.RegisterCompleteObjectUndo(i.gameObject, "Change " + hideFlags);
						if (value)
							i.gameObject.hideFlags |= hideFlags;
						else
							i.gameObject.hideFlags &= ~hideFlags;
					}
				}
			}
		}

		float[] _ParseFloats(string text, string prefix, int count)
		{
			if (string.IsNullOrEmpty(text))
				return null;

			// build a regex that matches "Prefix(a,b,c,...)" at start of text
			var sb = new System.Text.StringBuilder();
			sb.Append('^');
			sb.Append(prefix);
			sb.Append("\\(");
			for (var i = 0; i < count; ++i)
			{
				if (i != 0)
					sb.Append(',');
				sb.Append("([^,]+)");
			}
			sb.Append("\\)");

			var match = Regex.Match(text, sb.ToString());
			if (!match.Success || match.Groups.Count <= count)
				return null;

			var res = new float[count];
			for (var i = 0; i < count; ++i)
			{
				if (float.TryParse(match.Groups[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
					res[i] = f;
				else
					return null;
			}
			return res;
		}
		bool _ParseVector3(out Vector3 res)
		{
			res = Vector3.zero;
			var v = _ParseFloats(EditorGUIUtility.systemCopyBuffer, "Vector3", 3);
			if (v == null)
				return false;
			res = new Vector3(v[0], v[1], v[2]);
			return true;
		}
		void _WriteVector3(Vector3 value)
		{
			EditorGUIUtility.systemCopyBuffer = string.Format(CultureInfo.InvariantCulture, "Vector3({0:g9},{1:g9},{2:g9})", value.x, value.y, value.z);
		}
	}
}