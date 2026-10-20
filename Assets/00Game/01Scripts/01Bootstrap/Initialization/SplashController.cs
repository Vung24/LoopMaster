using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class SplashController : MonoBehaviour
{
	[SerializeField]
	private SplashLoadingPanel splashLoadingPanel;

	public SceneInit InitData { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void ConfigureFrameRate()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
		DOTween.SetTweensCapacity(500, 100);
		DOTween.defaultRecyclable = true;
#if !UNITY_EDITOR
		DOTween.logBehaviour = LogBehaviour.ErrorsOnly;
#endif
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
		if (splashLoadingPanel != null)
			await splashLoadingPanel.Show();

		InitData = new SceneInit();
		await InitData.LoadAsync(this.GetCancellationTokenOnDestroy());

		if (InitData.EnterGameFromSplash)
		{
			GameSceneManager.EnterGame(GameEnterRoute.Splash, withAnimation: false).Forget();
		}
		else
		{
			GameSceneManager.EnterHome(withAnimation: false).Forget();
		}
	}
}
