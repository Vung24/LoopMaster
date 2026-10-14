using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class SceneInit
{
	private const float MinimumLoadingTime = 1.2f;

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
		if (uiRequest.asset != null) _ = Snui.PrefabLibrary.Current;

		if (GameplayConfig == null)
			Debug.LogWarning("[SceneInit] GameAssetsConfig is missing from Resources.");
		if (LevelCatalog == null)
			Debug.LogWarning("[SceneInit] BuiltinLevelCatalog is missing from Resources.");

		NextPlayableLevel = GameProgressStore.NextPlayableLevel;
		HighestCompletedLevel = GameProgressStore.HighestCompletedLevel;
		EnterGameFromSplash = GameProgressStore.CurrentPuzzleIndex == 0;

		await GameLoader.PreloadDefaultAssetsAsync(cancellationToken);
		await BuiltinPuzzleLoader.PreloadLevelsAsync(NextPlayableLevel, 3, cancellationToken);

		Snui.TextFontCatalog.Resolve();
		SpecialStreakManager.Ensure();
		LevelBoxManager.EnsureInitialized();
		Addressables.LoadAssetAsync<GameObject>("WinDialog");
		Shader.WarmupAllShaders();

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
