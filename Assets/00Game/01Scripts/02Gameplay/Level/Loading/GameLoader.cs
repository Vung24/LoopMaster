using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameLoader : MonoBehaviour
{
	private const string ResourceRoot = "CoreGamePrefabs/";
	private static readonly Dictionary<string, GameObject> PreloadedPrefabs = new Dictionary<string, GameObject>();
	private GameAssetsConfig _config;
	private readonly Dictionary<string, Stack<PooledGameObject>> _gameObjectPools =
		new Dictionary<string, Stack<PooledGameObject>>();

	public GameObject Box => LoadPrefab("Box");
	public GameObject BoxEnterOnly => LoadPrefab("OnlyEnterBox");
	public GameObject LimitedColorBox => LoadPrefab("LimitedColorBox");
	public GameObject SingleTypeBox => LoadPrefab("SingleTypeBox");
	public GameObject LongBox => LoadPrefab("LongColorBox");
	public GameObject BoxLock => LoadPrefab("BoxColorLock");
	public GameObject AdjacentLock => LoadPrefab("BoxAdjacentLock");
	public GameObject NumLock => LoadPrefab("BoxNumLock");
	public GameObject CubeNumLock => LoadPrefab("CubeNumLock");
	public GameObject Slice => LoadPrefab("Slice");
	public GameObject Cube => LoadPrefab("Cube");
	public GameObject LinkedCubeCable => LoadPrefab("LinkedCubeCable");
	public GameObject FinishCube => LoadPrefab("CubeFinish");
	public GameObject FinishLongCube => LoadPrefab("CubeLongFinish");
	public GameObject GameRoad => LoadPrefab("GameRoad");
	public GameObject RoadArrow => LoadPrefab("BoxArrow");
	public GameObject FlyPath => LoadPrefab("FlyAddPath");
	public GameObject GroupColorLock => LoadPrefab("KeyLockBox");
	public GameObject GroupKey => LoadPrefab("Key");
	public GameObject NewPlayerTutorialPrefab { get; private set; }
	public GameObject FailTutorialPrefab { get; private set; }
	public GameObject ToolPathBoxEffect { get; private set; }
	public GameObject ToolShuffleMask { get; private set; }
	public GameObject BoxFinishEffect { get; private set; }
	public GameObject BoxNormalEffect { get; private set; }
	public GameObject BoxComboEffect => LoadPrefab("BoxComboEffect");
	public GameObject PathComboEffect => LoadPrefab("PathCombo");

	public AudioClip SlicePopIn { get; private set; }
	public AudioClip SlicePopOut { get; private set; }
	public float SlicePopOutPitch { get; private set; } = 1f;
	public AudioClip MatchClip { get; private set; }
	public AudioClip AccClip { get; private set; }
	public AudioClip DecClip { get; private set; }
	public AudioClip AccShortClip { get; private set; }
	public AudioClip UndoClip { get; private set; }
	public AudioClip ShuffleClip { get; private set; }
	public AudioClip PathClip { get; private set; }
	public AudioClip StartShakeClip { get; private set; }
	public AudioClip Fail { get; private set; }
	public AudioClip FailShow { get; private set; }
	public AudioClip OnlyEnterRockBlast { get; private set; }
	public AudioClip ColorLockOpen { get; private set; }

	public Sprite SpeedSprite => _config != null ? _config.SpeedImage : (GameAssetsConfig.Instance != null ? GameAssetsConfig.Instance.SpeedImage : null);
	public Sprite UnSpeedSprite => _config != null ? _config.UnSpeedImage : (GameAssetsConfig.Instance != null ? GameAssetsConfig.Instance.UnSpeedImage : null);

	public void LoadAssets(GameData gameData)
	{
		foreach (string name in GameplayAssetPreloadRules.GetRequiredPrefabNames(gameData)) LoadPrefab(name);
		ConfigureAssets(gameData);
	}

	public async UniTask LoadAssetsAsync(GameData gameData, CancellationToken cancellationToken = default)
	{
		foreach (string name in GameplayAssetPreloadRules.GetRequiredPrefabNames(gameData))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (PreloadedPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null) continue;
			ResourceRequest request = Resources.LoadAsync<GameObject>(ResourceRoot + name);
			await request.WithCancellation(cancellationToken);
			if (request.asset is GameObject loaded) PreloadedPrefabs[name] = loaded;
		}
		ResourceRequest uiRequest = Resources.LoadAsync<Snui.PrefabLibrary>("PrefabLibrary");
		await uiRequest.WithCancellation(cancellationToken);
		ConfigureAssets(gameData);
		await PreloadInteractionAudioAsync(cancellationToken);
	}

	private async UniTask PreloadInteractionAudioAsync(CancellationToken cancellationToken)
	{
		// Loading an AudioClip reference does not load its sample data when preload is off.
		foreach (AudioClip clip in new[] { SlicePopOut, SlicePopIn })
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (clip == null || clip.loadState == AudioDataLoadState.Loaded) continue;
			if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
			{
				Debug.LogWarning("Could not preload interaction audio: " + clip.name);
				continue;
			}
			await UniTask.WaitUntil(() => clip == null || clip.loadState != AudioDataLoadState.Loading,
				cancellationToken: cancellationToken);
			if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
				Debug.LogWarning("Interaction audio failed to load: " + clip.name);
		}
	}

	private void ConfigureAssets(GameData gameData)
	{
		_config = GameAssetsConfig.Instance;
		BoxFinishEffect = _config == null ? null : _config.BoxFinishEffectPrefab;
		BoxNormalEffect = _config == null ? null : _config.BoxNormalEffectPrefab;
		ToolShuffleMask = _config == null ? null : _config.ToolShuffleMaskPrefab;
		LoadAudio(gameData);
		LoadCustomerAssets(gameData);
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetPreloadedPrefabs() => PreloadedPrefabs.Clear();

	private UniTask LoadAudio(GameData gameData)
	{
		if (_config == null) _config = GameAssetsConfig.Instance;
		if (_config == null) return default;
		SlicePopIn = _config.SlicePopIn;
		SlicePopOut = _config.SlicePopOut;
		MatchClip = GameplayFeedbackRules.SelectMatch(_config.UseAlternateMatchAudio, _config.Match, _config.Match2);
		AccClip = _config.AccelerateAudio;
		DecClip = _config.DecelerateAudio;
		AccShortClip = _config.AccelerateShortAudio;
		UndoClip = _config.ToolUndoAudio;
		ShuffleClip = _config.ToolShuffleAudio;
		PathClip = _config.ToolPathAudio;
		StartShakeClip = _config.StartShakeAudio;
		Fail = _config.Fail;
		FailShow = _config.FailShow;
		OnlyEnterRockBlast = _config.OnlyEnterRockBlast;
		ColorLockOpen = _config.ColorLockOpen;
		return default;
	}

	private UniTask LoadCustomerAssets(GameData gameData)
	{
		Snui.PrefabLibrary library = Snui.PrefabLibrary.Current;
		if (library != null)
		{
			bool blue = GamePrefs.GetInt(GamePrefKeys.BlueSkin, 0) != 0;
			NewPlayerTutorialPrefab = blue && library.NewPlayerTutorialBluePrefab != null
				? library.NewPlayerTutorialBluePrefab : library.NewPlayerTutorialPrefab;
			FailTutorialPrefab = blue && library.FailTutorialBluePrefab != null
				? library.FailTutorialBluePrefab : library.FailTutorialPrefab;
		}
		return default;
	}

	public Slice GetSlice(Transform parent)
	{
		PooledSlice pooled = GetPooledGameObject<PooledSlice>("Slice", Slice, parent);
		return pooled == null ? null : pooled.GetComponent<Slice>();
	}

	public void PrewarmSlicePool(GameData data)
	{
		Stack<PooledGameObject> pool = GetSlicePrewarmPool(data, out int targetCount);
		if (pool == null) return;
		while (pool.Count < targetCount) AddPrewarmedSlice();
	}

	public async UniTask PrewarmSlicePoolAsync(GameData data, CancellationToken cancellationToken)
	{
		Stack<PooledGameObject> pool = GetSlicePrewarmPool(data, out int targetCount);
		if (pool == null) return;
		float frameStartedAt = Time.realtimeSinceStartup;
		while (pool.Count < targetCount)
		{
			cancellationToken.ThrowIfCancellationRequested();
			AddPrewarmedSlice();
			if (Time.realtimeSinceStartup - frameStartedAt < 0.004f) continue;
			await UniTask.NextFrame(cancellationToken);
			frameStartedAt = Time.realtimeSinceStartup;
		}
	}

	public async UniTask WarmupSliceVisualsAsync(GameData data, Transform parent, CancellationToken cancellationToken)
	{
		if (data == null || data.PathPoints.Count < 2 || LoadingPanel.Current == null) return;
		var types = new HashSet<int>();
		foreach (BoxInfo box in data.Boxes)
			if (box != null)
				foreach (CubeInfo cube in box.CubeInfos)
					if (cube != null) types.Add(cube.Type);
		foreach (SliceInfo info in data.TransferSlice)
			if (info != null) types.Add(info.Type);
		// Draw the real slice materials under the loading cover; inactive pooling
		// alone does not exercise the first draw on the gameplay cameras.
		foreach (int type in types)
		{
			cancellationToken.ThrowIfCancellationRequested();
			PooledSlice pooled = GetPooledGameObject<PooledSlice>("Slice", Slice, parent);
			if (pooled == null) continue;
			try
			{
				pooled.transform.SetPositionAndRotation(data.PathPoints[0], Quaternion.identity);
				pooled.GetComponent<Slice>().PrepareVisual(type);
				await UniTask.NextFrame(cancellationToken);
			}
			finally { if (pooled != null) pooled.Recycle(); }
		}
	}

	private Stack<PooledGameObject> GetSlicePrewarmPool(GameData data, out int targetCount)
	{
		targetCount = 0;
		if (data == null || Slice == null || data.PathPoints.Count < 2) return null;
		int totalSlices = data.TransferSlice.Count;
		int largestBox = 0;
		foreach (BoxInfo box in data.Boxes)
		{
			if (box == null) continue;
			int count = Mathf.Max(0, box.GetSliceCount());
			totalSlices += count;
			largestBox = Mathf.Max(largestBox, count);
		}
		targetCount = Mathf.Min(totalSlices,
			Mathf.Max(0, data.GetTransferMaxGroup()) * GameData.SliceCountPerGroup + largestBox);
		if (!_gameObjectPools.TryGetValue("Slice", out Stack<PooledGameObject> pool))
		{
			pool = new Stack<PooledGameObject>(targetCount);
			_gameObjectPools.Add("Slice", pool);
		}
		return pool;
	}

	private void AddPrewarmedSlice()
	{
		GameObject instance = Instantiate(Slice, transform);
		PooledSlice item = instance.GetComponent<PooledSlice>();
		if (item == null) item = instance.AddComponent<PooledSlice>();
		item.Initialize("Slice", this);
		item.Recycle();
	}

	public void PrewarmEffectPool(int targetCount = 10)
	{
		if (BoxNormalEffect == null) return;
		if (!_gameObjectPools.TryGetValue("BoxNormalEffect", out Stack<PooledGameObject> pool))
		{
			pool = new Stack<PooledGameObject>(targetCount);
			_gameObjectPools.Add("BoxNormalEffect", pool);
		}
		while (pool.Count < targetCount)
		{
			GameObject instance = Instantiate(BoxNormalEffect, transform);
			instance.name = "BoxNormalEffect_Pooled";
			PooledParticleEffect item = instance.GetComponent<PooledParticleEffect>();
			if (item == null) item = instance.AddComponent<PooledParticleEffect>();
			item.Initialize("BoxNormalEffect", this);
			item.Recycle();
		}
	}

	public T GetPooledGameObject<T>(string key, GameObject prefab, Transform parent) where T : PooledGameObject
	{
		if (string.IsNullOrEmpty(key) || prefab == null) return null;
		Stack<PooledGameObject> pool;
		if (!_gameObjectPools.TryGetValue(key, out pool))
		{
			pool = new Stack<PooledGameObject>();
			_gameObjectPools.Add(key, pool);
		}

		PooledGameObject item = null;
		while (pool.Count > 0 && item == null) item = pool.Pop();
		if (item == null)
		{
			GameObject instance = Instantiate(prefab, parent);
			item = instance.GetComponent<T>();
			if (item == null) item = instance.AddComponent<T>();
			item.Initialize(key, this);
		}
		else
		{
			item.MarkTakenFromPool();
			item.transform.SetParent(parent, false);
			item.gameObject.SetActive(true);
		}
		return item as T;
	}

	public void RecyclePooledGameObject(string key, PooledGameObject pooledGo)
	{
		if (pooledGo == null || string.IsNullOrEmpty(key)) return;
		Stack<PooledGameObject> pool;
		if (!_gameObjectPools.TryGetValue(key, out pool))
		{
			pool = new Stack<PooledGameObject>();
			_gameObjectPools.Add(key, pool);
		}
		pooledGo.gameObject.SetActive(false);
		pooledGo.transform.SetParent(transform, false);
		pool.Push(pooledGo);
	}

	private static GameObject LoadPrefab(string name)
	{
		if (PreloadedPrefabs.TryGetValue(name, out GameObject prefab) && prefab != null) return prefab;
		prefab = Resources.Load<GameObject>(ResourceRoot + name);
		if (prefab != null) PreloadedPrefabs[name] = prefab;
		return prefab;
	}

	public GameObject GetMapPrefab(string mapItemName)
	{
		string name = GameplayAssetPreloadRules.GetMapResourceName(mapItemName);
		return name == null ? null : LoadPrefab(name);
	}


	private void OnDestroy()
	{
		_gameObjectPools.Clear();
	}
}

public abstract class PooledGameObject : MonoBehaviour
{
	protected string _key;
	protected GameLoader _gameLoader;
	private bool _isInPool;

	public void MarkTakenFromPool() => _isInPool = false;

	public void Initialize(string key, GameLoader gameLoader)
	{
		_key = key;
		_gameLoader = gameLoader;
		RegisterRecycleCallback();
	}

	public void Recycle()
	{
		if (_isInPool) return;
		_isInPool = true;
		BeforeRecycle();
		if (_gameLoader != null) _gameLoader.RecyclePooledGameObject(_key, this);
		else gameObject.SetActive(false);
	}

	protected abstract void RegisterRecycleCallback();
	protected abstract void BeforeRecycle();
}

[RequireComponent(typeof(Slice))]
public class PooledSlice : PooledGameObject
{
	private Slice _slice;

	protected override void RegisterRecycleCallback()
	{
		if (_slice == null) _slice = GetComponent<Slice>();
		if (_slice != null)
		{
			_slice.OnSliceDismiss.RemoveListener(Recycle);
			_slice.OnSliceDismiss.AddListener(Recycle);
		}
	}

	protected override void BeforeRecycle()
	{
		if (_slice != null) _slice.ResetForPool();
		transform.localScale = Vector3.one;
	}
}
