using UnityEngine;

public static class GroupColorLockConstants
{
	public const int None = 0;
	public const int Yellow = 1;
	public const int Green = 2;
	public const int Pink = 3;
	public const int SkyBlue = 4;
	public const int White = 5;
	public const int MinColorType = 1;
	public const int MaxColorType = 5;

	public static bool IsValidType(int type) => type >= MinColorType && type <= MaxColorType;

	public static Color GetColorByType(int type)
	{
		switch (type)
		{
			case Yellow: return new Color(1f, 0.78f, 0.12f);
			case Green: return new Color(0.25f, 0.78f, 0.35f);
			case Pink: return new Color(1f, 0.35f, 0.62f);
			case SkyBlue: return new Color(0.25f, 0.72f, 1f);
			case White: return Color.white;
			default: return Color.clear;
		}
	}

	public static string GetColorNameByType(int type)
	{
		switch (type)
		{
			case Yellow: return "Yellow";
			case Green: return "Green";
			case Pink: return "Pink";
			case SkyBlue: return "SkyBlue";
			case White: return "White";
			default: return "None";
		}
	}
}
