using UnityEngine;
using UnityEngine.UI;

public static class CanvasExtension
{
	public static float GetExpandScale(this Canvas canvas)
	{
		if (canvas == null) return 1f;
		Canvas root = canvas.rootCanvas;
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		if (scaler == null) return 1f;
		return CalculateExpandScale(root.pixelRect.size, scaler.referenceResolution);
	}

	public static float GetExpandScale(this CanvasScaler canvasScaler)
	{
		if (canvasScaler == null) return 1f;
		Canvas canvas = canvasScaler.GetComponent<Canvas>();
		return canvas != null ? canvas.GetExpandScale() :
			CalculateExpandScale(new Vector2(Screen.width, Screen.height), canvasScaler.referenceResolution);
	}

	private static float CalculateExpandScale(Vector2 screenSize, Vector2 referenceSize)
	{
		if (screenSize.x <= 0f || screenSize.y <= 0f || referenceSize.x <= 0f || referenceSize.y <= 0f)
			return 1f;
		return Mathf.Min(screenSize.x / referenceSize.x, screenSize.y / referenceSize.y);
	}
}

