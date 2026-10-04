using System.Collections;
using UnityEngine;

public class VibrationManager : MonoBehaviour, IGameManager
{
	private const int LIGHT_MILLISECONDS = 15;
	private const int TINY_MILLISECONDS = 2;
	public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;

	public void Startup() => status = ManagerStatus.Started;
	public void PlayLight() => Vibrate(LIGHT_MILLISECONDS);
	public void PlayTiny() => Vibrate(TINY_MILLISECONDS);
	public void PlayContinuousTiny(int count, int interval) => StartCoroutine(PlayContinuous(count, interval, false));
	public void PlayContinuousLight(int count, int interval) => StartCoroutine(PlayContinuous(count, interval, true));

	private IEnumerator PlayContinuous(int count, int interval, bool light)
	{
		for (int i = 0; i < Mathf.Max(0, count); i++)
		{
			if (light) PlayLight(); else PlayTiny();
			if (i + 1 < count) yield return new WaitForSecondsRealtime(Mathf.Max(0, interval) / 1000f);
		}
	}

	[System.Obsolete("Use PlayLight or PlayTiny")]
	public void Vibrate(long milliseconds, int amplitude = -1)
	{
		#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
		if (GameVibrationHelper.Enabled && Application.isMobilePlatform) Handheld.Vibrate();
		#endif
	}

	[System.Obsolete("Use PlayTiny")]
	public void PlayVibration() => PlayTiny();
}
