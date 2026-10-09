using UnityEngine;

namespace Snui
{
	[ExecuteAlways]
	[RequireComponent(typeof(RectTransform))]
	public sealed class SafeArea : MonoBehaviour
	{
		private RectTransform _rectTransform;
		private Rect _lastSafeArea;
		private Vector2Int _lastResolution;

		private void Awake()
		{
			_rectTransform = GetComponent<RectTransform>();
			ApplySafeArea();
		}

		private void Update()
		{
			Vector2Int resolution = new Vector2Int(Screen.width, Screen.height);
			if (Screen.safeArea != _lastSafeArea || resolution != _lastResolution)
				ApplySafeArea();
		}

		private void ApplySafeArea()
		{
			if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
			if (_rectTransform == null || Screen.width <= 0 || Screen.height <= 0) return;

			Rect safeArea = Screen.safeArea;
			if (safeArea.width <= 0f || safeArea.height <= 0f)
				safeArea = new Rect(0f, 0f, Screen.width, Screen.height);

			Vector2 min = safeArea.position;
			Vector2 max = safeArea.position + safeArea.size;
			min.x /= Screen.width;
			min.y /= Screen.height;
			max.x /= Screen.width;
			max.y /= Screen.height;
			_rectTransform.anchorMin = min;
			_rectTransform.anchorMax = max;
			_rectTransform.offsetMin = Vector2.zero;
			_rectTransform.offsetMax = Vector2.zero;

			_lastSafeArea = safeArea;
			_lastResolution = new Vector2Int(Screen.width, Screen.height);
		}
	}
}
