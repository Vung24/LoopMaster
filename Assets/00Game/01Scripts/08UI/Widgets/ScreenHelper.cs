using UnityEngine;

public class ScreenHelper : MonoBehaviour
{
	public static float GetCanvasNotchHeight(Canvas canvas) =>
		Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / CanvasScale(canvas);
	public static float GetCanvasSafeBottom(Canvas canvas) =>
		Mathf.Max(0f, Screen.safeArea.yMin) / CanvasScale(canvas);
	private static float CanvasScale(Canvas canvas) => canvas == null ? 1f : Mathf.Max(0.0001f, canvas.rootCanvas.scaleFactor);
}
