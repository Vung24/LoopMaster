using UnityEngine;

public static class GameVibrationHelper
{
	public static bool Enabled { get; set; } = GamePrefs.GetInt(GamePrefKeys.Vibration, 1) != 0;
	public static void SetEnabled(bool enabled)
	{
		Enabled = enabled;
		GamePrefs.SetInt(GamePrefKeys.Vibration, enabled ? 1 : 0);
		GamePrefs.Save();
	}

	public static void PlayBoxTap(bool canPopupSlice)
	{
		if (canPopupSlice) Vibrate(3, 35);
		else Vibrate(35, 95);
	}

	public static void PlaySlicePopOut() => Vibrate(7, 27);
	public static void PlaySlicePopOutFirst() => Vibrate(10, 80);
	public static void PlaySlicePopIn() => Vibrate(8, 20);

	public static void PlayLockDecrement() => Vibrate(2, 20);
	public static void PlayLockUnlock() => Vibrate(15, 80);

	private static void Vibrate(int milliseconds, int amplitude)
	{
		if (!CanPlayGameVibration()) return;
		#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
		Handheld.Vibrate();
		#endif
	}

	private static bool CanPlayGameVibration() => Enabled && Application.isMobilePlatform;
}
