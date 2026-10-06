using System;
using TMPro;
using UnityEngine;

public static class UiUtils
{
	public static float DESIGN_WIDTH { get; private set; } = 750f;
	public static float DESIGN_HEIGHT { get; private set; } = 1334f;
	public static float DesignAspect => DESIGN_WIDTH / DESIGN_HEIGHT;

	public static bool IsUiRtl()
	{
		return false;
	}

	public static void FixArabic(TMP_Text tmp, bool force = false)
	{
	}

	public static void RevertFixedArabic(TMP_Text tmp)
	{
	}

	public static void SetPivotKeepPlace(RectTransform rectTransform, Vector2 pivot)
	{
		if (rectTransform == null) return;
		Vector2 size = rectTransform.rect.size;
		Vector2 deltaPivot = rectTransform.pivot - pivot;
		Vector3 deltaPosition = new Vector3(deltaPivot.x * size.x, deltaPivot.y * size.y);
		rectTransform.pivot = pivot;
		rectTransform.localPosition -= deltaPosition;
	}
}
