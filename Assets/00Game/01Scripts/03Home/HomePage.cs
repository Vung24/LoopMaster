using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Snui;
using Snui.Profile;
using System;
using UnityEngine;
using UnityEngine.UI;

public class HomePage : MonoBehaviour
{
	[SerializeField]
	private HomeEventController _eventController;

	[SerializeField]
	private PlayButton _playButton;

	[SerializeField]
	private Button _settingButton;

	[SerializeField]
	private LevelBoxMachine _levelBoxMachine;

	[SerializeField]
	private MyAvatar _myAvatar;

	[SerializeField]
	private AudioClip _homeInAudioClip;

	[SerializeField]
	private GameObject _clickMask;

	public bool IsAnimPlaying;

	private SpecialStreakProgress _specialStreakProgress;
	private CancellationTokenSource _cts;
	private bool _started;
	private bool _canSkipAnimations;
	private bool _isLeavingHome;
	private bool _progressCommitted;
	public bool IsEntryBlocked => !isActiveAndEnabled || !_canSkipAnimations || _isLeavingHome || GameSceneManager.OnChangeScene
		|| EventPrefabUI.HasOpenDialogs
		|| (GetComponent<DailyCheckInHomeUI>() != null && GetComponent<DailyCheckInHomeUI>().IsDialogOpen);

	public PlayButton PlayButton => _playButton;
	public HomeEventController EventController => _eventController;
	public Button SettingButton => _settingButton;

	private void Awake()
	{
		if (_playButton == null)
		{
			_playButton = GetComponentInChildren<PlayButton>(true);
		}
		if (_playButton != null)
		{
			_playButton.Bind(this);
		}

		if (_eventController == null)
		{
			_eventController = GetComponentInChildren<HomeEventController>(true);
		}

		if (_settingButton == null)
		{
			var btnTrans = HierarchyReferenceCache.Descendant(transform, "SettingBtn");
			if (btnTrans != null)
			{
				_settingButton = btnTrans.GetComponent<Button>();
			}
		}
		if (_settingButton != null)
		{
			_settingButton.onClick.AddListener(OnSettingClick);
		}

		if (_levelBoxMachine == null)
		{
			_levelBoxMachine = GetComponentInChildren<LevelBoxMachine>(true);
		}

	}

	private void OnEnable()
	{
		RefreshProgress();
		if (_started) BeginStartup();
	}

	private void Start()
	{
		Controller().Initialize();
		_started = true;
		BeginStartup();
	}

	private void BeginStartup()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();
		_canSkipAnimations = false;
		_isLeavingHome = false;
		_progressCommitted = false;
		IsAnimPlaying = true;
		if (_clickMask != null) _clickMask.SetActive(true);
		StartAsync(_cts.Token).Forget();
	}

	private async UniTask StartAsync(CancellationToken token)
	{
		try
		{
			EventRuntime.BeforeEnterHome();
			var controller = Controller();
			await UniTask.WaitUntil(() => !GameSceneManager.OnChangeScene, cancellationToken: token);
			while (LevelBoxManager.Ensure().PendingMilestone > 0)
			{
				int milestone = LevelBoxManager.Ensure().PendingMilestone;
				await UniTask.WaitUntil(() => controller.CanOpenLevelBox(_levelBoxMachine) && !EventPrefabUI.HasOpenDialogs,
					cancellationToken: token);
				if (!controller.TryOpenPendingLevelBox()) break;
				await UniTask.WaitUntil(() => !controller.HasLevelBoxOpen, cancellationToken: token);
				if (LevelBoxManager.Ensure().PendingMilestone == milestone) break;
			}
			await SetupEvents(token);
			token.ThrowIfCancellationRequested();
			if (_homeInAudioClip != null && AudioManager.Current != null)
			{
				AudioManager.Current.PlayOneShot(_homeInAudioClip);
			}

			if (_playButton != null)
			{
				await _playButton.InitAsync().AttachExternalCancellation(token);
				token.ThrowIfCancellationRequested();
				_playButton.PlayEnterAnim();
			}

			if (_eventController != null)
			{
				await _eventController.PlayEnterAnimation(true).AttachExternalCancellation(token);
			}
			if (_clickMask != null) _clickMask.SetActive(false);
			_specialStreakProgress = GetComponentInChildren<SpecialStreakProgress>(true);
			if (_specialStreakProgress != null)
			{
				Canvas canvas = GetComponentInParent<Canvas>();
				Transform parent = canvas == null ? transform : canvas.rootCanvas.transform;
				await _specialStreakProgress.ShowHomeCollection(parent).AttachExternalCancellation(token);
				token.ThrowIfCancellationRequested();
			}
			token.ThrowIfCancellationRequested();

			if (_playButton != null && HomeLevelChangePresentation.HasPending)
				await _playButton.PlayChangeLevelAnim(token);
			token.ThrowIfCancellationRequested();

			// Initialization can replace an incomplete scene widget with its full prefab.
			_levelBoxMachine = GetComponentInChildren<LevelBoxMachine>();
			if (_levelBoxMachine != null)
				await _levelBoxMachine.ShowHomeIntroduction(token);
			if (_specialStreakProgress != null)
				await _specialStreakProgress.ShowHomeIntroduction(token);
			token.ThrowIfCancellationRequested();

			if (_eventController != null)
			{
				await _eventController.ShowPopupDialog(token);
				token.ThrowIfCancellationRequested();
			}
			var daily = GetComponent<DailyCheckInHomeUI>();
			if (DailyCheckInManager.IsOpen && daily != null) await daily.ShowOnArrival(token);
			token.ThrowIfCancellationRequested();
			if (_eventController != null) await _eventController.PlayIdleAnimation(cancellationToken: token);
			_canSkipAnimations = true;
		}
		catch (OperationCanceledException) { }
		finally
		{
			if (!token.IsCancellationRequested)
			{
				IsAnimPlaying = false;
				if (_clickMask != null) _clickMask.SetActive(false);
			}
		}
	}

	private HomeController Controller()
	{
		HomeController controller = HomeController.ForScene(gameObject.scene);
		if (controller == null) controller = gameObject.AddComponent<HomeController>();
		if (controller.HomePage == null) controller.HomePage = this;
		return controller;
	}

	private async UniTask SetupEvents(CancellationToken token)
	{
		if (_eventController == null) return;
		_eventController.ClearWidgets();
		ConfigureEventLayout();
		var widgets = new List<HomeEventBase>();
		bool committed = false;
		try
		{
			foreach (var manager in new BaseEventManager[] { Managers.Pass, Managers.StreakLine })
			{
				if (manager == null || !manager.ShouldShowIcon()) continue;
				var icon = await manager.CreateIcon(_eventController.transform);
				if (icon != null) widgets.Add(icon);
				token.ThrowIfCancellationRequested();
			}
			_eventController.SetupWidgets(widgets);
			committed = true;
		}
		finally
		{
			if (!committed)
			{
				foreach (var widget in widgets) if (widget != null) Destroy(widget.gameObject);
			}
		}
	}

	private void ConfigureEventLayout()
	{
		var streak = GetComponentInChildren<SpecialStreakProgress>(true);
		_eventController.TopPadding = streak != null && streak.gameObject.activeSelf ? 335 : 230;
		_eventController.OffsetX = -110;
		_eventController.CenterAlignIcons = false;
	}

	public void RefreshProgress()
	{
		_playButton?.RefreshLevelLabel();
	}

	public void OnClickPlay()
	{
		TryPlay();
	}

	public bool TryPlay() => !IsEntryBlocked && Controller().TryEnterGame();

	private void OnSettingClick()
	{
		Controller().OpenSettings();
	}

	public bool IsSkipAnimationsBlocked() => IsEntryBlocked
		|| (Managers.Heart != null && !Managers.Heart.IsHeartEnough())
		|| (_eventController != null && _eventController.HasNonSkippableAnimation())
		|| (_specialStreakProgress != null && _specialStreakProgress.HasNonSkippableAnimation());

	public bool CommitProgressBeforeEnterGame()
	{
		if (IsEntryBlocked) return false;
		if (_progressCommitted) return true;
		_eventController?.CommitProgressBeforeEnterGame();
		_progressCommitted = true;
		return true;
	}

	public void SetLeavingHome(bool leaving) => _isLeavingHome = leaving;

	private void OnDisable()
	{
		_cts?.Cancel();
		EventPrefabUI.CloseDialogs(transform);
		_canSkipAnimations = false;
		IsAnimPlaying = false;
		if (_clickMask != null) _clickMask.SetActive(false);
	}

	private void OnDestroy()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		if (_settingButton != null)
		{
			_settingButton.onClick.RemoveListener(OnSettingClick);
		}
	}
}
