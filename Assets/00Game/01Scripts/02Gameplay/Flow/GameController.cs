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
		IsGroupUnlockAnimationPlaying, UpdateCombo, RefreshPathState, RefreshGameplayAfterOperation, CheckGameOver, SetSpeedUp,
		HandleLinkedLaunchFailure));
	private bool _recoverLinkedOnEnable;
	private bool _isLinkedRecovery;
	private LinkedCubeCable _linkedCable;
	[SerializeField] private Camera _mainCamera;
	[SerializeField] private Camera _topCamera;

	public Camera MainCamera => _mainCamera;
	public Camera TopCamera => _topCamera;
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
		int requestedCheatLevel = Snui.CheatManager.RequestedLevel;
		Snui.CheatManager.RequestedLevel = 0;
		if (requestedCheatLevel > 0 && TryCheatLoadLevel(requestedCheatLevel)) return;
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
				TryReturnHome();
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
			await UniTask.NextFrame(token);
			await _gameLoader.LoadAssetsAsync(data, token);
			GameVibrationHelper.PrepareForGameplay();
			await _gameLoader.PrewarmSlicePoolAsync(data, token);
			await Scene.BuildAsync(data, _gameLoader, GameRoot, RefreshPathState, token);
			_cameraFlow.Reset(data, GameRoot);
			UpdateCamera();
			await _gameLoader.WarmupSliceVisualsAsync(data, GameRoot, token);
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
		_gameLoader.LoadAssets(data);
		_gameLoader.PrewarmSlicePool(data);
		_gameLoader.PrewarmEffectPool(10);
		Scene.Build(Data, _gameLoader, GameRoot, RefreshPathState);
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
		_winRewards.BeginLevel(data);
		bool enteringEventLevel = Data != data && !editorPlaytest && !_isLinkedRecovery;
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
			GameObject cablePrefab = _gameLoader.LinkedCubeCable;
			if (cablePrefab == null) throw new System.InvalidOperationException("Missing CoreGamePrefabs/LinkedCubeCable prefab.");
			GameObject cableRoot = Instantiate(cablePrefab, GameRoot);
			_linkedCable = cableRoot.GetComponent<LinkedCubeCable>();
			if (_linkedCable == null) throw new System.InvalidOperationException("LinkedCubeCable prefab requires its visual component.");
			_linkedCable.Initialize(data);
		}
		_cameraFlow.Reset(Data, GameRoot);
		UpdateCamera();
		_isGameStart = Data.PathPoints.Count > 1;
		IsGameOver = false;
		IsGameFailed = false;
		EndState = GameEndState.Playing;
		GameProgressStore.RecordStarted(CurrentLevelNumber);
		IsLevelReady = true;
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
		if (!IsGameplayActive || box == null || box.Info == null) return;
		if (Data.ActiveLinkedLaunch != null)
		{
			box.PlayClickedAnim(false);
			if (_linkedCable != null) _linkedCable.ShowBlocked(LinkedCubeLaunchFailure.Busy);
			return;
		}
		if (box.Info.State != BoxState.Free) return;
		CubeInfo top = box.Info.PeekTopCube();
		if (top != null && top.LinkedPairId != 0)
		{
			LinkedCubePair pair = Data.LinkedPair;
			if (top.LinkedPairId < 0 || pair == null || pair.Id != top.LinkedPairId ||
				pair.State == LinkedCubePairState.Consumed || !pair.Contains(box.Info))
			{
				HandleLinkedLaunchFailure(new System.InvalidOperationException("Invalid linked cube intent in level " + Data.Id));
				return;
			}
			if (!IsGameplaySettled())
			{
				box.PlayClickedAnim(false);
				if (_linkedCable != null) _linkedCable.ShowBlocked(LinkedCubeLaunchFailure.Busy);
				return;
			}
			if (!Transfers.TryLaunchLinked(box, _sliceJumpDuration, _sliceJumpHeight, out LinkedCubeLaunchFailure failure))
			{
				box.PlayClickedAnim(false);
				if (_linkedCable != null) _linkedCable.ShowBlocked(failure);
				Box partner = GetBox(box.Info == pair.FirstBox ? pair.SecondBox : pair.FirstBox);
				if (partner != null) partner.PlayClickedAnim(false);
				if (failure == LinkedCubeLaunchFailure.InvalidState)
					HandleLinkedLaunchFailure(new System.InvalidOperationException("Linked pair " + pair.Id + " failed its state validation."));
				return;
			}
			if (_presentation != null && _uiCanvasInstance != null)
				_presentation.OnBoxClicked(UICanvas.Current != null ? UICanvas.Current.CapacityNode : _uiCanvas?.CapacityNode);
			return;
		}
		if (!box.CanPopupSlice()) return;
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
		if (!Boosters.TryUsePath()) return false;
		UICanvas canvas = _uiCanvas != null ? _uiCanvas : UICanvas.Current;
		if (canvas != null) canvas.PlayPathExtension();
		return true;
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
	public bool TryDebugForceEndState(GameEndState state)
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

	public bool NextLevel()
	{
		return TryLoadNextLevel();
	}

	public bool TryLoadNextLevel()
	{
		if (IsEditorPlaytest) return false;
		return Navigation.TryLoadNextLevel();
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
	public bool TryReturnHome()
	{
		return Navigation.TryReturnHome();
	}

	public UniTask<bool> ReturnHomeAsync(bool withAnimation = true) => Navigation.ReturnHomeAsync(withAnimation);

	public void OnClickNextLevel()
	{
		TryLoadNextLevel();
	}

	public bool ShouldReturnHomeAfterWin => Navigation.ShouldReturnHomeAfterWin;

	public bool TryContinueAfterWin()
	{
		if (IsEditorPlaytest) return TryReplayLevel();
		return Navigation.TryContinueAfterWin(HasClaimedWinReward(Data));
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
		if (Data != null && Data.LinkedPair != null && (!IsGameplaySettled() || IsUsingBooster))
		{
			_recoverLinkedOnEnable = true;
			_isGameStart = false;
			_sliceTransferFlow?.Cancel();
			_boosterFlow?.Cancel();
		}
		_sliceTransferFlow?.StopVibrationWindows();
		_boosterUI?.Cancel();
		_presentation?.Cancel();
	}

	private void OnEnable()
	{
		if (_recoverLinkedOnEnable) RecoverLinkedLevel();
	}

	private void HandleLinkedLaunchFailure(System.Exception error)
	{
		Debug.LogError("Linked cube level recovery: " + error, this);
		_isGameStart = false;
		_recoverLinkedOnEnable = true;
		_sliceTransferFlow?.Cancel();
		if (isActiveAndEnabled) RecoverLinkedLevel();
	}

	private void RecoverLinkedLevel()
	{
		_recoverLinkedOnEnable = false;
		_isLinkedRecovery = true;
		try
		{
			if (_resultUI != null) _resultUI.HideLinkedRecoveryError();
			Initialize(Data.RecreateFromTemplate(), IsEditorPlaytest);
			if (_linkedCable != null) _linkedCable.ShowRecoveryNotice();
		}
		catch (System.Exception error)
		{
			_isGameStart = false;
			IsLevelReady = false;
			GameData.SetCurrent(Data);
			InitializationError = error;
			Debug.LogError("Cannot recover linked cube level: " + error, this);
			if (_resultUI != null) _resultUI.ShowLinkedRecoveryError(this);
		}
		finally { _isLinkedRecovery = false; }
	}

	public bool TryRetryLinkedRecovery()
	{
		if (_isLinkedRecovery || InitializationError == null || Data == null || Data.LinkedPair == null) return false;
		RecoverLinkedLevel();
		return InitializationError == null && IsLevelReady;
	}
}
