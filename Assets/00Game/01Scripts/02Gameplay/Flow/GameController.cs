using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

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
	[SerializeField, Min(0.02f)] private float _conveyorTickInterval = 0.08f;
	[SerializeField, Range(0.4f, 1f)] private float _sliceSpacingScale = 1f;
	public static GameController Current;
#if UNITY_EDITOR
	public static System.Func<GameData> EditorPlaytestSource;
	public void InitializeEditorPlaytest(GameData data) => Initialize(data, true);
#endif
	public GameData Data { get; private set; }
	public GameObject UICanvasObjAsset;
	public GameObject UICanvasLightObjAsset;
	public string WinDialogAddress = "WinDialog";
	public string FailDialogAddress = "FailDialog";
	public string FailDialogContentAddress = "FailDialogContent";
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
	public bool IsGameplayActive => Time.timeScale > 0f && !IsBoosterShopOpen && _isGameStart && Data != null && EndState == GameEndState.Playing && !IsUsingBooster && !IsLevelIntroPlaying;
	public bool IsLevelIntroPlaying => _presentation != null && _presentation.IsPlaying;
	public bool IsSpeedUpActive => _conveyorFlow != null && _conveyorFlow.IsSpeedUpActive;
	public bool IsFinishAnimationPlaying
	{
		get
		{
			for (int i = 0; i < Scene.Boxes.Count; i++)
				if (Scene.Boxes[i] != null && Scene.Boxes[i].IsFinishAnimationPlaying) return true;
			return false;
		}
	}
	public float SliceJumpTimeScale => GameplayFeedbackRules.GetTransferTimeScale(IsSpeedUpActive);
	public Sprite SpeedButtonSprite => _gameLoader == null ? null :
		(IsSpeedUpActive ? _gameLoader.SpeedSprite : _gameLoader.UnSpeedSprite);
	public bool CanUseUndoTool => Boosters.CanUseUndo;
	public bool CanUseShuffleTool => Boosters.CanUseShuffle;
	public bool CanUsePathTool => Boosters.CanUsePath;
	public bool CanRequestUndoTool => !IsBoosterShopOpen && Boosters.CanRequestUndo;
	public bool CanRequestShuffleTool => !IsBoosterShopOpen && Boosters.CanRequestShuffle;
	public bool CanRequestPathTool => !IsBoosterShopOpen && Boosters.CanRequestPath;
	public void GetToolRequestState(out bool undo, out bool shuffle, out bool path)
	{
		undo = shuffle = path = false;
		if (!IsBoosterShopOpen) Boosters.GetRequestState(out undo, out shuffle, out path);
	}
	public bool CanRevive => Data != null && Data.PathPoints.Count > 1 &&
		EndState == GameEndState.Failed && !_isTransitioningLevel;
	public bool IsFailTutorial() => !Snui.CheatManager.SkipTutorial && GamePrefs.GetInt(GamePrefKeys.FailTutorial, 0) == 0;
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
	private GroupLockFlow _groupLocks;
	private GroupLockFlow GroupLocks => _groupLocks ?? (_groupLocks = new GroupLockFlow(Scene, destroyCancellationToken));
	private readonly GameplayInput _input = new GameplayInput();
	private readonly GameplayCameraFlow _cameraFlow = new GameplayCameraFlow();
	private readonly List<Slice> _sliceBuffer = new List<Slice>();
	private bool _isGameStart;
	private float _comboTime;
	private BoosterFlow _boosterFlow;
	private BoosterFlow Boosters => _boosterFlow ?? (_boosterFlow = new BoosterFlow(
		this, Scene.Boxes, GetBox, () => !IsBoosterShopOpen && _isGameStart && EndState == GameEndState.Playing && !IsLevelIntroPlaying,
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
	private bool IsFailureConveyorActive => Time.timeScale > 0f && Data != null &&
		EndState == GameEndState.Failed && IsGameFailed && !IsGameOver &&
		Data != _abandonedStreakData && !_isTransitioningLevel;
	private bool CanTickConveyor => (IsGameplayActive && !IsGameOver && !IsGameFailed) ||
		IsFailureConveyorActive;
	private ConveyorFlow Conveyor => _conveyorFlow ?? (_conveyorFlow = new ConveyorFlow(
		Scene.Boxes, Scene.Roads, Scene.PathArrows,
		() => CanTickConveyor,
		slice => IsGameplayActive && Transfers.CheckInBox(slice, _sliceJumpDuration, _sliceJumpHeight), CheckGameOver,
		active => { if (active) Transfers.BeginArrivalBatch(); else Transfers.EndArrivalBatch(); }));
	private SliceTransferFlow _sliceTransferFlow;
	private SliceTransferFlow Transfers => _sliceTransferFlow ?? (_sliceTransferFlow = new SliceTransferFlow(
		this, Scene.Boxes, () => _isGameStart,
		IsGroupUnlockAnimationPlaying, UpdateCombo, RefreshPathState, RefreshGameplayAfterOperation, CheckGameOver, SetSpeedUp));
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
	private GameObject _uiCanvasInstance;
	private UICanvas _uiCanvas;
	private GameLevelPresentationFlow _presentation;
	private GameLevelPresentationFlow Presentation => _presentation ?? (_presentation =
		new GameLevelPresentationFlow(destroyCancellationToken));
	private GameBoosterUIFlow _boosterUI;
	private GameBoosterUIFlow BoosterUI => _boosterUI ?? (_boosterUI = new GameBoosterUIFlow(this));
	private bool IsBoosterShopOpen => _boosterUI != null && _boosterUI.IsOpen;

	private void Awake()
	{
		EventRuntime.EnsureInitialized();
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
			_uiCanvas = _uiCanvasInstance.GetComponent<UICanvas>();
			if (_uiCanvas != null) _uiCanvas.Bind(this);
		}
		GameResultUI.Ensure(this, WinDialogAddress, FailDialogAddress, FailDialogContentAddress);
	}

	private void Start()
	{
		int requestedCheatLevel = Snui.CheatManager.RequestedLevel;
		Snui.CheatManager.RequestedLevel = 0;
		if (requestedCheatLevel > 0 && TryCheatLoadLevel(requestedCheatLevel)) return;
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
		_boosterUI?.Cancel();
		_presentation?.Cancel();
		StopAllCoroutines();
		if (Data != data)
		{
			_winRewardData = null;
			_winRewardCoins = 0;
			_winRewardClaimed = false;
		}
		bool enteringEventLevel = Data != data && !editorPlaytest;
		Data = data;
		if (enteringEventLevel) Managers.Event?.DispatchEvent("OnEnterGame");
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
		GroupLocks.Reset(data);
		_gameLoader.PrewarmSlicePool(data);
		Scene.Build(Data, _gameLoader, GameRoot, RefreshPathState);
		_cameraFlow.Reset(Data, GameRoot);
		UpdateCamera();
		_isGameStart = Data.PathPoints.Count > 1;
		IsGameOver = false;
		IsGameFailed = false;
		EndState = GameEndState.Playing;
		GameProgressStore.RecordStarted(CurrentLevelNumber);
		LevelStarted?.Invoke(CurrentLevelNumber);
		if (Data == data && EndState == GameEndState.Playing)
			Presentation.Play(data, _uiCanvasInstance, Scene.Boxes,
				_topCamera != null ? _topCamera : _mainCamera, _gameLoader.NewPlayerTutorialPrefab).Forget();
	}

	private void Update()
	{
		if (!IsGameplayActive && !IsFailureConveyorActive) return;
		float delta = Time.deltaTime;
		if (IsGameplayActive)
		{
			Data.Duration += delta;
			GameplayFeedbackRules.AdvanceCombo(ref ComboInCount, ref _comboTime, delta);
		}
		Conveyor.Tick(delta, GetTickInterval(false), GetTickInterval(true));
		if (IsGameplayActive && _input.TryGetClickedBox(_mainCamera, out Box box)) OnClickBox(box);
	}

	private void ResolveGameplayCameras()
	{
		if (_mainCamera != null && _topCamera != null) return;
		if (_mainCamera == null) _mainCamera = Camera.main;
	}

	private void LateUpdate()
	{
		UpdateCamera();
	}

	public void UpdateCamera()
	{
		if (Data == null) return;
		if (_mainCamera == null) ResolveGameplayCameras();
		_cameraFlow.Update(_mainCamera, _topCamera, _uiCanvas == null ? null : _uiCanvas, CurrentLevelNumber);
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
		return _groupLocks != null && _groupLocks.IsUnlocking(box);
	}

	public void OnClickBox(Box box)
	{
		if (!IsGameplayActive || box == null || box.Info == null ||
			box.Info.State != BoxState.Free || !box.CanPopupSlice()) return;
		Transfers.Launch(box, _sliceJumpDuration, _sliceJumpHeight);
		if (_presentation != null && _uiCanvasInstance != null)
			_presentation.OnBoxClicked(UICanvas.Current != null ? UICanvas.Current.CapacityNode : _uiCanvas?.CapacityNode);
	}

	public void HandleBoxFinished(Box box)
	{
		if (box == null || box.Info == null || Data == null) return;
		BoxFinishRules.UpdateCombo(Data, GetLastFinishBoxInterval());
		Data.LastFinishBoxTime = Data.Duration;
		Data.FinishBoxCount = Data.GetFinishBoxCount();
		Data.UndoData.Clear();
		GroupLocks.HandleFinishedBox(box.Info);
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
		if (Data != null) GroupLocks.TryUnlockAvailableKeys();
	}

	public void UpdateCombo()
	{
		GameplayFeedbackRules.RegisterArrival(ref ComboInCount, ref _comboTime);
	}

	public void SetSpeedUp(bool speedUp)
	{
		bool next = speedUp && Data != null && EndState == GameEndState.Playing && !IsLevelIntroPlaying;
		bool previous = IsSpeedUpActive;
		Conveyor.SetSpeedUp(next);
		if (previous != next)
		{
			if (GameRoot != null)
			{
				GameRoot.GetComponentsInChildren<Slice>(false, _sliceBuffer);
				float timeScale = SliceJumpTimeScale;
				for (int i = 0; i < _sliceBuffer.Count; i++)
					if (_sliceBuffer[i] != null) _sliceBuffer[i].SetJumpTimeScale(timeScale);
				_sliceBuffer.Clear();
			}
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
		if (_conveyorFlow != null && _conveyorFlow.IsFailureSlowdownActive)
			return _conveyorFlow.FailureTickInterval;
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

	public void CancelCheatTutorials() => _presentation?.Cancel();

	public bool TryCheatLoadLevel(int level)
	{
		if (level < 1 || _isTransitioningLevel || IsUsingBooster) return false;
		var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		if (catalog == null || !catalog.HasLevel(level)) return false;
		TextAsset asset = null;
		GameData previous = GameData.Current;
		try
		{
			asset = Resources.Load<TextAsset>($"{BuiltinLevelCatalog.ResourceFolder}/Level {level}");
			if (asset == null) return false;
			var data = GameLevelDataConverter.FromJson(asset.text, level);
			Initialize(data, true);
			return Data == data;
		}
		catch (System.Exception exception)
		{
			GameData.SetCurrent(previous);
			Debug.LogException(exception);
			return false;
		}
		finally
		{
			if (asset != null) Resources.UnloadAsset(asset);
		}
	}

#if UNITY_EDITOR
	public bool TryDebugForceEndState(GameEndState state)
	{
		if (!Application.isPlaying || !IsGameplayActive || _isTransitioningLevel ||
			(state != GameEndState.Won && state != GameEndState.Failed)) return false;
		CompleteGame(state);
		return EndState == state;
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
		Managers.StreakLine?.SynchronizeTempLevel();
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
			Managers.Event?.DispatchEvent("OnGameFail");
		}
	}
	public bool TryReturnHome()
	{
		if (!BeginReturnHome()) return false;
		ReturnHomeWithLoading().Forget();
		return true;
	}

	private bool BeginReturnHome()
	{
		if (_isTransitioningLevel || GameSceneManager.OnChangeScene) return false;
		// Pending wins are saved by OnGameWon; Home presents and settles them.
		Managers.StreakLine?.SynchronizeTempLevel();
		CommitAbandonedLevel();
		_isTransitioningLevel = true;
		return true;
	}

	public async UniTask<bool> ReturnHomeAsync(bool withAnimation = true)
	{
		if (!BeginReturnHome()) return false;
		return await LoadHomeAsync(withAnimation);
	}

	private async UniTaskVoid ReturnHomeWithLoading()
	{
		if (!await LoadHomeAsync(withAnimation: true))
			await LoadingPanel.TryDismiss();
	}

	private async UniTask<bool> LoadHomeAsync(bool withAnimation)
	{
		GameData previousData = GameData.Current;
		Time.timeScale = 1f;
		try
		{
			if (EndState != GameEndState.Won && Managers.Lava != null && Managers.Lava.ShouldShowFailDialog())
			{
				GameResultUI resultUI = GetComponent<GameResultUI>();
				Transform parent = resultUI == null ? null : resultUI.DialogParent;
				var dialog = await Snui.Lava.LavaDetailDialog.Show(parent);
				if (dialog != null)
				{
					Snui.RewardDialog.PrepareOverlay(dialog.gameObject, 1400);
					await dialog.WaitForCloseAsync();
				}
			}
			if (GameSceneManager.OnChangeScene)
			{
				_isTransitioningLevel = false;
				return false;
			}
			GameData.ClearCurrent();
			await GameSceneManager.EnterHome(withAnimation);
			return true;
		}
		catch (System.Exception error)
		{
			if (this != null)
			{
				GameData.SetCurrent(previousData);
				_isTransitioningLevel = false;
			}
			Debug.LogException(error);
			return false;
		}
	}

	public void OnClickNextLevel()
	{
		TryLoadNextLevel();
	}

	public bool ShouldReturnHomeAfterWin => !CanLoadNextLevel ||
		GameSceneRoutingRules.ReturnHomeAfterWin(GameProgressStore.CurrentPuzzleIndex);

	public bool TryContinueAfterWin()
	{
		if (EndState != GameEndState.Won || !HasClaimedWinReward(Data) || _isTransitioningLevel ||
			GameSceneManager.OnChangeScene) return false;
		return ShouldReturnHomeAfterWin ? TryReturnHome() : TryLoadNextLevel();
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
		if (Data == null || Data.OnJumpingCube > 0 || (_groupLocks != null && _groupLocks.IsBusy)) return false;
		for (int i = 0; i < Scene.Boxes.Count; i++)
			if (Scene.Boxes[i] != null && Scene.Boxes[i].IsLockAnimationPlaying) return false;
		for (int i = 0; i < Data.Boxes.Count; i++)
		{
			BoxInfo box = Data.Boxes[i];
			if (box != null && (box.State == BoxState.OnPopup || box.State == BoxState.OnPush)) return false;
		}
		return true;
	}

	private void RequestTool(string key, bool canRequest, System.Func<bool> use)
	{
		BoosterUI.Request(key, canRequest, use);
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
		_conveyorFlow?.EndFailureSlowdown();
		RefreshPathState();
		GameRevived?.Invoke();
		return true;
	}
	public void Revive(string way, string route) => TryRevive(way, route);

	public FeatureType IsFeatureTutorial() => Presentation.IsFeatureTutorial(Data);

	private void CompleteGame(GameEndState state)
	{
		if (state == GameEndState.Playing || EndState != GameEndState.Playing) return;
		_boosterUI?.Cancel();
		_presentation?.Cancel();
		EndState = state;
		IsGameOver = state == GameEndState.Won;
		IsGameFailed = state == GameEndState.Failed;
		_isGameStart = false;
		SetSpeedUp(false);
		if (state == GameEndState.Failed)
			Conveyor.BeginFailureSlowdown(GetTickInterval(false));
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
			if (!GameProgressStore.IsLevelCompleted(CurrentLevelNumber))
				Managers.Event?.DispatchEvent("OnGameWin", new Dictionary<string, int>
				{
					{ GameWinArgs.PassPoints, Data == null ? 1 : Mathf.Clamp((int)Data.GetHardLevel() + 1, 1, 3) }
				});
			GameProgressStore.RecordWin(CurrentLevelNumber, _levelFlow != null && _levelFlow.HasNext(CurrentLevelNumber),
				Data == null ? HardLevel.Normal : Data.GetHardLevel());
			GameWon?.Invoke();
		}
		else GameLost?.Invoke();
		GameEnded?.Invoke(state);
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
		_groupLocks?.Clear();
		// Return active slices before clearing the level so replay/next level reuse them.
		if (GameRoot != null)
		{
			GameRoot.GetComponentsInChildren<Slice>(true, _sliceBuffer);
			for (int i = 0; i < _sliceBuffer.Count; i++)
				if (_sliceBuffer[i] != null) _sliceBuffer[i].DismissForUndo();
			_sliceBuffer.Clear();
		}
		Scene.Clear(GameRoot);
	}

	private void OnDestroy()
	{
		_boosterUI?.Cancel();
		_presentation?.Cancel();
		_boosterFlow?.Cancel();
		_sliceTransferFlow?.Cancel();
		_groupLocks?.Clear();
		if (Current == this)
		{
			Current = null;
			if (GameData.Current == Data) GameData.ClearCurrent();
		}
		if (_uiCanvasInstance != null) Destroy(_uiCanvasInstance);
	}

	private void OnDisable()
	{
		_boosterUI?.Cancel();
		_presentation?.Cancel();
	}
}
