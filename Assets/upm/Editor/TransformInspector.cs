using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Emptybraces.Editor
{
	[CanEditMultipleObjects, CustomEditor(typeof(Transform))]
	public class TransformInspector : TransformInspectorBase<Transform>
	{
		protected override string TypeName => "UnityEditor.TransformInspector, UnityEditor";
		protected override void _AddCustomAction()
		{
			// 選択しているオブジェクトを接地
			_customActions.Add(
				("Shift to ground", new System.Action(() =>
				{
					bool success = false;
					Undo.RecordObjects(_targets.ToArray(), "Shift to ground");
					foreach (var child in _targets)
						if (Physics.Raycast(child.position, Vector3.down, out var hit))
						{
							success = true;
							child.position = hit.point;
						}
					if (!success)
						Debug.LogWarning("No ground.");
				}))
			);
			// トルネード上に配置
			_customActions.Add(
				("Positioning/FibonacciSphere", new System.Action(() =>
				{
					if (_targets.Count == 1)
					{
						Debug.LogWarning("Must selected objects least 2.");
						return;
					}
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

