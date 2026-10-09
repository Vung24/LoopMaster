using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class SceneInit
{
	private const float MinimumLoadingTime = 5f;

	public GameAssetsConfig GameplayConfig { get; private set; }
	public BuiltinLevelCatalog LevelCatalog { get; private set; }
	public int NextPlayableLevel { get; private set; }
	public int HighestCompletedLevel { get; private set; }
	public bool EnterGameFromSplash { get; private set; }

	public async UniTask LoadAsync(CancellationToken cancellationToken)
	{
		float loadingStartedAt = Time.realtimeSinceStartup;

		ResourceRequest configRequest = Resources.LoadAsync<GameAssetsConfig>("GameAssetsConfig");
		ResourceRequest catalogRequest = Resources.LoadAsync<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		ResourceRequest uiRequest = Resources.LoadAsync<Snui.PrefabLibrary>("PrefabLibrary");
		await UniTask.WaitUntil(() => configRequest.isDone && catalogRequest.isDone && uiRequest.isDone,
			cancellationToken: cancellationToken);
		GameplayConfig = configRequest.asset as GameAssetsConfig;
		LevelCatalog = catalogRequest.asset as BuiltinLevelCatalog;
		// Retain the loaded library through the scene change; HUD initialization uses it too.
		if (uiRequest.asset != null) _ = Snui.PrefabLibrary.Current;

		if (GameplayConfig == null)
			Debug.LogWarning("[SceneInit] GameAssetsConfig is missing from Resources.");
		if (LevelCatalog == null)
			Debug.LogWarning("[SceneInit] BuiltinLevelCatalog is missing from Resources.");

		NextPlayableLevel = GameProgressStore.NextPlayableLevel;
		HighestCompletedLevel = GameProgressStore.HighestCompletedLevel;
		// Original SplashController routes on the zero-based CurrentPuzzleIndex (RVA 0x17c6308).
		EnterGameFromSplash = GameProgressStore.CurrentPuzzleIndex == 0;

		// Prepare gameplay prefabs during loading instead of first using them in GameScene.Awake.
		if (EnterGameFromSplash)
			await GameLoader.PreloadDefaultAssetsAsync(cancellationToken);

		// These managers otherwise parse their configuration for the first time in GameController.Awake.
		await UniTask.Yield(cancellationToken);
		EventRuntime.EnsureInitialized();
		await UniTask.Yield(cancellationToken);
		InventoryManager.Ensure();

		float remainingTime = MinimumLoadingTime - (Time.realtimeSinceStartup - loadingStartedAt);
		if (remainingTime > 0f)
			await UniTask.WaitForSeconds(remainingTime, ignoreTimeScale: true,
				cancellationToken: cancellationToken);
	}
}
