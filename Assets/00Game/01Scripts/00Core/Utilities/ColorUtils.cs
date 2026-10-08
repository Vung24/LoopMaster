using UnityEngine;

public static class ColorUtils
{
	public static Color FromString(string colorStr)
	{
		Color color = Color.clear;
		ColorUtility.TryParseHtmlString(colorStr, out color);
		return color;
	}

	public static bool FromString(out Color color, string colorStr)
	{
		return ColorUtility.TryParseHtmlString(colorStr, out color);
	}

	public static Color RGB(int rgb)
	{
		return new Color(
			((rgb >> 16) & 0xFF) / 255f,
			((rgb >> 8) & 0xFF) / 255f,
			(rgb & 0xFF) / 255f,
			1f);
	}

	public static Color RGBA(uint rgba)
	{
		return new Color(
			((rgba >> 24) & 0xFF) / 255f,
			((rgba >> 16) & 0xFF) / 255f,
			((rgba >> 8) & 0xFF) / 255f,
			(rgba & 0xFF) / 255f);
	}
}
