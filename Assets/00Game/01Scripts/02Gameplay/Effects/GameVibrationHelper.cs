using UnityEngine;

public static class GameVibrationHelper
{
	public static bool Enabled { get; set; } = PlayerPrefs.GetInt("game_vibration_enabled", 1) != 0;
	public static void SetEnabled(bool enabled)
	{
		Enabled = enabled;
		PlayerPrefs.SetInt("game_vibration_enabled", enabled ? 1 : 0);
		PlayerPrefs.Save();
	}

	public static void PlayBoxTap(bool canPopupSlice)
	{
		if (canPopupSlice) Vibrate(3, 35);
		else Vibrate(35, 95);
	}

	public static void PlaySlicePopOut() => Vibrate(7, 27);
	public static void PlaySlicePopOutFirst() => Vibrate(10, 80);
	public static void PlaySlicePopIn() => Vibrate(8, 20);

	// The offline haptic backend maps native Tiny/Light requests to Handheld.Vibrate.
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
