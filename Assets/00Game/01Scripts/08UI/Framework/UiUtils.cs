using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

	public static Image CreateMask(Transform parent)
	{
		if (parent == null) return null;
		var go = new GameObject("TutorialMask", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		var rect = go.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.sizeDelta = Vector2.zero;
		rect.anchoredPosition = Vector2.zero;
		var img = go.GetComponent<Image>();
		img.color = new Color(0f, 0f, 0f, 0f);
		img.raycastTarget = true;
		return img;
	}

	public static Image CreateMask(Canvas canvas)
	{
		return CreateMask(canvas != null ? canvas.transform : null);
	}
}
