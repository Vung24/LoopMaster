using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class GameController : MonoBehaviour, IGameNavigationSession
{
	[SerializeField, Min(0.1f)] private float _sliceJumpDuration = GameplayFeedbackRules.SliceJumpDuration;
	[SerializeField, Min(0.1f)] private float _sliceJumpHeight = GameplayFeedbackRules.SliceJumpHeight;
	[SerializeField, Min(0.02f)] private float _conveyorTickInterval = GameplayFeedbackRules.ConveyorNormalInterval;
	[SerializeField, Range(0.4f, 1f)] private float _sliceSpacingScale = 1f;
	public static GameController Current;
#if UNITY_EDITOR
	public static System.Func<GameData> EditorPlaytestSource;
	public static System.Func<GameData> EditorPlaytestResetSource;
	public void InitializeEditorPlaytest(GameData data) => Initialize(data, true);
#endif
	public bool IsEditorPlaytest { get; private set; }
	public GameData Data { get; private set; }
	public bool IsLevelReady { get; private set; }
	public System.Exception InitializationError { get; private set; }
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
	public Sprite SpeedButtonSprite => _gameLoader == null ? null : (IsSpeedUpActive ? _gameLoader.SpeedSprite : _gameLoader.UnSpeedSprite);
	public bool CanUseUndoTool => Boosters.CanUseUndo;
	public bool CanUseShuffleTool => Boosters.CanUseShuffle;
	public bool IsSelectingShuffle => _boosterFlow != null && _boosterFlow.IsSelectingShuffle;
	public bool CanSelectShuffleBox(Box box) => Boosters.CanSelectShuffleBox(box);
	public bool CanUsePathTool => Boosters.CanUsePath;
	public bool CanRequestUndoTool => !IsBoosterShopOpen && Boosters.CanRequestUndo;

	public UndoUnavailableReason GetUndoUnavailableReason()
	{
		return Boosters.GetUndoUnavailableReason();
	}
	public bool CanRequestShuffleTool => !IsBoosterShopOpen && Boosters.CanRequestShuffle;
	public bool CanRequestPathTool => !IsBoosterShopOpen && Boosters.CanRequestPath;
	public void GetToolRequestState(out bool undo, out bool shuffle, out bool path)
	{
		undo = shuffle = path = false;
		if (!IsBoosterShopOpen) Boosters.GetRequestState(out undo, out shuffle, out path);
	}
	public bool CanRevive => Data != null && Data.PathPoints.Count > 1 &&
		EndState == GameEndState.Failed && !IsTransitioningLevel;
	public bool IsFailTutorial() => !Snui.CheatManager.SkipTutorial && GamePrefs.GetInt(GamePrefKeys.FailTutorial, 0) == 0;
	public bool CanReplayLevel => Data != null && (IsEditorPlaytest || Navigation.CanReplayLevel);
	public bool CanLoadNextLevel => !IsEditorPlaytest && EndState == GameEndState.Won && Navigation.CanLoadNextLevel;
	public event System.Action GameWon;
	public event System.Action GameLost;
	public event System.Action GameRevived;
	public event System.Action<GameEndState> GameEnded;
	public event System.Action<int> LevelStarted;
	public event System.Action<int> LevelReplayed;
	public event System.Action<int> LevelAdvanced;
	public event System.Action<int> NextLevelUnavailable;
	private GameLoader _gameLoader;
	private LevelBuilder _levelBuilder;
	private LevelBuilder Builder => _levelBuilder ?? (_levelBuilder = new LevelBuilder(
		_gameLoader, Scene, RefreshPathState, _cameraFlow, UpdateCamera));
	private CoreGameplayEngine _engine;
	private GameplaySceneBuilder _sceneBuilder;
	private GameplaySceneBuilder Scene => _sceneBuilder ?? (_sceneBuilder = new GameplaySceneBuilder());
	private GroupLockFlow _groupLocks;
	private GroupLockFlow GroupLocks => _groupLocks ?? (_groupLocks = new GroupLockFlow(Scene, destroyCancellationToken));
	private readonly GameplayInput _input = new GameplayInput();
	private readonly GameplayCameraFlow _cameraFlow = new GameplayCameraFlow();
	private readonly List<Slice> _sliceBuffer = new List<Slice>();
	private bool _isGameStart;
	private CancellationTokenSource _startupInitialization;
	private float _comboTime;
	private BoosterFlow _boosterFlow;
	private bool CanRunBoosterOperations() =>
		!IsBoosterShopOpen && _isGameStart && EndState == GameEndState.Playing && !IsLevelIntroPlaying;

	private BoosterFlow Boosters => _boosterFlow ?? (_boosterFlow = new BoosterFlow(
		this, Scene.Boxes, GetBox, CanRunBoosterOperations,
		IsGameplaySettled, RefreshPathState, RefreshGameplayAfterOperation, PlayBoosterAudio));
	private bool IsUsingBooster => _boosterFlow != null && _boosterFlow.IsBusy;
	private GameNavigationFlow _navigation;
	private GameNavigationEffects _navigationEffects;
	private BuiltinPuzzleLoader _puzzleLoader;
	private Transform _dialogParent;
	private GameResultUI _resultUI;
	public Transform DialogParent => _dialogParent != null ? _dialogParent : (_uiCanvas != null ? _uiCanvas.transform : null);
	private GameNavigationFlow Navigation => _navigation ?? CreateNavigation();
	private bool IsTransitioningLevel => _navigation != null && _navigation.IsTransitioning;
	private readonly WinRewardFlow _winRewards = new WinRewardFlow(new WinRewardEconomy());
	public bool HasClaimedWinReward(GameData data) => _winRewards.HasClaimed(data);
	public int GetWinRewardCoins(GameData data) => EndState == GameEndState.Won ? _winRewards.GetCoins(data) : 0;

	public bool TryClaimWinReward(GameData data, bool doubled, out int coins)
	{
		return _winRewards.TryClaim(data, doubled, IsTransitioningLevel, out coins);
	}
	private ConveyorFlow _conveyorFlow;
	private bool IsFailureConveyorActive => Time.timeScale > 0f && Data != null &&
		EndState == GameEndState.Failed && IsGameFailed && !IsGameOver &&
		!Navigation.IsCurrentLevelAbandoned && !IsTransitioningLevel;
	private bool CanTickConveyor => (IsGameplayActive && !IsGameOver && !IsGameFailed) ||
		IsFailureConveyorActive;
	private bool IsConveyorTickingAllowed() => CanTickConveyor;

	private bool HandleConveyorSliceArrival(Slice slice)
	{
		if (!IsGameplayActive) return false;
		return Transfers.CheckInBox(slice, _sliceJumpDuration, _sliceJumpHeight);
	}

	private void HandleConveyorArrivalBatchChanged(bool active)
	{
		if (active) Transfers.BeginArrivalBatch();
		else Transfers.EndArrivalBatch();
	}

	private ConveyorFlow Conveyor => _conveyorFlow ?? (_conveyorFlow = new ConveyorFlow(
		Scene.Boxes, Scene.Roads, Scene.PathArrows,
		IsConveyorTickingAllowed,
		HandleConveyorSliceArrival, CheckGameOver,
		HandleConveyorArrivalBatchChanged));

	private bool IsGameStartActive() => _isGameStart;

	private SliceTransferFlow _sliceTransferFlow;
	private SliceTransferFlow Transfers => _sliceTransferFlow ?? (_sliceTransferFlow = new SliceTransferFlow(
		this, Scene.Boxes, IsGameStartActive,
		IsGroupUnlockAnimationPlaying, UpdateCombo, RefreshPathState, RefreshGameplayAfterOperation, CheckGameOver, SetSpeedUp));
	private BoxLaunch _boxLaunch;
	private BoxLaunch BoxLaunchFlow => _boxLaunch ?? (_boxLaunch = new BoxLaunch(
		() => IsGameplayActive,
		() => IsSelectingShuffle,
		box => Boosters.TryUseShuffle(box),
		HideShuffleSelection,
		() => Data,
		() => _linkedCable,
		IsGameplaySettled,
		() => Transfers,
		GetBox,
		() => _sliceJumpDuration,
		() => _sliceJumpHeight,
		NotifyBoxClickedPresentation));

	private LinkedCubeCable _linkedCable;
	[SerializeField] private Camera _mainCamera;
	// [SerializeField] private Camera _topCamera;

	public Camera MainCamera => _mainCamera;
	// public Camera TopCamera => _topCamera;
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
		AudioManager.EnsureInitialized();
		EventRuntime.EnsureInitialized();
		SpecialStreakManager.Ensure();
		LevelBoxManager.EnsureInitialized();
		Current = this;
		InventoryManager.Ensure();
		_gameLoader = GetComponent<GameLoader>();
		if (_gameLoader == null) _gameLoader = gameObject.AddComponent<GameLoader>();
		ResolvePuzzleLoader();
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
		_resultUI = GameResultUI.Ensure(this, WinDialogAddress, FailDialogAddress, FailDialogContentAddress);
		_dialogParent = _resultUI == null ? null : _resultUI.DialogParent;
	}

	private void ResolvePuzzleLoader()
	{
		if (_puzzleLoader != null) return;
		_puzzleLoader = GetComponent<BuiltinPuzzleLoader>();
		if (_puzzleLoader == null) _puzzleLoader = gameObject.AddComponent<BuiltinPuzzleLoader>();
	}

	private GameNavigationFlow CreateNavigation()
	{
		ResolvePuzzleLoader();
		_navigationEffects = new GameNavigationEffects();
		_navigation = new GameNavigationFlow(this, new GameLevelFlow(_puzzleLoader),
			_navigationEffects, destroyCancellationToken);
		_navigation.LevelReplayed += OnLevelReplayed;
		_navigation.LevelAdvanced += OnLevelAdvanced;
		_navigation.NextLevelUnavailable += OnNextLevelUnavailable;
		return _navigation;
	}

	private void OnLevelReplayed(int level) => LevelReplayed?.Invoke(level);
	private void OnLevelAdvanced(int level) => LevelAdvanced?.Invoke(level);
	private void OnNextLevelUnavailable(int level) => NextLevelUnavailable?.Invoke(level);

	bool IGameNavigationSession.TryStartLevel(GameData data)
	{
		Initialize(data);
		return Data == data && EndState == GameEndState.Playing;
	}

	private void Start()
	{
#if UNITY_EDITOR
        if (GamePrefs.EditorCampaignLoader != null)
        {
            ResolvePuzzleLoader();
            if (!_puzzleLoader.LoadDefault(this))
                InitializationError = new System.InvalidOperationException("Alternative campaign level could not be loaded. Check the campaign report and Console.");
            return;
        }
#endif
		int requestedCheatLevel = Snui.CheatManager.RequestedLevel;
		Snui.CheatManager.RequestedLevel = 0;
		if (requestedCheatLevel > 0 && CheatLoadLevel(requestedCheatLevel)) return;
#if UNITY_EDITOR
		GameData draft = EditorPlaytestSource?.Invoke();
		if (draft != null) { InitializeEditorPlaytest(draft); return; }
#endif
		if (GameData.Current != null && !GameProgressStore.IsLevelCompleted(GameData.Current.GetPuzzleActualLevel())) InitializeOnSceneStart(GameData.Current);
		else
		{
			ResolvePuzzleLoader();
			if (GameProgressStore.IsCampaignComplete ||
				!_puzzleLoader.TryCreateExactLevel(GameProgressStore.NextPlayableLevel, out GameData data))
			{
				Debug.LogWarning(GameProgressStore.IsCampaignComplete ? "All available levels complete." : "Next level data is missing.", this);
				InitializationError = new System.InvalidOperationException("No playable level data is available.");
				ReturnHome();
			}
			else InitializeOnSceneStart(data);
		}
	}

	private void InitializeOnSceneStart(GameData data)
	{
		if (LoadingPanel.Current == null) Initialize(data);
		else InitializeStartupAsync(data).Forget();
	}

	private async UniTask InitializeStartupAsync(GameData data)
	{
		CancellationTokenSource lifetime = null;
		try
		{
			if (!PrepareLevel(data, false)) return;
			lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_startupInitialization = lifetime;
			var token = lifetime.Token;
			await Builder.BuildAsync(data, GameRoot, token);
			CompleteLevelInitialization(data);
		}
		catch (System.OperationCanceledException) when (lifetime != null && lifetime.IsCancellationRequested) { }
		catch (System.Exception error)
		{
			InitializationError = error;
			throw;
		}
		finally
		{
			if (_startupInitialization == lifetime) _startupInitialization = null;
			lifetime?.Dispose();
		}
	}

	public void Initialize(GameData data)
	{
		Initialize(data, false);
	}

	private void Initialize(GameData data, bool editorPlaytest)
	{
		if (!PrepareLevel(data, editorPlaytest)) return;
		Builder.BuildSync(data, GameRoot);
		CompleteLevelInitialization(data);
	}

	private bool PrepareLevel(GameData data, bool editorPlaytest)
	{
		if (data == null) return false;
		if (!editorPlaytest && GameProgressStore.IsLevelCompleted(data.GetPuzzleActualLevel())) { GameData.SetCurrent(Data); return false; }
		_startupInitialization?.Cancel();
		IsLevelReady = false;
		InitializationError = null;
		_isGameStart = false;
		IsEditorPlaytest = editorPlaytest;
		GameData.SetCurrent(data);
		_boosterUI?.Cancel();
		_presentation?.Cancel();
		StopAllCoroutines();
		CancelShuffleSelection();
		_winRewards.BeginLevel(data);
		bool enteringEventLevel = Data != data && !editorPlaytest;
		Data = data;
		if (enteringEventLevel) Managers.Event?.DispatchEvent("OnEnterGame");
		Data.ConfigureRoadSpacing(_sliceSpacingScale);
		Data.DifferentColorSliceSpacing = 1;
		Navigation.BeginLevel(data);
		_engine = new CoreGameplayEngine(data);
		Conveyor.Reset(data, _engine);
		Boosters.Reset(data, _engine, _gameLoader, GameRoot);
		Transfers.Reset(data, _gameLoader, GameRoot);
		ComboInCount = 0;
		_comboTime = 0f;
		ClearRuntimeObjects();
		GroupLocks.Reset(data);
		return true;
	}

	private void CompleteLevelInitialization(GameData data)
	{
		if (data.LinkedPair != null && GameRoot != null)
		{
			_linkedCable = Builder.SpawnLinkedCable(data, GameRoot);
		}
		_cameraFlow.Reset(Data, GameRoot);
		UpdateCamera();
		_isGameStart = Data.PathPoints.Count > 1;
		IsGameOver = false;
		IsGameFailed = false;
		EndState = GameEndState.Playing;
		GameProgressStore.RecordStarted(CurrentLevelNumber);
		IsLevelReady = true;
		if (!IsEditorPlaytest)
		{
			HeartManager.EnsureInitialized()?.UseHeart();
		}
		LevelStarted?.Invoke(CurrentLevelNumber);
		if (Data == data && EndState == GameEndState.Playing)
			Presentation.Play(data, _uiCanvasInstance, Scene.Boxes, _mainCamera, _gameLoader.NewPlayerTutorialPrefab).Forget();
	}

	private void Update()
	{
		float delta = Time.deltaTime;
		if (!IsGameplayActive && !IsFailureConveyorActive)
		{
			if (_conveyorFlow != null) _conveyorFlow.AdvanceRoadVisuals(delta);
			return;
		}
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
		if (_mainCamera == null) _mainCamera = Camera.main;
	}

	private void LateUpdate()
	{
		UpdateCamera();
		UICanvas canvas = _uiCanvas != null ? _uiCanvas : UICanvas.Current;
		if (canvas != null) canvas.RefreshShuffleSelection();
	}

	public void UpdateCamera()
	{
		if (Data == null) return;
		if (_mainCamera == null) ResolveGameplayCameras();
		_cameraFlow.Update(_mainCamera, _uiCanvas == null ? null : _uiCanvas, CurrentLevelNumber);
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
		BoxLaunchFlow.OnClickBox(box);
	}

	private void NotifyBoxClickedPresentation()
	{
		if (_presentation != null && _uiCanvasInstance != null)
		{
			Transform capacityNode = UICanvas.Current != null ? UICanvas.Current.CapacityNode : (_uiCanvas != null ? _uiCanvas.CapacityNode : null);
			_presentation.OnBoxClicked(capacityNode);
		}
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
		UnlockAvailableGroupColorLockKeys();
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

	public void UnlockAvailableGroupColorLockKeys()
	{
		if (Data != null) GroupLocks.UnlockAvailableKeys();
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
		if (!previous && next)
			PlayBoosterAudio(_gameLoader == null ? null : _gameLoader.AccShortClip);
	}

	public void OnClickToolPath()
	{
		RequestTool(InventoryKeys.TOOL_PATH, CanRequestPathTool, UseToolPath);
	}

	public void OnUseToolPath()
	{
		UseToolPath();
	}

	public bool UseToolPath()
	{
		if (!Boosters.TryUsePath()) return false;
		UICanvas canvas = _uiCanvas != null ? _uiCanvas : UICanvas.Current;
		if (canvas != null) canvas.PlayPathExtension();
		return true;
	}

	public void OnClickToolUndo()
	{
		RequestTool(InventoryKeys.TOOL_UNDO, CanRequestUndoTool, UseToolUndo);
	}

	public void OnUseToolUndo()
	{
		UseToolUndo();
	}

	public bool UseToolUndo()
	{
		return Boosters.TryUseUndo();
	}

	public void OnClickToolShuffle()
	{
		RequestTool(InventoryKeys.TOOL_SHUFFLE, CanRequestShuffleTool, UseToolShuffle);
	}

	public void OnUseToolShuffle()
	{
		UseToolShuffle();
	}

	public bool UseToolShuffle()
	{
		if (!Boosters.TryUseShuffle()) return false;
		UICanvas canvas = _uiCanvas != null ? _uiCanvas : UICanvas.Current;
		ResolveGameplayCameras();
		if (canvas != null && canvas.ShowShuffleSelection(_mainCamera, Scene.Boxes))
			return true;
		CancelShuffleSelection();
		Debug.LogError("Shuffle selection requires a gameplay camera and the UICanvas ShuffleSelectionView reference.", this);
		return false;
	}

	public void CancelShuffleSelection()
	{
		_boosterFlow?.CancelShuffleSelection();
		HideShuffleSelection();
	}

	private void HideShuffleSelection()
	{
		UICanvas canvas = _uiCanvas != null ? _uiCanvas : UICanvas.Current;
		if (canvas != null) canvas.HideShuffleSelection();
	}

	private void RefreshGameplayAfterOperation()
	{
		UnlockAvailableGroupColorLockKeys();
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

	public void CheatWin()
	{
		GameOver();
	}

	public void CancelCheatTutorials() => _presentation?.Cancel();

	public bool CheatLoadLevel(int level)
	{
		if (level < 1 || IsTransitioningLevel || IsUsingBooster) return false;
		var catalog = BuiltinLevelCatalog.Current;
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
	public bool DebugForceEndState(GameEndState state)
	{
		if (!Application.isPlaying || !IsGameplayActive || IsTransitioningLevel ||
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

	public bool LoadNextLevel()
	{
		if (IsEditorPlaytest) return false;
		return Navigation.TryLoadNextLevel();
	}

	public bool ReplayLevel()
	{
#if UNITY_EDITOR
		if (IsEditorPlaytest)
		{
			GameData snapshot = EditorPlaytestResetSource?.Invoke();
			if (snapshot == null) return false;
			InitializeEditorPlaytest(snapshot); return true;
		}
#endif
		return Navigation.TryReplayLevel();
	}

	public void CommitAbandonedLevel()
	{
		Navigation.CommitAbandonedLevel();
	}
	public bool ReturnHome()
	{
		return Navigation.TryReturnHome();
	}

	public UniTask<bool> ReturnHomeAsync(bool withAnimation = true) => Navigation.ReturnHomeAsync(withAnimation);

	public void OnClickNextLevel()
	{
		LoadNextLevel();
	}

	public bool ShouldReturnHomeAfterWin => Navigation.ShouldReturnHomeAfterWin;

	public bool ContinueAfterWin()
	{
		if (IsEditorPlaytest) return ReplayLevel();
		return Navigation.TryContinueAfterWin(HasClaimedWinReward(Data));
	}

	public void OnClickReplayLevel()
	{
		ReplayLevel();
	}

	public void OnClickRestartLevel()
	{
		ReplayLevel();
	}

	private bool IsGameplaySettled()
	{
		if (Data != null && Data.ActiveLinkedLaunch != null) return false;
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

	public bool Revive(string way = "free", string route = "pre_fail")
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
		if (BgmSource.Current != null) BgmSource.Current.Play();
		PlayBoosterAudio(_gameLoader == null ? null : _gameLoader.PathClip);
		GameRevived?.Invoke();
		return true;
	}

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
		CancelShuffleSelection();
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
		{
			if (BgmSource.Current != null) BgmSource.Current.Stop().Forget();
			PlayBoosterAudio(_gameLoader == null ? null : _gameLoader.Fail);
		}
		if (state == GameEndState.Won)
		{
			if (!IsEditorPlaytest)
			{
				HeartManager.EnsureInitialized()?.AddHeart();
			}
			_winRewards.Prepare(Data);
			LevelBoxManager.EnsureInitialized();
			SpecialStreakManager.Ensure().OnGameWon(CurrentLevelNumber);
			if (!GameProgressStore.IsLevelCompleted(CurrentLevelNumber))
				Managers.Event?.DispatchEvent("OnGameWin", new Dictionary<string, int>
				{
					{ GameWinArgs.PassPoints, Data == null ? 1 : Mathf.Clamp((int)Data.GetHardLevel() + 1, 1, 3) }
				});
			GameProgressStore.RecordWin(CurrentLevelNumber, Navigation.HasNextLevel,
				Data == null ? HardLevel.Normal : Data.GetHardLevel());
			GameWon?.Invoke();
		}
		else GameLost?.Invoke();
		GameEnded?.Invoke(state);
	}

	private Box GetBox(BoxInfo info)
	{
		return Scene.GetBox(info);
	}

	private void ClearRuntimeObjects()
	{
		_linkedCable = null;
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
		_navigation?.Dispose();
		_boosterUI?.Cancel();
		_presentation?.Cancel();
		CancelShuffleSelection();
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
		CancelShuffleSelection();
		_sliceTransferFlow?.StopVibrationWindows();
		_boosterUI?.Cancel();
		_presentation?.Cancel();
	}

}
