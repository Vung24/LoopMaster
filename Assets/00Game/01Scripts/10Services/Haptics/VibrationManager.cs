using UnityEngine;

public class VibrationManager : MonoBehaviour, IGameManager
{
	private const int LIGHT_MILLISECONDS = 15;
	private const int TINY_MILLISECONDS = 2;
	public ManagerStatus status { get; private set; } = ManagerStatus.Shutdown;

	public void Startup() => status = ManagerStatus.Started;
	public void PlayLight() => GameVibrationHelper.PlayLight();
	public void PlayTiny() => GameVibrationHelper.PlayTiny();
	public void PlayContinuousTiny(int count, int interval) => GameVibrationHelper.PlayContinuous(count, interval, TINY_MILLISECONDS);
	public void PlayContinuousLight(int count, int interval) => GameVibrationHelper.PlayContinuous(count, interval, LIGHT_MILLISECONDS);

	[System.Obsolete("Use PlayLight or PlayTiny")]
	public void Vibrate(long milliseconds, int amplitude = -1)
	{
		GameVibrationHelper.Vibrate(milliseconds, amplitude);
	}

	[System.Obsolete("Use PlayTiny")]
	public void PlayVibration() => PlayTiny();

	private void OnApplicationPause(bool paused) { if (paused) GameVibrationHelper.Cancel(); }
	private void OnDisable() => GameVibrationHelper.Cancel();
}
