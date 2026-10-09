using Cysharp.Threading.Tasks;
using UnityEngine;

public class SplashController : MonoBehaviour
{
	[SerializeField]
	private SplashLoadingPanel splashLoadingPanel;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void ConfigureFrameRate()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
	}

	private void Awake()
	{
		if (splashLoadingPanel == null)
		{
			splashLoadingPanel = GetComponentInChildren<SplashLoadingPanel>(true);
		}
	}

	private void Start()
	{
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		StartAsync().Forget();
	}

	private async UniTaskVoid StartAsync()
	{
		await RequestAttPermission();

		int currentLevel = GameProgressStore.NextPlayableLevel;
		if (currentLevel <= 1 && GameProgressStore.HighestCompletedLevel < 1)
		{
			GameSceneManager.EnterGame(GameEnterRoute.Splash, withAnimation: false).Forget();
		}
		else
		{
			GameSceneManager.EnterHome(withAnimation: false).Forget();
		}
	}

	public UniTask RequestAttPermission()
	{
		return UniTask.CompletedTask;
	}
}
