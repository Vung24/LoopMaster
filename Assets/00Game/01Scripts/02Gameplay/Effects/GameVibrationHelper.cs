using UnityEngine;

public static class GameVibrationHelper
{
	#if UNITY_IOS && !UNITY_EDITOR
	[System.Runtime.InteropServices.DllImport("__Internal")]
	private static extern void LoopMasterNavigationHaptic();
	#endif

	public static bool Enabled { get; set; } = GamePrefs.GetInt(GamePrefKeys.Vibration,
		GamePrefs.GetInt(GamePrefKeys.UserVibration, 1)) != 0;
	public static void SetEnabled(bool enabled)
	{
		Enabled = enabled;
		GamePrefs.SetInt(GamePrefKeys.Vibration, enabled ? 1 : 0);
		GamePrefs.SetInt(GamePrefKeys.UserVibration, enabled ? 1 : 0);
		if (!enabled) Cancel();
		GamePrefs.Save();
	}

	public static void PlayBoxTap(bool canPopupSlice)
	{
		if (canPopupSlice) Vibrate(3, 35);
		else Vibrate(35, 95);
	}

	public static void PrepareForGameplay()
	{
		if (!CanPlayGameVibration()) return;
		#if UNITY_ANDROID && !UNITY_EDITOR
		try { InitializeAndroid(); }
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
		#endif
	}

	public static void PlaySlicePopOut() => Vibrate(7, 27);
	public static void PlaySlicePopOutFirst() => Vibrate(10, 80);
	public static void PlaySlicePopIn() => Vibrate(8, 20);

	public static void PlayLockDecrement() => PlayTiny();
	public static void PlayLockUnlock() => PlayLight();

	public static void PlayNavigationTap()
	{
		if (!CanPlayGameVibration()) return;
		#if UNITY_ANDROID && !UNITY_EDITOR
		using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
		using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
		{
			activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
			{
				if (!Enabled) return;
				using (var currentPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
				using (var currentActivity = currentPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
				using (var window = currentActivity.Call<AndroidJavaObject>("getWindow"))
				using (var view = window.Call<AndroidJavaObject>("getDecorView"))
				{
					// CLOCK_TICK: short system haptic; respects the device's touch feedback setting.
					view.Call<bool>("performHapticFeedback", 4);
				}
			}));
		}
		#elif UNITY_IOS && !UNITY_EDITOR
		LoopMasterNavigationHaptic();
		#endif
	}

	public static void PlayTiny() => Vibrate(2, -1);
	public static void PlayLight() => Vibrate(15, -1);

	public static void PlayContinuous(int count, int interval, int milliseconds)
	{
		if (!CanPlayGameVibration() || count <= 0 || milliseconds <= 0) return;
		#if UNITY_ANDROID && !UNITY_EDITOR
		try
		{
			if (!InitializeAndroid()) return;
			// Native pattern: initial delay, pulse, gap, pulse ...; no trailing gap.
			var pattern = new long[checked(count * 2)];
			for (int i = 1; i < pattern.Length; i++)
				pattern[i] = (i & 1) != 0 ? milliseconds : Mathf.Max(0, interval);
			int version = System.Threading.Volatile.Read(ref _androidVibrationVersion);
			_androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => PlayAndroidPattern(pattern, version)));
		}
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
		#endif
	}

	public static void Vibrate(long milliseconds, int amplitude = -1)
	{
		if (!CanPlayGameVibration() || milliseconds <= 0) return;
		#if UNITY_ANDROID && !UNITY_EDITOR
		try
		{
			if (!InitializeAndroid()) return;
			int safeAmplitude = amplitude == -1 ? -1 : Mathf.Clamp(amplitude, 1, 255);
			int version = System.Threading.Volatile.Read(ref _androidVibrationVersion);
			// Android's vibrator service can block on IPC. Queue it off the Unity frame.
			_androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => PlayAndroidPulse(milliseconds, safeAmplitude, version)));
		}
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
		#elif UNITY_IOS && !UNITY_EDITOR
		LoopMasterNavigationHaptic();
		#endif
	}

	public static void Cancel()
	{
		#if UNITY_ANDROID && !UNITY_EDITOR
		System.Threading.Interlocked.Increment(ref _androidVibrationVersion);
		try { if (_androidActivity != null) _androidActivity.Call("runOnUiThread", new AndroidJavaRunnable(CancelAndroidVibration)); }
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
		#endif
	}

	#if UNITY_ANDROID && !UNITY_EDITOR
	private static AndroidJavaObject _vibrator;
	private static AndroidJavaObject _androidActivity;
	private static AndroidJavaClass _vibrationEffect;
	private static int _androidVersion;
	private static int _androidVibrationVersion;
	private static bool _androidInitialized;
	private static volatile bool _androidFailed;

	private static bool CanPlayAndroidRequest(int version)
	{
		return Enabled && !_androidFailed && version == System.Threading.Volatile.Read(ref _androidVibrationVersion);
	}

	private static void PlayAndroidPulse(long milliseconds, int amplitude, int version)
	{
		if (!CanPlayAndroidRequest(version)) return;
		try
		{
			if (_androidVersion >= 26)
			{
				using (var effect = _vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
					_vibrator.Call("vibrate", effect);
			}
			else _vibrator.Call("vibrate", milliseconds);
		}
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
	}

	private static void PlayAndroidPattern(long[] pattern, int version)
	{
		if (!CanPlayAndroidRequest(version)) return;
		try
		{
			if (_androidVersion >= 26)
			{
				using (var effect = _vibrationEffect.CallStatic<AndroidJavaObject>("createWaveform", pattern, -1))
					_vibrator.Call("vibrate", effect);
			}
			else _vibrator.Call("vibrate", pattern, -1);
		}
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
	}

	private static void CancelAndroidVibration()
	{
		try { if (_vibrator != null) _vibrator.Call("cancel"); }
		catch (AndroidJavaException exception) { ReportAndroidFailure(exception); }
	}

	private static bool InitializeAndroid()
	{
		if (_androidFailed) return false;
		if (_androidInitialized) return _vibrator != null;
		using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
		using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
		using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
		{
			if (activity == null) return false;
			_androidActivity = player.GetStatic<AndroidJavaObject>("currentActivity");
			_androidVersion = version.GetStatic<int>("SDK_INT");
			_vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
			if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator"))
			{
				_vibrator.Dispose();
				_vibrator = null;
			}
			if (_vibrator != null && _androidVersion >= 26)
				_vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
			_androidInitialized = true;
			return _vibrator != null;
		}
	}

	private static void ReportAndroidFailure(AndroidJavaException exception)
	{
		if (!_androidFailed) Debug.LogWarning("Vibrate failed: " + exception.Message);
		_androidFailed = true;
	}
	#endif

	private static bool CanPlayGameVibration() => Enabled && Application.isMobilePlatform;
}
