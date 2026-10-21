using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class LevelBuilder
{
	private readonly GameLoader _gameLoader;
	private readonly GameplaySceneBuilder _scene;
	private readonly System.Action _refreshPathState;
	private readonly GameplayCameraFlow _cameraFlow;
	private readonly System.Action _updateCamera;

	public LevelBuilder(
		GameLoader gameLoader,
		GameplaySceneBuilder scene,
		System.Action refreshPathState,
		GameplayCameraFlow cameraFlow,
		System.Action updateCamera)
	{
		_gameLoader = gameLoader;
		_scene = scene;
		_refreshPathState = refreshPathState;
		_cameraFlow = cameraFlow;
		_updateCamera = updateCamera;
	}

	public void BuildSync(GameData data, Transform gameRoot)
	{
		if (data == null || _gameLoader == null) return;
		_gameLoader.LoadAssets(data);
		_gameLoader.PrewarmSlicePool(data);
		_gameLoader.PrewarmEffectPool(10);
		_scene.Build(data, _gameLoader, gameRoot, _refreshPathState);
	}

	public async UniTask BuildAsync(GameData data, Transform gameRoot, CancellationToken token)
	{
		if (data == null || _gameLoader == null) return;
		token.ThrowIfCancellationRequested();
		await UniTask.NextFrame(token);
		await _gameLoader.LoadAssetsAsync(data, token);
		GameVibrationHelper.PrepareForGameplay();
		await _gameLoader.PrewarmSlicePoolAsync(data, token);
		await _scene.BuildAsync(data, _gameLoader, gameRoot, _refreshPathState, token);
		_cameraFlow?.Reset(data, gameRoot);
		_updateCamera?.Invoke();
		await _gameLoader.WarmupSliceVisualsAsync(data, gameRoot, token);
	}

	public LinkedCubeCable SpawnLinkedCable(GameData data, Transform gameRoot)
	{
		if (data == null || data.LinkedPair == null || gameRoot == null || _gameLoader == null)
			return null;

		GameObject cablePrefab = _gameLoader.LinkedCubeCable;
		if (cablePrefab == null)
			throw new System.InvalidOperationException("Missing CoreGamePrefabs/LinkedCubeCable prefab.");

		GameObject cableRoot = UnityEngine.Object.Instantiate(cablePrefab, gameRoot);
		LinkedCubeCable linkedCable = cableRoot.GetComponent<LinkedCubeCable>();
		if (linkedCable == null)
			throw new System.InvalidOperationException("LinkedCubeCable prefab requires its visual component.");

		linkedCable.Initialize(data);
		return linkedCable;
	}
}

