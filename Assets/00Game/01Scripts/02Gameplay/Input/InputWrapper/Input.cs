using UnityEngine;
using System;

namespace InputWrapper
{
	public static class Input
	{
		private static bool touchSupported => UnityEngine.Input.touchSupported && !IsEditorSimulator();
		private static Touch? fakeTouch => MouseSimulator.FakeTouch;

		private static bool IsEditorSimulator() => Application.isEditor && UnityEngine.Input.touchCount == 0;

		public static bool GetKeyDown(KeyCode key) => UnityEngine.Input.GetKeyDown(key);
		public static float GetAxis(string axisName) => UnityEngine.Input.GetAxis(axisName);
		public static int touchCount => touchSupported ? UnityEngine.Input.touchCount : (fakeTouch.HasValue ? 1 : 0);
		public static Touch[] touches
		{
			get
			{
				if (touchSupported) return UnityEngine.Input.touches;
				Touch? touch = fakeTouch;
				return touch.HasValue ? new[] { touch.Value } : Array.Empty<Touch>();
			}
		}

		public static Touch GetTouch(int index)
		{
			if (touchSupported) return UnityEngine.Input.GetTouch(index);
			Touch? touch = fakeTouch;
			if (index != 0 || !touch.HasValue) throw new ArgumentOutOfRangeException(nameof(index));
			return touch.Value;
		}

		private static class MouseSimulator
		{
			private static int _lastFrame = -1;
			private static Vector2 _previousPosition;
			private static Touch? _touch;

			internal static Touch? FakeTouch
			{
				get
				{
					// Cache by frame instead of scaled time so input still updates when paused.
					if (_lastFrame == Time.frameCount) return _touch;
					_lastFrame = Time.frameCount;
					Vector2 position = UnityEngine.Input.mousePosition;
					Vector2 delta = position - _previousPosition;
					_previousPosition = position;
					TouchPhase? phase = null;
					if (UnityEngine.Input.GetMouseButtonDown(0)) { phase = TouchPhase.Began; delta = Vector2.zero; }
					else if (UnityEngine.Input.GetMouseButton(0))
						phase = delta.sqrMagnitude < 0.00001f ? TouchPhase.Stationary : TouchPhase.Moved;
					else if (UnityEngine.Input.GetMouseButtonUp(0)) phase = TouchPhase.Ended;
					_touch = phase.HasValue ? new Touch
					{
						fingerId = 0, type = TouchType.Indirect, phase = phase.Value,
						position = position, rawPosition = position, deltaPosition = delta,
						deltaTime = Time.unscaledDeltaTime, tapCount = 1
					} : (Touch?)null;
					return _touch;
				}
			}
		}
	}
}
