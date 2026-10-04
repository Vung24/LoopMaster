using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameLoader : MonoBehaviour
{
	private const string ResourceRoot = "CoreGamePrefabs/";
	private GameAssetsConfig _config;
	private readonly Dictionary<string, Stack<PooledGameObject>> _gameObjectPools =
		new Dictionary<string, Stack<PooledGameObject>>();

	public GameObject Box { get; private set; }
	public GameObject BoxEnterOnly { get; private set; }
	public GameObject LimitedColorBox { get; private set; }
	public GameObject SingleTypeBox { get; private set; }
	public GameObject LongBox { get; private set; }
	public GameObject BoxLock { get; private set; }
	public GameObject AdjacentLock { get; private set; }
	public GameObject NumLock { get; private set; }
	public GameObject CubeNumLock { get; private set; }
	public GameObject Slice { get; private set; }
	public GameObject Cube { get; private set; }
	public GameObject FinishCube { get; private set; }
	public GameObject FinishLongCube { get; private set; }
	public GameObject GameRoad { get; private set; }
	public GameObject RoadArrow { get; private set; }
	public GameObject MapArrow { get; private set; }
	public GameObject MapEnter { get; private set; }
	public GameObject MapExit { get; private set; }
	public GameObject Capacity { get; private set; }
	public GameObject FlyPath { get; private set; }
	public GameObject GroupColorLock { get; private set; }
	public GameObject GroupKey { get; private set; }
	public GameObject LevelHardPanel { get; private set; }
	public GameObject LevelExpertPanel { get; private set; }
	public GameObject NewPlayerTutorialPrefab { get; private set; }
	public GameObject FailTutorialPrefab { get; private set; }
	public GameObject FeatureUnlockDialogPrefab { get; private set; }
	public GameObject ToolPathBoxEffect { get; private set; }
	public GameObject ToolShuffleMask { get; private set; }
	public GameObject BoxFinishEffect { get; private set; }
	public GameObject BoxNormalEffect { get; private set; }
	public GameObject BoxComboEffect { get; private set; }
	public GameObject PathComboEffect { get; private set; }

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

	public Sprite SpeedSprite;
	public Sprite UnSpeedSprite;
	public Sprite LevelTitleBgHardImage;
	public Sprite LevelTitleBgExpertImage;
	public Sprite TopLeftArmHardImage;
	public Sprite TopLeftArmExpertImage;

	private void Awake()
	{
		_config = GameAssetsConfig.Instance;
		LoadDefaultAssets();
	}

	public UniTask LoadAssetsAsync(GameData gameData)
	{
		LoadDefaultAssets();
		LoadAudio(gameData);
		LoadCustomerAssets(gameData);
		return default;
	}

	private UniTask LoadDefaultAssets()
	{
		Box = LoadPrefab("Box");
		BoxEnterOnly = LoadPrefab("OnlyEnterBox");
		LimitedColorBox = LoadPrefab("LimitedColorBox");
		SingleTypeBox = LoadPrefab("SingleTypeBox");
		LongBox = LoadPrefab("LongColorBox");
		BoxLock = LoadPrefab("BoxColorLock");
		AdjacentLock = LoadPrefab("BoxAdjacentLock");
		NumLock = LoadPrefab("BoxNumLock");
		CubeNumLock = LoadPrefab("CubeNumLock");
		Slice = LoadPrefab("Slice");
		Cube = LoadPrefab("Cube");
		FinishCube = LoadPrefab("CubeFinish");
		FinishLongCube = LoadPrefab("CubeLongFinish");
		GameRoad = LoadPrefab("GameRoad");
		RoadArrow = LoadPrefab("BoxArrow");
		MapArrow = LoadPrefab("MapArrow");
		MapEnter = LoadPrefab("MapEnter");
		MapExit = LoadPrefab("MapExit");
		Capacity = LoadPrefab("Capacity");
		FlyPath = LoadPrefab("FlyAddPath");
		GroupColorLock = LoadPrefab("KeyLockBox");
		GroupKey = LoadPrefab("Key");
		BoxComboEffect = LoadPrefab("BoxComboEffect");
		if (_config == null) _config = GameAssetsConfig.Instance;
		BoxFinishEffect = _config == null ? null : _config.BoxFinishEffectPrefab;
		BoxNormalEffect = _config == null ? null : _config.BoxNormalEffectPrefab;
		PathComboEffect = LoadPrefab("PathCombo");
		if (_config != null)
		{
			SpeedSprite = _config.SpeedImage;
			UnSpeedSprite = _config.UnSpeedImage;
		}
		return default;
	}

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
		Snui.DataPrefabLibrary library = Snui.DataPrefabLibrary.Current;
		if (library != null)
		{
			bool blue = PlayerPrefs.GetInt("offline_IsBlueSkin", 0) != 0;
			NewPlayerTutorialPrefab = blue && library.NewPlayerTutorialBluePrefab != null
				? library.NewPlayerTutorialBluePrefab : library.NewPlayerTutorialPrefab;
			FailTutorialPrefab = blue && library.FailTutorialBluePrefab != null
				? library.FailTutorialBluePrefab : library.FailTutorialPrefab;
			LevelHardPanel = library.LevelHardPanelPrefab;
			LevelExpertPanel = library.LevelExpertPanelPrefab;
			FeatureUnlockDialogPrefab = library.FeatureUnlockDialogPrefab;
		}
		return default;
	}

	public Slice GetSlice(Transform parent)
	{
		PooledSlice pooled = GetPooledGameObject<PooledSlice>("Slice", Slice, parent);
		return pooled == null ? null : pooled.GetComponent<Slice>();
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
		return Resources.Load<GameObject>(ResourceRoot + name);
	}

	public GameObject GetMapPrefab(string mapItemName)
	{
		if (string.IsNullOrEmpty(mapItemName)) return null;
		switch (mapItemName.Trim().ToLowerInvariant())
		{
			case "arrow":
			case "maparrow": return MapArrow;
			case "enter":
			case "mapenter": return MapEnter;
			case "exit":
			case "mapexit": return MapExit;
			case "capacity": return Capacity;
			default: return LoadPrefab(mapItemName);
		}
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

	public void Initialize(string key, GameLoader gameLoader)
	{
		_key = key;
		_gameLoader = gameLoader;
		RegisterRecycleCallback();
	}

	public void Recycle()
	{
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
		transform.localScale = Vector3.one;
	}
}
