using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum GameEndState
{
	Playing = 0,
	Won = 1,
	Failed = 2
}

public class GameController : MonoBehaviour
{
	[SerializeField, Min(0.1f)] private float _sliceJumpDuration = GameplayFeedbackRules.SliceJumpDuration;
	[SerializeField, Min(0.1f)] private float _sliceJumpHeight = GameplayFeedbackRules.SliceJumpHeight;
	[SerializeField, Min(0.02f)] private float _conveyorTickInterval = 0.04f;
	[SerializeField, Range(0.4f, 1f)] private float _sliceSpacingScale = 1f;
	public static GameController Current;
#if UNITY_EDITOR
	// The editor supplies a detached draft after the Play Mode domain reload.
	public static System.Func<GameData> EditorPlaytestSource;
	public void InitializeEditorPlaytest(GameData data) => Initialize(data, true);
#endif
	public GameData Data { get; private set; }
	public GameObject UICanvasObjAsset;
	public GameObject UICanvasLightObjAsset;
	public GameObject WinDialogPrefab;
	public GameObject FailDialogPrefab;
	public GameObject FailDialogContentPrefab;
	public TMP_FontAsset FallbackUIFontAsset;
	[HideInInspector] public bool IsGameOver;
	[HideInInspector] public bool IsGameFailed;
	[HideInInspector] public Transform GameRoot;
	[HideInInspector] public int ComboInCount;
	public GameEndState EndState { get; private set; }
	public int RoadUsedCapacity => Data == null ? 0 : Data.GetTransferGroup();
	public int RoadMaxCapacity => Data == null ? 0 : Data.GetTransferMaxGroup();
	public int RoadAvailableCapacity => Data == null ? 0 : Data.GetTransferAvailableGroup();
	public int CurrentLevelNumber => Data == null ? -1 : Data.GetPuzzleActualLevel();
	public bool IsGameplayActive => Time.timeScale > 0f && _buyToolDialog == null && _isGameStart && Data != null && EndState == GameEndState.Playing && !IsUsingBooster && !_isShowingLevelIntro;
	public bool IsLevelIntroPlaying => _isShowingLevelIntro;
	public bool IsSpeedUpActive => _conveyorFlow != null && _conveyorFlow.IsSpeedUpActive;
	public bool IsFinishAnimationPlaying => Scene.Boxes.Any(box => box != null && box.IsFinishAnimationPlaying);
	public float SliceJumpTimeScale => GameplayFeedbackRules.GetTransferTimeScale(IsSpeedUpActive);
	public Sprite SpeedButtonSprite => _gameLoader == null ? null :
		(IsSpeedUpActive ? _gameLoader.SpeedSprite : _gameLoader.UnSpeedSprite);
	public bool CanUseUndoTool => Boosters.CanUseUndo;
	public bool CanUseShuffleTool => Boosters.CanUseShuffle;
	public bool CanUsePathTool => Boosters.CanUsePath;
	public bool CanRequestUndoTool => _buyToolDialog == null && Boosters.CanRequestUndo;
	public bool CanRequestShuffleTool => _buyToolDialog == null && Boosters.CanRequestShuffle;
	public bool CanRequestPathTool => _buyToolDialog == null && Boosters.CanRequestPath;
	public bool CanRevive => Data != null && Data.PathPoints.Count > 1 &&
		EndState == GameEndState.Failed && !_isTransitioningLevel;
	public bool IsFailTutorial() => PlayerPrefs.GetInt("offline_HasShowFailTutorial", 0) == 0;
	public bool CanReplayLevel => Data != null && !_isTransitioningLevel && EndState != GameEndState.Won && !GameProgressStore.IsLevelCompleted(CurrentLevelNumber);
	public bool CanLoadNextLevel => EndState == GameEndState.Won && !_isTransitioningLevel &&
		_levelFlow != null && _levelFlow.HasNext(CurrentLevelNumber);
	public event System.Action GameWon;
	public event System.Action GameLost;
	public event System.Action GameRevived;
	public event System.Action<GameEndState> GameEnded;
	public event System.Action<int> LevelStarted;
	public event System.Action<int> LevelReplayed;
	public event System.Action<int> LevelAdvanced;
	public event System.Action<int> NextLevelUnavailable;
	private GameLoader _gameLoader;
	private CoreGameplayEngine _engine;
	private GameplaySceneBuilder _sceneBuilder;
	private GameplaySceneBuilder Scene => _sceneBuilder ?? (_sceneBuilder = new GameplaySceneBuilder());
	private readonly HashSet<int> _unlockingGroupIds = new HashSet<int>();
	private readonly HashSet<GroupColorLockKey> _groupUnlockKeys = new HashSet<GroupColorLockKey>();
	private bool _isGameStart;
	private float _comboTime;
	private BoosterFlow _boosterFlow;
	private BoosterFlow Boosters => _boosterFlow ?? (_boosterFlow = new BoosterFlow(
		this, Scene.Boxes, GetBox, () => _buyToolDialog == null && _isGameStart && EndState == GameEndState.Playing && !_isShowingLevelIntro,
		IsGameplaySettled, RefreshPathState, RefreshGameplayAfterOperation, PlayBoosterAudio));
	private bool IsUsingBooster => _boosterFlow != null && _boosterFlow.IsBusy;
	private bool _isTransitioningLevel;
	private GameData _abandonedStreakData;
	private GameData _winRewardData;
	private int _winRewardCoins;
	private bool _winRewardClaimed;
	public bool HasClaimedWinReward(GameData data) => data != null &&
		_winRewardData == data && _winRewardClaimed;
	public int GetWinRewardCoins(GameData data) => data != null && Data == data &&
		_winRewardData == data && EndState == GameEndState.Won ? _winRewardCoins : 0;

	public bool TryClaimWinReward(GameData data, bool doubled, out int coins)
	{
		coins = 0;
		int reward = GetWinRewardCoins(data);
		if (reward <= 0 || _winRewardClaimed || _isTransitioningLevel) return false;
		reward *= doubled ? 2 : 1;
		// Reserve before publishing inventory events: listeners can reenter this controller.
		_winRewardClaimed = true;
		if (!InventoryManager.Ensure().Add(InventoryKeys.COIN, reward, doubled ? "win_offline_double" : "win"))
		{
			if (_winRewardData == data) _winRewardClaimed = false;
			return false;
		}
		coins = reward;
		return true;
	}
	private GameLevelFlow _levelFlow;
	private ConveyorFlow _conveyorFlow;
	private ConveyorFlow Conveyor => _conveyorFlow ?? (_conveyorFlow = new ConveyorFlow(
		Scene.Boxes, Scene.Roads, Scene.PathArrows,
		() => _isGameStart && Data != null && !IsGameOver && !IsGameFailed && !IsUsingBooster && !_isShowingLevelIntro,
		slice => Transfers.CheckInBox(slice, _sliceJumpDuration, _sliceJumpHeight), CheckGameOver));
	private SliceTransferFlow _sliceTransferFlow;
	private SliceTransferFlow Transfers => _sliceTransferFlow ?? (_sliceTransferFlow = new SliceTransferFlow(
		this, Scene.Boxes, () => _isGameStart, () => Conveyor.LaunchTickCount,
		IsGroupUnlockAnimationPlaying, UpdateCombo, RefreshPathState, RefreshGameplayAfterOperation, CheckGameOver));
	[SerializeField] private Camera _mainCamera;
	[SerializeField] private Camera _topCamera;
	[SerializeField] private Camera _uiCamera;
	[SerializeField] private Camera _backgroundCamera;
	[SerializeField] private Canvas _backgroundCanvas;
	[SerializeField] private GameBg _gameBg;

	public Camera MainCamera => _mainCamera;
	public Camera TopCamera => _topCamera;
	public Camera UICamera => _uiCamera;
	public Camera BackgroundCamera => _backgroundCamera;
	public Canvas BackgroundCanvas => _backgroundCanvas;
	public GameBg GameBg => _gameBg;
	private Bounds _cameraBounds;
	private Rect _cameraViewport;
	private Rect _cameraPixels;
	private float _cameraAspect;
	private bool _cameraLayoutReady;
	private bool _cameraLayoutPending;
	private GameObject _uiCanvasInstance;
	private NewPlayerTutorial _newPlayerTutorial;
	private FeatureUnlockDialog _featureUnlockDialog;
	private LevelDifficultyPanel _difficultyPanel;
	private Snui.Dialog _speedTutorialDialog;
	private CancellationTokenSource _levelPresentation;
	private bool _isShowingLevelIntro;
	private BuyToolDialog _buyToolDialog;
	[SerializeField] private bool _enableSpeedUpTutorial = true;

	private void Awake()
	{
		SpecialStreakManager.Ensure();
		LevelBoxManager.EnsureInitialized();
		Current = this;
		InventoryManager.Ensure();
		_gameLoader = GetComponent<GameLoader>();
		if (_gameLoader == null) _gameLoader = gameObject.AddComponent<GameLoader>();
		BuiltinPuzzleLoader puzzleLoader = GetComponent<BuiltinPuzzleLoader>();
		if (puzzleLoader == null) puzzleLoader = gameObject.AddComponent<BuiltinPuzzleLoader>();
		_levelFlow = new GameLevelFlow(this, puzzleLoader);
		ResolveGameplayCameras();
		if (GameRoot == null)
		{
			GameObject root = new GameObject("RuntimeGameRoot");
			GameRoot = root.transform;
		}
		if (UICanvasObjAsset != null)
		{
			_uiCanvasInstance = Instantiate(UICanvasObjAsset);
			_uiCanvasInstance.name = UICanvasObjAsset.name;
			_uiCanvasInstance.transform.SetParent(null, false);
			_uiCanvasInstance.SetActive(true);
			UICanvas ui = _uiCanvasInstance.GetComponent<UICanvas>();
			if (ui != null) ui.Bind(this);
		}
		GameResultUI.Ensure(this, WinDialogPrefab, FailDialogPrefab, FailDialogContentPrefab);
	}

	private void Start()
	{
#if UNITY_EDITOR
		GameData draft = EditorPlaytestSource?.Invoke();
		if (draft != null) { InitializeEditorPlaytest(draft); return; }
#endif
		if (GameData.Current != null && !GameProgressStore.IsLevelCompleted(GameData.Current.GetPuzzleActualLevel())) Initialize(GameData.Current);
		else
		{
			BuiltinPuzzleLoader puzzleLoader = GetComponent<BuiltinPuzzleLoader>();
			if (puzzleLoader == null) puzzleLoader = gameObject.AddComponent<BuiltinPuzzleLoader>();
			if (!puzzleLoader.LoadDefault(this))
			{
				Debug.LogWarning(GameProgressStore.IsCampaignComplete ? "All available levels complete." : "Next level data is missing.", this);
				TryReturnHome();
			}
		}
	}

	public void Initialize(GameData data)
	{
		Initialize(data, false);
	}

	private void Initialize(GameData data, bool editorPlaytest)
	{
		if (data == null) return;
		if (!editorPlaytest && GameProgressStore.IsLevelCompleted(data.GetPuzzleActualLevel())) { GameData.SetCurrent(Data); return; }
		GameData.SetCurrent(data);
		CancelBuyToolDialog();
		CancelLevelPresentation();
		StopAllCoroutines();
		if (Data != data)
		{
			_winRewardData = null;
			_winRewardCoins = 0;
			_winRewardClaimed = false;
		}
		Data = data;
		Data.ConfigureRoadSpacing(_sliceSpacingScale);
		Data.DifferentColorSliceSpacing = 1;
		if (_levelFlow == null)
		{
			BuiltinPuzzleLoader loader = GetComponent<BuiltinPuzzleLoader>();
			if (loader == null) loader = gameObject.AddComponent<BuiltinPuzzleLoader>();
			_levelFlow = new GameLevelFlow(this, loader);
		}
		_levelFlow.Capture(data);
		_engine = new CoreGameplayEngine(data);
		Conveyor.Reset(data, _engine);
		Boosters.Reset(data, _engine, _gameLoader, GameRoot);
		Transfers.Reset(data, _gameLoader, GameRoot);
		ComboInCount = 0;
		_comboTime = 0f;
		_isTransitioningLevel = false;
		_gameLoader.LoadAssetsAsync(data);
		ClearRuntimeObjects();
		Scene.Build(Data, _gameLoader, GameRoot, RefreshPathState);
		_cameraLayoutReady = GameplayCameraLayout.TryGetBounds(GameRoot, out _cameraBounds);
		if (!_cameraLayoutReady) _cameraBounds = GameplayCameraLayout.FallbackBounds(Data, GameRoot);
		_cameraLayoutReady = true;
		_cameraLayoutPending = true;
		UpdateCamera();
		_isGameStart = Data.PathPoints.Count > 1;
		IsGameOver = false;
		IsGameFailed = false;
		EndState = GameEndState.Playing;
		GameProgressStore.RecordStarted(CurrentLevelNumber);
		LevelStarted?.Invoke(CurrentLevelNumber);
		if (Data == data && EndState == GameEndState.Playing) ShowLevelPresentation(data).Forget();
	}

	private void Update()
	{
		if (!IsGameplayActive) return;
		float delta = Time.deltaTime;
		Data.Duration += delta;
		GameplayFeedbackRules.AdvanceCombo(ref ComboInCount, ref _comboTime, delta);
		Conveyor.Tick(delta, GetTickInterval(false), GetTickInterval(true));
		HandleTouchEvent();
	}

	private void ResolveGameplayCameras()
	{
		if (_mainCamera != null && _topCamera != null) return;
		if (_mainCamera == null) _mainCamera = Camera.main;
	}

	private void LateUpdate()
	{
		// Framing also refreshes during pause/intro and device orientation changes.
		if (!_cameraLayoutReady || Data == null) return;
		UpdateCamera();
	}

	public void UpdateCamera()
	{
		if (!_cameraLayoutReady || Data == null) return;
		if (_mainCamera == null) ResolveGameplayCameras();
		if (_mainCamera == null || !_mainCamera.orthographic) return;
		if (_cameraLayoutPending) Canvas.ForceUpdateCanvases();
		UICanvas ui = _uiCanvasInstance == null ? null : _uiCanvasInstance.GetComponent<UICanvas>();
		Rect pixels = _mainCamera.pixelRect;
		Rect viewport = ui == null ? GameplayCameraLayout.Viewport(pixels, Screen.safeArea, 0, 0)
			: ui.GetGameplayViewport(_mainCamera);
		if (!_cameraLayoutPending && pixels == _cameraPixels && viewport == _cameraViewport &&
			Mathf.Approximately(_cameraAspect, _mainCamera.aspect)) return;
		GameplayCameraLayout.Fit(_mainCamera, _topCamera, _cameraBounds, viewport, CurrentLevelNumber);
		_cameraViewport = viewport;
		_cameraPixels = pixels;
		_cameraAspect = _mainCamera.aspect;
		_cameraLayoutPending = false;
	}

	private void RefreshPathState()
	{
		if (Data == null) return;
		int used = Data.GetTransferGroup();
		int maximum = Mathf.Max(0, Data.GetPathLength());
		UpdateProgress(used, maximum);
		Conveyor.UpdatePathWarning();
	}

	private void UpdateProgress(int curProgress, int max)
	{
		Scene.UpdateCapacity(curProgress, max);
	}

	private bool IsGroupUnlockAnimationPlaying(Box box)
	{
		return box != null && box.Info != null && box.Info.GroupInfo != null &&
			_unlockingGroupIds.Contains(box.Info.GroupInfo.Id);
	}

	public void OnClickBox(Box box)
	{
		if (!IsGameplayActive || box == null || box.Info == null ||
			box.Info.State != BoxState.Free || !box.CanPopupSlice()) return;
		Transfers.Launch(box, _sliceJumpDuration, _sliceJumpHeight);
		if (_newPlayerTutorial != null && _uiCanvasInstance != null)
			_newPlayerTutorial.OnClickBox(HierarchyReferenceCache.Descendant(_uiCanvasInstance.transform, "Capacity"));
	}


	public void HandleBoxFinished(Box box)
	{
		if (box == null || box.Info == null || Data == null) return;
		BoxFinishRules.UpdateCombo(Data, GetLastFinishBoxInterval());
		Data.LastFinishBoxTime = Data.Duration;
		Data.FinishBoxCount = Data.GetFinishBoxCount();
		Data.UndoData.Clear();
		foreach (CubeInfo cube in box.Info.CubeInfos)
		{
			if (cube.GroupKeyId <= 0) continue;
			int groupId = cube.GroupKeyId;
			if (Data.TryUnlockGroupColorLock(groupId))
			{
				_unlockingGroupIds.Add(groupId);
				PlayGroupColorLockKeyUnlockAnim(cube, groupId).Forget();
			}
			else TakeGroupColorLockKey(cube)?.DestroySelf();
		}
		for (int i = 0; i < Scene.Boxes.Count; i++)
			if (Scene.Boxes[i] != null) Scene.Boxes[i].OnBoxFinished(box.Info);
		TryUnlockAvailableGroupColorLockKeys();
		CheckGameOver();
	}

	public float GetLastFinishBoxInterval() => Data == null ? float.PositiveInfinity :
		Data.Duration - Data.LastFinishBoxTime;

	public int GetBoxCombo(bool isEmoji) => Data == null ? 0 :
		(isEmoji ? Data.BoxEmojiCombo : Data.BoxCombo);

	private void CheckRemoveNoSolutionLockBoxes()
	{
		for (int i = 0; i < Scene.Boxes.Count; i++)
		{
			Box box = Scene.Boxes[i];
			if (box != null && box.Info != null && box.Info.IsAdjacentLocked)
				box.CheckNoSolutionRemoveLock();
		}
	}

	public void TryUnlockAvailableGroupColorLockKeys()
	{
		if (Data == null) return;
		for (int i = 0; i < Scene.Boxes.Count; i++)
		{
			Box box = Scene.Boxes[i];
			if (box == null || box.Info == null || box.Info.HasBlockingLock()) continue;
			List<CubeInfo> topCubes = box.Info.GetTopCubes(false);
			CubeInfo keyCube = null;
			for (int cubeIndex = 0; cubeIndex < topCubes.Count; cubeIndex++)
			{
				CubeInfo candidate = topCubes[cubeIndex];
				if (candidate == null || candidate.GroupKeyId <= 0) continue;
				keyCube = candidate;
				break;
			}
			if (keyCube == null || _unlockingGroupIds.Contains(keyCube.GroupKeyId)) continue;

			int groupId = keyCube.GroupKeyId;
			if (!Data.TryUnlockGroupColorLock(groupId)) continue;
			_unlockingGroupIds.Add(groupId);
			Data.UndoData.Clear();
			PlayGroupColorLockKeyUnlockAnim(keyCube, groupId).Forget();
		}
	}

	private static GroupColorLockKey TakeGroupColorLockKey(CubeInfo cube)
	{
		if (cube == null) return null;
		if (cube.CubeSc != null) return cube.CubeSc.TakeGroupColorLockKey();
		cube.ClearGroupColorLockKey();
		return null;
	}

	private async UniTask PlayGroupColorLockKeyUnlockAnim(CubeInfo cube, int groupId)
	{
		GameData data = Data;
		Scene.TryGetGroupRollDoor(groupId, out GroupRollDoor door);
		GroupColorLockKey key = TakeGroupColorLockKey(cube);
		if (key != null) _groupUnlockKeys.Add(key);
		try
		{
			if (key != null && door != null &&
				door.TryGetKeyInsertPose(out Vector3 position, out Quaternion rotation, out Vector3 direction))
			{
				key.DetachFromCube();
				await key.PlayInsertTo(position, rotation, direction);
				if (Data != data || door == null) return;
				door.Unlock();
				if (key != null) await key.PlayDismiss();
			}
			else
			{
				if (key != null) key.DestroySelf();
				if (door != null) door.Unlock();
			}
			await UniTask.WaitUntil(() => Data != data || door == null || door.IsUnlockComplete,
				cancellationToken: destroyCancellationToken);
		}
		finally
		{
			_groupUnlockKeys.Remove(key);
			if (key != null) key.DestroySelf();
			// Old level completions cannot release a new level's group with the same ID.
			if (Data == data)
			{
				_unlockingGroupIds.Remove(groupId);
				Scene.RemoveGroupRollDoor(groupId, door);
			}
		}
	}

	public void UpdateCombo()
	{
		GameplayFeedbackRules.RegisterArrival(ref ComboInCount, ref _comboTime);
	}


	public void SetSpeedUp(bool speedUp)
	{
		bool next = speedUp && Data != null && EndState == GameEndState.Playing && !_isShowingLevelIntro;
		bool previous = IsSpeedUpActive;
		Conveyor.SetSpeedUp(next);
		if (previous != next)
		{
			if (GameRoot != null)
				foreach (Slice slice in GameRoot.GetComponentsInChildren<Slice>())
					slice.SetJumpTimeScale(SliceJumpTimeScale);
		}
		if (GameplayFeedbackRules.ShouldPlaySpeedTransition(CurrentLevelNumber, previous, next))
			PlayBoosterAudio(_gameLoader == null ? null : (next ? _gameLoader.AccClip : _gameLoader.DecClip));
	}

	public void OnClickToolPath()
	{
		RequestTool(InventoryKeys.TOOL_PATH, CanRequestPathTool, TryUseToolPath);
	}

	public void OnUseToolPath()
	{
		TryUseToolPath();
	}

	public bool TryUseToolPath()
	{
		return Boosters.TryUsePath();
	}

	public void OnClickToolUndo()
	{
		RequestTool(InventoryKeys.TOOL_UNDO, CanRequestUndoTool, TryUseToolUndo);
	}

	public void OnUseToolUndo()
	{
		TryUseToolUndo();
	}

	public bool TryUseToolUndo()
	{
		return Boosters.TryUseUndo();
	}

	public void OnClickToolShuffle()
	{
		RequestTool(InventoryKeys.TOOL_SHUFFLE, CanRequestShuffleTool, TryUseToolShuffle);
	}

	public void OnUseToolShuffle()
	{
		TryUseToolShuffle();
	}

	public bool TryUseToolShuffle()
	{
		return Boosters.TryUseShuffle();
	}

	private void RefreshGameplayAfterOperation()
	{
		TryUnlockAvailableGroupColorLockKeys();
		CheckRemoveNoSolutionLockBoxes();
		RefreshPathState();
		CheckGameOver();
	}

	private void PlayBoosterAudio(AudioClip clip)
	{
		if (!GameAudioSettings.EffectsEnabled || clip == null) return;
		if (GameAudioPlayer.Current != null) GameAudioPlayer.Current.PlayOneShot(clip);
		else AudioSource.PlayClipAtPoint(clip, transform.position);
	}

	private float GetTickInterval(bool speedUp = false)
	{
		return GameplayFeedbackRules.GetConveyorInterval(_conveyorTickInterval, speedUp, _sliceSpacingScale);
	}

	private float GetSliceInterval(bool speedUp = false)
	{
		return GetTickInterval(speedUp) * 0.5f;
	}

	private bool IsSliceBlocked(SliceInfo slice, SliceInfo sliceB, bool isOverlapMove)
	{
		return _engine != null && _engine.IsSliceBlocked(slice, sliceB, isOverlapMove);
	}

	private bool IsBoxBlockIndex(int pathIndex)
	{
		for (int i = 0; i < Scene.Boxes.Count; i++)
		{
			Box box = Scene.Boxes[i];
			if (box != null && box.Info != null && box.Info.State == BoxState.OnPopup &&
				box.Info.PathIndex - 1 == pathIndex) return true;
		}
		return false;
	}

	public void CheatWin()
	{
		GameOver();
	}

#if UNITY_EDITOR
	public bool IsDebugGameplaySettled => IsGameplaySettled();
	public CoreMoveResult DebugEndResult => Data == null || _engine == null ?
		CoreMoveResult.None : _engine.EvaluateEndState();

	public bool TryDebugCheckEndState()
	{
		if (!Application.isPlaying || !IsGameplayActive || _isTransitioningLevel || _engine == null) return false;
		CheckGameOver();
		return true;
	}

	public bool TryDebugForceEndState(GameEndState state)
	{
		if (!Application.isPlaying || !IsGameplayActive || _isTransitioningLevel ||
			(state != GameEndState.Won && state != GameEndState.Failed)) return false;
		CompleteGame(state);
		return EndState == state;
	}

	public bool IsDebugAutoPlayReady => IsGameplayActive && IsGameplaySettled() &&
		Data.Boxes.All(box => box != null && box.State == BoxState.Free);
	public float DebugConveyorLoopDuration => Data == null ? 0f :
		Data.PathPoints.Count * GetTickInterval(IsSpeedUpActive);

	public bool TryDebugPlayBox(int boxIndex)
	{
		if (!IsDebugAutoPlayReady || boxIndex < 0 || boxIndex >= Data.Boxes.Count ||
			!Data.CanCubeJumpToTransfer()) return false;
		BoxInfo info = Data.Boxes[boxIndex];
		Box box = GetBox(info);
		if (box == null || info.IsFinished() || !box.CanPopupSlice() ||
			info.GetTopCubes(false).Count == 0) return false;
		OnClickBox(box);
		return info.State == BoxState.OnPopup;
	}
#endif

	private void CheckGameOver()
	{
		if (EndState != GameEndState.Playing || Data == null || _engine == null) return;
		CoreMoveResult result = _engine.EvaluateEndState();
		if (result == CoreMoveResult.GameWon && IsGameplaySettled()) GameOver();
		else if (result == CoreMoveResult.GameFailed && IsGameplaySettled()) GameFailed();
	}

	public void GameOver()
	{
		CompleteGame(GameEndState.Won);
	}

	public void GameFailed()
	{
		CompleteGame(GameEndState.Failed);
	}

	public bool NextLevel()
	{
		return TryLoadNextLevel();
	}

	public bool TryLoadNextLevel()
	{
		if (EndState != GameEndState.Won || Data == null || _isTransitioningLevel) return false;
		if (!SpecialStreakManager.Ensure().SettlePending()) return false;
		int nextLevel = CurrentLevelNumber + 1;
		if (nextLevel < 1) return false;
		if (_levelFlow == null || !_levelFlow.HasNext(CurrentLevelNumber))
		{
			NextLevelUnavailable?.Invoke(nextLevel);
			return false;
		}

		_isTransitioningLevel = true;
		bool loaded = _levelFlow.LoadNext(CurrentLevelNumber);
		if (!loaded) _isTransitioningLevel = false;
		else LevelAdvanced?.Invoke(nextLevel);
		return loaded;
	}

	public bool ReplayLevel()
	{
		return TryReplayLevel();
	}

	public bool RestartLevel()
	{
		return TryReplayLevel();
	}

	public bool TryReplayLevel()
	{
		if (!CanReplayLevel) return false;
		CommitAbandonedLevel();
		int level = CurrentLevelNumber;
		if (level < 1) level = Mathf.Max(1, Data.GetPuzzleMainLevel());
		_isTransitioningLevel = true;

		bool loaded = _levelFlow != null && _levelFlow.Replay(Data, level);

		if (!loaded) _isTransitioningLevel = false;
		else LevelReplayed?.Invoke(level);
		return loaded;
	}

	public void CommitAbandonedLevel()
	{
		if (Data != null && Data != _abandonedStreakData && EndState != GameEndState.Won)
		{
			_abandonedStreakData = Data;
			SpecialStreakManager.Ensure().OnGameFailed();
		}
	}
	public bool TryReturnHome()
	{
		if (_isTransitioningLevel || !Application.CanStreamedLevelBeLoaded("HomeScene")) return false;
		if (!SpecialStreakManager.Ensure().SettlePending()) return false;
		CommitAbandonedLevel();
		_isTransitioningLevel = true;
		GameData.ClearCurrent(); Time.timeScale = 1f;
		SceneManager.LoadScene("HomeScene");
		return true;
	}

	public void OnClickNextLevel()
	{
		TryLoadNextLevel();
	}

	public void OnClickReplayLevel()
	{
		TryReplayLevel();
	}

	public void OnClickRestartLevel()
	{
		TryReplayLevel();
	}

	private bool IsGameplaySettled()
	{
		return Data != null && Data.OnJumpingCube <= 0 && _unlockingGroupIds.Count == 0 &&
			!Scene.Boxes.Any(box => box != null && box.IsLockAnimationPlaying) &&
			!Data.Boxes.Any(box => box != null &&
				(box.State == BoxState.OnPopup || box.State == BoxState.OnPush));
	}

	private void RequestTool(string key, bool canRequest, System.Func<bool> use)
	{
		if (!canRequest) return;
		if (InventoryManager.Ensure().GetAmount(key) > 0) { use(); return; }
		GameData data = Data;
		GameResultUI resultUI = GetComponent<GameResultUI>();
		Transform parent = resultUI == null ? null : resultUI.DialogParent;
		_buyToolDialog = BuyToolDialog.Open(key, parent, () => this != null && Data == data &&
			EndState == GameEndState.Playing && !_isTransitioningLevel);
		if (_buyToolDialog == null) return;
		_buyToolDialog.Closed += result =>
		{
			_buyToolDialog = null;
			// Native PrepareUsedTool returns false after opening the dialog. The next tap uses the new count.
		};
	}
	private void CancelBuyToolDialog()
	{
		if (_buyToolDialog != null) _buyToolDialog.Cancel();
		_buyToolDialog = null;
	}

	public bool TryRevive(string way = "free", string route = "pre_fail")
	{
		if (!CanRevive) return false;
		Data.ExtendPath();
		Data.FailReason = string.Empty;
		Data.UndoData.Clear();
		IsGameFailed = false;
		IsGameOver = false;
		EndState = GameEndState.Playing;
		_isGameStart = true;
		RefreshPathState();
		GameRevived?.Invoke();
		return true;
	}
	public void Revive(string way, string route) => TryRevive(way, route);

	private async UniTask ShowLevelPresentation(GameData data)
	{
		if (_uiCanvasInstance == null || !_isGameStart) return;
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
		_levelPresentation = lifetime;
		try
		{
			await ShowFeatureTutorial(lifetime.Token);
			lifetime.Token.ThrowIfCancellationRequested();
			GameObject prefab = data.GetHardLevel() == HardLevel.Hard ? _gameLoader.LevelHardPanel :
				data.GetHardLevel() == HardLevel.VeryHard ? _gameLoader.LevelExpertPanel : null;
			if (prefab != null)
			{
				_isShowingLevelIntro = true;
				GameObject instance = Instantiate(prefab, _uiCanvasInstance.transform, false);
				_difficultyPanel = instance.GetComponent<LevelDifficultyPanel>();
				if (_difficultyPanel != null)
				{
					Snui.Snui_Text.EnsureFontAssets(instance.transform);
					await _difficultyPanel.Play(lifetime.Token);
				}
				else Destroy(instance);
				_difficultyPanel = null;
			}
			lifetime.Token.ThrowIfCancellationRequested();
			CheckNewPlayerTutorial();
			await CheckSpeedUpTutorial(lifetime.Token);
		}
		catch (System.OperationCanceledException) when (lifetime.IsCancellationRequested) { }
		finally
		{
			if (_levelPresentation == lifetime)
			{
				_levelPresentation = null;
				_isShowingLevelIntro = false;
			}
			lifetime.Dispose();
		}
	}

	public FeatureType IsFeatureTutorial() => FeatureUnlockDialog.GetUnseenFeature(Data,
		(FeatureType)PlayerPrefs.GetInt("offline_FeatureTutorialState", 0));

	private async UniTask ShowFeatureTutorial(CancellationToken token)
	{
		FeatureType feature = IsFeatureTutorial();
		if (feature == FeatureType.None || _gameLoader.FeatureUnlockDialogPrefab == null) return;
		GameObject instance = Instantiate(_gameLoader.FeatureUnlockDialogPrefab, _uiCanvasInstance.transform, false);
		FeatureUnlockDialog dialog = instance.GetComponent<FeatureUnlockDialog>();
		if (dialog == null) { Destroy(instance); return; }
		_featureUnlockDialog = dialog;
		_isShowingLevelIntro = true;
		// Native CheckFeatureTutorial records the flag when the prompt is shown.
		PlayerPrefs.SetInt("offline_FeatureTutorialState",
			PlayerPrefs.GetInt("offline_FeatureTutorialState", 0) | (int)feature);
		PlayerPrefs.Save();
		dialog.Init(feature);
		await UniTask.WaitUntil(() => dialog == null || !dialog.gameObject.activeInHierarchy, cancellationToken: token);
		if (_featureUnlockDialog == dialog) _featureUnlockDialog = null;
	}

	private void CheckNewPlayerTutorial()
	{
		int stage = GameProgressStore.HighestUnlockedLevel;
		if (stage < 1 || stage > 3 || _gameLoader.NewPlayerTutorialPrefab == null) return;
		GameObject instance = Instantiate(_gameLoader.NewPlayerTutorialPrefab, _uiCanvasInstance.transform, false);
		_newPlayerTutorial = instance.GetComponent<NewPlayerTutorial>();
		if (_newPlayerTutorial == null) { Destroy(instance); return; }
		_newPlayerTutorial.Initialize(stage);
		if (stage == 1)
		{
			Vector2 position = Vector2.zero;
			Box target = Scene.Boxes.Count > 1 ? Scene.Boxes[1] : Scene.Boxes.FirstOrDefault();
			Canvas canvas = _uiCanvasInstance.GetComponent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			if (target != null && _mainCamera != null)
				RectTransformUtility.ScreenPointToLocalPointInRectangle(instance.transform as RectTransform,
					_mainCamera.WorldToScreenPoint(target.transform.position), uiCamera, out position);
			_newPlayerTutorial.ShowLevel1(position);
		}
		else if (stage == 2) _newPlayerTutorial.ShowLevel2();
	}

	private bool ShouldShowSpeedUpTutorial()
	{
		// The offline game exposes the speed control that was gated by native test group B.
		return _enableSpeedUpTutorial && GameProgressStore.HighestUnlockedLevel >= 5 &&
			PlayerPrefs.GetInt("offline_HasShowSpeedUpTutorial", 0) == 0;
	}

	private async UniTask CheckSpeedUpTutorial(CancellationToken token)
	{
		Snui.DataPrefabLibrary library = Snui.DataPrefabLibrary.Current;
		if (!ShouldShowSpeedUpTutorial() || library == null ||
			library.DialogPrefab == null || library.SpeedUpTutorialPrefab == null) return;
		_isShowingLevelIntro = true;
		SpeedUpTutorialDialogContent content = await SpeedUpTutorialDialogContent.Show(_uiCanvasInstance.transform);
		Snui.Dialog dialog = content.GetComponentInParent<Snui.Dialog>();
		_speedTutorialDialog = dialog;
		PlayerPrefs.SetInt("offline_HasShowSpeedUpTutorial", 1);
		PlayerPrefs.Save();
		try
		{
			await UniTask.WaitUntil(() => content == null || content.Result != Snui.DialogContent.RESULT_UNKNOWN ||
				!content.gameObject.activeInHierarchy, cancellationToken: token);
		}
		finally
		{
			if (dialog != null)
			{
				dialog.gameObject.SetActive(false);
				Destroy(dialog.gameObject);
			}
			if (_speedTutorialDialog == dialog) _speedTutorialDialog = null;
		}
	}

	private void CancelLevelPresentation()
	{
		CancellationTokenSource lifetime = _levelPresentation;
		_levelPresentation = null;
		lifetime?.Cancel();
		_isShowingLevelIntro = false;
		if (_featureUnlockDialog != null)
		{
			_featureUnlockDialog.gameObject.SetActive(false);
			Destroy(_featureUnlockDialog.gameObject);
		}
		_featureUnlockDialog = null;
		if (_newPlayerTutorial != null) _newPlayerTutorial.CancelAndDismiss();
		_newPlayerTutorial = null;
		if (_difficultyPanel != null)
		{
			_difficultyPanel.gameObject.SetActive(false);
			Destroy(_difficultyPanel.gameObject);
		}
		_difficultyPanel = null;
		if (_speedTutorialDialog != null)
		{
			_speedTutorialDialog.gameObject.SetActive(false);
			Destroy(_speedTutorialDialog.gameObject);
		}
		_speedTutorialDialog = null;
	}

	private void CompleteGame(GameEndState state)
	{
		if (state == GameEndState.Playing || EndState != GameEndState.Playing) return;
		CancelBuyToolDialog();
		CancelLevelPresentation();
		EndState = state;
		IsGameOver = state == GameEndState.Won;
		IsGameFailed = state == GameEndState.Failed;
		_isGameStart = false;
		_conveyorFlow?.SetSpeedUp(false);
		ComboInCount = 0;
		_comboTime = 0f;
		_boosterFlow?.Cancel();
		_sliceTransferFlow?.Cancel();
		if (Data != null)
		{
			Data.OnJumpingCube = 0;
			Data.UndoData.Clear();
			Data.FinishBoxCount = Data.GetFinishBoxCount();
			if (state == GameEndState.Failed)
			{
				Data.FailCount++;
				if (string.IsNullOrEmpty(Data.FailReason)) Data.FailReason =
					Data.IsTransferHaveSpace() ? "nosavailablesmove" : "roadsfull";
			}
		}
		RefreshPathState();
		if (state == GameEndState.Failed)
			PlayBoosterAudio(_gameLoader == null ? null : _gameLoader.Fail);
		if (state == GameEndState.Won)
		{
			if (_winRewardData != Data)
			{
				_winRewardData = Data;
				_winRewardCoins = Data == null ? 0 : ConfigManager.GetFinishLevelCoinCount(Data.GetHardLevel());
				_winRewardClaimed = false;
			}
			LevelBoxManager.EnsureInitialized();
			SpecialStreakManager.Ensure().OnGameWon(CurrentLevelNumber);
			GameProgressStore.RecordWin(CurrentLevelNumber, _levelFlow != null && _levelFlow.HasNext(CurrentLevelNumber));
			GameWon?.Invoke();
		}
		else GameLost?.Invoke();
		GameEnded?.Invoke(state);
	}

	private void HandleTouchEvent()
	{
		if (Input.GetMouseButtonDown(0) && !IsPointerOverUI()) OnClickBox();
	}

	private static bool IsPointerOverUI()
	{
		EventSystem eventSystem = EventSystem.current;
		if (eventSystem == null) return false;
		Vector2 pointerPosition = Input.mousePosition;
		for (int i = 0; i < Input.touchCount; i++)
		{
			Touch touch = Input.GetTouch(i);
			if (touch.phase != TouchPhase.Began) continue;
			pointerPosition = touch.position;
			if (eventSystem.IsPointerOverGameObject(touch.fingerId)) return true;
			break;
		}

		var pointer = new PointerEventData(eventSystem) { position = pointerPosition };
		var hits = new List<RaycastResult>();
		eventSystem.RaycastAll(pointer, hits);
		return hits.Any(hit => hit.module is GraphicRaycaster);
	}

	private void OnClickBox()
	{
		if (_mainCamera == null) return;
		Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit, _mainCamera.farClipPlane)) OnClickBox(hit.collider.GetComponent<Box>());
	}

	private int WrapPathIndex(int index)
	{
		int count = Data == null ? 0 : Data.PathPoints.Count;
		return count <= 0 ? index : ((index % count) + count) % count;
	}

	private Box GetBox(BoxInfo info)
	{
		return Scene.GetBox(info);
	}

	private void ClearRuntimeObjects()
	{
		foreach (GroupColorLockKey key in _groupUnlockKeys.ToArray())
			if (key != null) key.DestroySelf();
		_groupUnlockKeys.Clear();
		_unlockingGroupIds.Clear();
		Scene.Clear(GameRoot);
	}

	private void OnDestroy()
	{
		CancelBuyToolDialog();
		CancelLevelPresentation();
		_boosterFlow?.Cancel();
		_sliceTransferFlow?.Cancel();
		foreach (GroupColorLockKey key in _groupUnlockKeys.ToArray())
			if (key != null) key.DestroySelf();
		_groupUnlockKeys.Clear();
		if (Current == this)
		{
			Current = null;
			if (GameData.Current == Data) GameData.ClearCurrent();
		}
		if (_uiCanvasInstance != null) Destroy(_uiCanvasInstance);
	}
}
