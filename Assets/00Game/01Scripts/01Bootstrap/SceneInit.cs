using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class SceneInit
{
	public GameAssetsConfig GameplayConfig { get; private set; }
	public BuiltinLevelCatalog LevelCatalog { get; private set; }
	public int NextPlayableLevel { get; private set; }
	public int HighestCompletedLevel { get; private set; }
	public bool EnterGameFromSplash { get; private set; }

	public async UniTask LoadAsync(CancellationToken cancellationToken)
	{
		ResourceRequest configRequest = Resources.LoadAsync<GameAssetsConfig>("GameAssetsConfig");
		ResourceRequest catalogRequest = Resources.LoadAsync<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		await UniTask.WaitUntil(() => configRequest.isDone && catalogRequest.isDone,
			cancellationToken: cancellationToken);
		GameplayConfig = configRequest.asset as GameAssetsConfig;
		LevelCatalog = catalogRequest.asset as BuiltinLevelCatalog;

		if (GameplayConfig == null)
			Debug.LogWarning("[SceneInit] GameAssetsConfig is missing from Resources.");
		if (LevelCatalog == null)
			Debug.LogWarning("[SceneInit] BuiltinLevelCatalog is missing from Resources.");

		NextPlayableLevel = GameProgressStore.NextPlayableLevel;
		HighestCompletedLevel = GameProgressStore.HighestCompletedLevel;
		EnterGameFromSplash = GameProgressStore.CurrentPuzzleIndex == 0;

		if (EnterGameFromSplash)
			await BuiltinPuzzleLoader.PreloadLevelsAsync(NextPlayableLevel, 1, cancellationToken);

		Snui.TextFontCatalog.Resolve();
		SpecialStreakManager.Ensure();
		LevelBoxManager.EnsureInitialized();
		await UniTask.NextFrame(cancellationToken);
		EventRuntime.EnsureInitialized();
		await UniTask.NextFrame(cancellationToken);
		InventoryManager.Ensure();
	}
}
