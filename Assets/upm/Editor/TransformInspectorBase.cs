using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System;
using System.Linq;
using System.Reflection;

namespace Emptybraces.Editor
{
	public abstract class TransformInspectorBase<T> : UnityEditor.Editor where T : Transform
	{
		protected UnityEditor.Editor _originalEditor;
		protected Type _originalEditorType;
		protected static T _target;
		protected static List<T> _targets = new List<T>();
		protected static List<(string, Action)> _customActions;
		protected static string[] _customActionNames;
		protected abstract string TypeName { get; }
		static protected string[] _XYZ = new[] { "X", "Y", "Z" };
		static protected bool[] _mixed = new bool[3];
		protected static bool _isShow;
		protected static bool _isShowDistanceOnSceneView;
		protected virtual void OnSceneGUI()
		{
			if (!_isShowDistanceOnSceneView)
				return;
			var t = (Transform)target;
			if (Selection.transforms[0] != t)
				return;
			if (5 < Selection.transforms.Length)
				return;
			var c = __Comb(Selection.transforms.Length, 2);
			for (int i = 0, cnt = 0, l = Selection.transforms.Length; i < l; ++i)
			{
				var from = Selection.transforms[i];
				for (int j = i + 1; j < l; ++j,++cnt)
				{
					Handles.color = GUI.color = Color.HSVToRGB(cnt / (float)c, 1, .8f);
					var to = Selection.transforms[j];
					Handles.DrawLine(from.position, to.position);
					Handles.Label((to.position + from.position) / 2, Vector3.Distance(to.position, from.position).ToString());
				}
			}

			int __Perm(int n, int p)
			{
				var r = n;
				--n;
				--p;
				for (; 1 < n && 0 < p; --n, --p)
					r *= n;
				return r;
			}
			int __Comb(int n, int c)
			{
				var d = __Perm(c, c);
				if (d != 0)
					return __Perm(n, c) / d;
				return 0;
			}
		}
		void Awake()
		{
			_InitIfNeeded();
			_originalEditorType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(_originalEditor, null);
		}
		protected virtual void OnEnable()
		{
			_InitIfNeeded();
			_MakeCustomActionIfNeeded();
		}
		protected virtual void OnDisable()
		{
			_originalEditorType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(_originalEditor, null);
			DestroyImmediate(_originalEditor);
		}

		public override void OnInspectorGUI()
		{
			_originalEditor.OnInspectorGUI();

			serializedObject.Update();

			_target = (T)target;
			_targets.Clear();
			for (int i = 0; i < targets.Length; ++i)
				_targets.Add((T)targets[i]);
			for (int i = 0; i < 3; ++i)
				_mixed[i] = false;

			// worlds
			_OnGUIWorldPosition();
			_OnGUIWorldEulerAngles();
			_OnGUILossyScale();
			// distance
			if (Selection.transforms.Length == 2)
			{
				GUI.enabled = false;
				using (var scope = new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.PrefixLabel("Distance");
					EditorGUIUtility.labelWidth = 22;
					EditorGUILayout.FloatField("", Vector3.Distance(Selection.transforms[1].position, Selection.transforms[0].position));
					EditorGUILayout.FloatField("Sqr", Vector3.SqrMagnitude(Selection.transforms[1].position - Selection.transforms[0].position));
				}
			}
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
			// distsance on sceneview
			_isShowDistanceOnSceneView = EditorGUILayout.Toggle("Show Distance on SceneView", _isShowDistanceOnSceneView);
			// custom actions
			var selected = EditorGUILayout.Popup("Custom Action", -1, _customActionNames);
			if (-1 < selected)
			{
				_customActions[selected].Item2();
			}
		}

		void _InitIfNeeded()
		{
			if (_originalEditor != null)
				return;
			_originalEditorType = Type.GetType(TypeName);
			_originalEditor = CreateEditor(targets, _originalEditorType);
		}

		protected void _OnGUIWorldPosition()
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
					using var scope2 = new EditorGUI.ChangeCheckScope();
					var value = EditorGUILayout.FloatField(_XYZ[i], _target.position[i]);
					if (scope2.changed)
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

		protected void _OnGUIWorldEulerAngles()
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
					using var scope2 = new EditorGUI.ChangeCheckScope();
					var value = EditorGUILayout.FloatField(_XYZ[i], _target.eulerAngles[i]);
					if (scope2.changed)
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

		protected void _OnGUILossyScale()
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

		protected void _OnGUIHideFlags(HideFlags hideFlags)
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
					using (new UndoGroupScope("Change " + hideFlags))
					{
						foreach (var i in _targets)
						{
							Undo.RegisterFullObjectHierarchyUndo(i, "");
							if (value)
								i.gameObject.hideFlags |= hideFlags;
							else
								i.gameObject.hideFlags &= ~hideFlags;
						}
					}
					EditorApplication.RepaintHierarchyWindow();
					EditorApplication.QueuePlayerLoopUpdate();
				}
			}
		}

		void _MakeCustomActionIfNeeded()
		{
			if (_customActions == null)
			{
				_customActions = new List<(string, Action)>
				{
					// 選択しているオブジェクトのHideFalgsリセット
					("Reset HideFlags", () => {
						using (new UndoGroupScope("Reset HideFlags"))
						{
							foreach (var i in _targets)
							{
								Undo.RegisterFullObjectHierarchyUndo(i, "Reset HideFlags");
								i.gameObject.hideFlags = HideFlags.None;
							}
						}
						EditorApplication.RepaintHierarchyWindow();
					}),
					// 選択しているオブジェクトのスケールのプラス化
					("Convert scale to positive", () => {
						using (new UndoGroupScope("Convert scale to positive"))
						{
							foreach (var i in _targets)
							{
								Undo.RegisterFullObjectHierarchyUndo(i, "");
								i.localScale = new Vector3(Mathf.Abs(i.localScale.x), Mathf.Abs(i.localScale.y), Mathf.Abs(i.localScale.z));
							}
						}
					}),
					// 選択しているオブジェクトの親トランスフォームのリセット（子はそのまま）
					("Reset transform and children stays", () => {
						if (1 != _targets.Count) {
							Debug.LogError("Reset transform and children stays: Abort, can't be execute when selected multiple.");
							return;
						}
						if (0 == _target.childCount) {
							Debug.LogError("Reset transform and children stays: Abort, no Children.");
							return;
						}
						using (new UndoGroupScope("Reset transform and children stays"))
						{
							var children = _GetChildren(_target);
							foreach (var child in children)
								Undo.SetTransformParent(child, null, "SetParent Null");
							_target.position = Vector3.zero;
							_target.rotation = Quaternion.identity;
							_target.localScale = Vector3.one;
							foreach (var child in children)
								Undo.SetTransformParent(child, _target, "SetParent Target");
						}
					}),
					// 選択しているオブジェクトの子の中心位置へ移動
					("Centering position based on children", () => {
						if (1 != _targets.Count) {
							Debug.LogError("Centering position based on children: Abort, can't be execute when selected multiple.");
							return;
						}
						if (0 == _target.childCount) {
							Debug.LogError("Centering position based on children: Abort, no Children.");
							return;
						}
						using (new UndoGroupScope("Centering position based on children"))
						{
							var children = _GetChildren(_target);
							Undo.RegisterFullObjectHierarchyUndo(_target , "");
							var cp = _GetCenterPosition(children);
							var ofs = _target.position - cp;
							_target.position = cp;
							foreach (var child in children) {
								Undo.RegisterFullObjectHierarchyUndo(child, "");
								child.position = child.position + ofs;
							}
						}
					}),
					// 選択しているオブジェクトの親の位置を同期
					("Align parent position to this", () => {
						if (_target.parent == null) {
							Debug.LogError("Align parent position to this: Abort, no parent.");
							return;
						}
						using (new UndoGroupScope("Align parent position to this"))
						{
							var parent = _target.parent;
							var other_children = _GetChildren(parent).Where(e => e != _target).ToArray();
							Undo.RegisterFullObjectHierarchyUndo(_target, "");
							Undo.RegisterFullObjectHierarchyUndo(parent, "");
							var ofs = parent.position - _target.position;
							parent.position = _target.position;
							_target.localPosition = Vector3.zero;
							foreach (var child in other_children) {
								Undo.RegisterFullObjectHierarchyUndo(child, "");
								child.position = child.position + ofs;
							}
						}
					}),
					// ランダムセット
					("Random Set/Position(Local)", () => {
						Undo.RecordObjects(_targets.ToArray(), "Random Set");
						foreach (var child in _targets)
							child.localPosition = UnityEngine.Random.insideUnitSphere * Mathf.Max(Mathf.Abs(child.localPosition.x), Mathf.Abs(child.localPosition.y), Mathf.Abs(child.localPosition.z));
					}),
					("Random Set/Position(World)", () => {
						Undo.RecordObjects(_targets.ToArray(), "Random Set");
						foreach (var child in _targets)
							child.position = UnityEngine.Random.insideUnitSphere * Mathf.Max(Mathf.Abs(child.position.x), Mathf.Abs(child.position.y), Mathf.Abs(child.position.z));
					}),
					("Random Set/Rotation", () => {
						Undo.RecordObjects(_targets.ToArray(), "Random Set");
						foreach (var child in _targets)
							child.rotation = UnityEngine.Random.rotationUniform;
					}),
					("Random Set/Scale", () => {
						Undo.RecordObjects(_targets.ToArray(), "Random Set");
						foreach (var child in _targets) {
							var r = UnityEngine.Random.insideUnitSphere * Mathf.Max(Mathf.Abs(child.localScale.x), Mathf.Abs(child.localScale.y), Mathf.Abs(child.localScale.z));
							child.localScale = new Vector3(Mathf.Abs(r.x), Mathf.Abs(r.y), Mathf.Abs(r.z));
						}
					}),
					("Random Set/All", () => {
						Undo.RecordObjects(_targets.ToArray(), "Random Set");
						foreach (var child in _targets) {
							child.localPosition = UnityEngine.Random.insideUnitSphere * Mathf.Max(Mathf.Abs(child.localPosition.x), Mathf.Abs(child.localPosition.y), Mathf.Abs(child.localPosition.z));
							child.rotation = UnityEngine.Random.rotationUniform;
							var r = UnityEngine.Random.insideUnitSphere * Mathf.Max(Mathf.Abs(child.localScale.x), Mathf.Abs(child.localScale.y), Mathf.Abs(child.localScale.z));
							child.localScale = new Vector3(Mathf.Abs(r.x), Mathf.Abs(r.y), Mathf.Abs(r.z));
						}
					}),
					("Naming/Add suffix number", () => {
						var regex = new Regex(@"\([0-9]+\)$");
						int i = 1;
						using (new UndoGroupScope("Naming/Add suffix number"))
						{
							foreach (var g in _targets.OrderBy(e => e.transform.GetSiblingIndex()))
							{
								Undo.RegisterFullObjectHierarchyUndo(g, "");
								if (regex.IsMatch(g.name))
									g.name = regex.Replace(g.name, $"({i++})");
								else
									g.name = g.name + $" ({i++})";
							}
						}
					}),
					("Naming/Rename top sibling obj name with suffix number", () => {
						if (1 == _targets.Count) {
							Debug.LogError("Cannot be done where only one is selected.");
							return;
						}
						using (new UndoGroupScope("Naming/Rename top sibling obj name with suffix number"))
						{
							var ordered = _targets.OrderBy(e => e.transform.GetSiblingIndex()).ToArray();
							var name = Regex.Replace(ordered[0].name, @"\([0-9]+\)$", "").TrimEnd();
							int i = 1;
							foreach (var g in ordered) {
								Undo.RegisterFullObjectHierarchyUndo(g, "");
								g.name = name + $" ({i++})";
							}
						}
					})
				};
				_AddCustomAction();
			}
			_customActionNames ??= _customActions.Select(e => e.Item1).ToArray();
		}

		struct UndoGroupScope : IDisposable
		{
			int group;
			public UndoGroupScope(string groupName)
			{
				Undo.SetCurrentGroupName(groupName);
				group = Undo.GetCurrentGroup();
			}
			public void Dispose()
			{
				Undo.CollapseUndoOperations(group);
			}
		}

		protected virtual void _AddCustomAction()
		{

		}

		protected Transform[] _GetChildren(Transform parent)
		{
			var children = new Transform[parent.childCount];
			for (int i = 0; i < parent.childCount; ++i)
				children[i] = parent.GetChild(i);
			return children;
		}

		protected Vector3 _GetCenterPosition(IEnumerable<Transform> ts)
		{
			if (ts == null || ts.Count() == 0)
				return Vector3.zero;
			if (ts.Count() == 1)
				return ts.ElementAt(0).position;
			var min = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
			var max = new Vector3(-Mathf.Infinity, -Mathf.Infinity, -Mathf.Infinity);
			foreach (var tr in ts)
			{
				if (tr.position.x < min.x) min.x = tr.position.x;
				if (tr.position.y < min.y) min.y = tr.position.y;
				if (tr.position.z < min.z) min.z = tr.position.z;
				if (tr.position.x > max.x) max.x = tr.position.x;
				if (tr.position.y > max.y) max.y = tr.position.y;
				if (tr.position.z > max.z) max.z = tr.position.z;
			}
			return new Vector3((min.x + max.x) / 2.0f, (min.y + max.y) / 2.0f, (min.z + max.z) / 2.0f);
		}

		protected float[] _ParseFloats(string text, string prefix, int count)
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
		protected bool _ParseVector3(out Vector3 res)
		{
			res = Vector3.zero;
			var v = _ParseFloats(EditorGUIUtility.systemCopyBuffer, "Vector3", 3);
			if (v == null)
				return false;
			res = new Vector3(v[0], v[1], v[2]);
			return true;
		}
		protected void _WriteVector3(Vector3 value)
		{
			EditorGUIUtility.systemCopyBuffer = string.Format(CultureInfo.InvariantCulture, "Vector3({0:g9},{1:g9},{2:g9})", value.x, value.y, value.z);
		}
	}
}
