using System;
using System.Threading;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameResultUI : MonoBehaviour
{
	private GameController _controller;
	private string _winAddress;
	private string _failAddress;
	private string _failContentAddress;
	private CancellationTokenSource _resultLoad;
	private CancellationTokenSource _winLoad;
	private bool _winPreparing;
	private int _uiVersion;
	private bool _retryLoading;
	[SerializeField] private Canvas _canvas;
	private WinDialog _winDialog;
	private WinDialogContent _winContent;
	private FailDialog _failDialog;
	private FailDialogContent _failContent;
	private PreFailDialogContent _preFailContent;
	private NewPlayerTutorial _failTutorial;
	private RetryDialogContent _retryContent;
	private GameObject _retryRoot, _streakWarning;
	private SpecialStreakFailBanner _streakBanner;
	public Transform DialogParent => _canvas == null ? null : _canvas.transform;
	private Coroutine _showWinRoutine;
	private Coroutine _showFailRoutine;

	public static GameResultUI Ensure(GameController controller, string winAddress,
		string failAddress, string failContentAddress)
	{
		if (controller == null) return null;
		GameResultUI result = controller.GetComponent<GameResultUI>();
		if (result == null)
		{
			Debug.LogError("GameResultUI must be configured on the controller with a dialog canvas.", controller);
			return null;
		}
		result._winAddress = winAddress;
		result._failAddress = failAddress;
		result._failContentAddress = failContentAddress;
		result.Bind(controller);
		return result;
	}

	public void Bind(GameController controller)
	{
		if (controller != null && _canvas == null)
		{
			Debug.LogError("Assign the scene dialog canvas to GameResultUI.", this);
			return;
		}
		if (_controller != controller)
		{
			HideAll(releaseWin: true);
			Unsubscribe();
			_controller = controller;
			if (_controller != null)
			{
				_controller.GameEnded += OnGameEnded;
				_controller.GameRevived += OnGameRevived;
				_controller.LevelStarted += OnLevelStarted;
				_controller.NextLevelUnavailable += OnNextLevelUnavailable;
			}
		}

		if (_controller != null)
		{
			_canvas.gameObject.SetActive(true);
		}
	}

	private async UniTask BuildResultDialog(GameEndState state)
	{
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
		if (state == GameEndState.Won) _winLoad = lifetime;
		else _resultLoad = lifetime;
		try
		{
			if (state == GameEndState.Won && !string.IsNullOrEmpty(_winAddress))
			{
				GameObject root = await Snui.PopupAssets.Instantiate(_winAddress, _canvas.transform, lifetime.Token);
				root.SetActive(false);
				_winDialog = root.GetComponent<WinDialog>() ?? root.AddComponent<WinDialog>();
				_winDialog.Bind(_controller);
				_winContent = root.GetComponentInChildren<WinDialogContent>(true);
				if (_winContent != null) _winContent.Bind(_controller);
				else { Destroy(root); _winDialog = null; }
			}
			else if (state == GameEndState.Failed && !string.IsNullOrEmpty(_failAddress))
			{
				GameObject root = await Snui.PopupAssets.Instantiate(_failAddress, _canvas.transform, lifetime.Token);
				root.SetActive(false);
				_failDialog = root.GetComponent<FailDialog>() ?? root.AddComponent<FailDialog>();
				_failDialog.Bind(_controller);
				_preFailContent = root.GetComponentInChildren<PreFailDialogContent>(true);
				if (_preFailContent != null) _preFailContent.gameObject.SetActive(false);
				foreach (var candidate in root.GetComponentsInChildren<FailDialogContent>(true))
					if (!(candidate is PreFailDialogContent)) { _failContent = candidate; break; }
				if (_failContent == null && !string.IsNullOrEmpty(_failContentAddress))
				{
					Snui.SafeArea area = root.GetComponentInChildren<Snui.SafeArea>(true);
					GameObject content = await Snui.PopupAssets.Instantiate(_failContentAddress,
						area == null ? root.transform : area.transform, lifetime.Token);
					_failContent = content.GetComponent<FailDialogContent>();
				}
				if (_failContent != null) _failContent.Bind(_controller);
				else { Destroy(root); _failDialog = null; }
			}
			lifetime.Token.ThrowIfCancellationRequested();
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
		catch (Exception error)
		{
			Debug.LogWarning("Result dialog unavailable: " + error.Message);
			if (_winDialog != null && _winContent == null) { Destroy(_winDialog.gameObject); _winDialog = null; }
			if (_failDialog != null && _failContent == null) { Destroy(_failDialog.gameObject); _failDialog = null; _preFailContent = null; }
		}
		finally
		{
			if (_winLoad == lifetime) _winLoad = null;
			if (_resultLoad == lifetime) _resultLoad = null;
			lifetime.Dispose();
		}
	}

	private async UniTask PrepareWinDialog()
	{
		if (_winPreparing)
			await UniTask.WaitUntil(() => !_winPreparing, cancellationToken: this.GetCancellationTokenOnDestroy());
		if (_winDialog != null || _controller == null || _canvas == null || !isActiveAndEnabled) return;
		_winPreparing = true;
		try
		{
			await BuildResultDialog(GameEndState.Won);
			if (_winDialog != null) _winDialog.Hide();
		}
		finally { _winPreparing = false; }
	}

	private void OnGameEnded(GameEndState state)
	{
		HideAll();
		if (state == GameEndState.Won)
		{
			_showWinRoutine = StartCoroutine(ShowWinAfterAnimations(_controller, _controller.Data));
		}
		else if (state == GameEndState.Failed)
		{
			_showFailRoutine = StartCoroutine(ShowFailAfterDelay(_controller, _controller.Data));
		}
	}

	private IEnumerator ShowFailAfterDelay(GameController controller, GameData data)
	{
		int version = _uiVersion;
		yield return new WaitForSeconds(controller.IsFailTutorial() ? 0.5f : 1.2f);
		if (controller == null || _controller != controller || controller.Data != data ||
			controller.EndState != GameEndState.Failed) yield break;
		yield return BuildResultDialog(GameEndState.Failed).ToCoroutine();
		_showFailRoutine = null;
		if (!isActiveAndEnabled || version != _uiVersion || controller == null || controller.Data != data || controller.EndState != GameEndState.Failed) yield break;
		if (_failDialog != null) _failDialog.Show();
		if (_preFailContent != null)
		{
			if (_failContent != null) _failContent.Hide();
			_preFailContent.Configure(GamePrefs.GetInt(GamePrefKeys.FreeRevive, 0) == 0, ShowFinalFailure);
			_preFailContent.Show();
			ShowStreakWarning(_preFailContent.transform);
			ShowFailTutorial(_preFailContent);
		}
		else ShowFinalFailure();
	}

	private void ShowFinalFailure()
	{
		HideFailTutorial();
		if (_controller == null || _controller.EndState != GameEndState.Failed) return;
		if (_preFailContent != null) _preFailContent.Hide();
		if (_failContent == null) return;
		_failContent.Configure(GamePrefs.GetInt(GamePrefKeys.FreeRevive, 0) == 0, ShowRetryAfterLoss);
		_failContent.ReviveRoute = "fail";
		_failContent.SetLevel(_controller.CurrentLevelNumber, _controller.Data.FailReason);
		_failContent.Show();
		ShowStreakWarning(_failContent.transform);
		ShowFailTutorial(_failContent);
	}
	private void ShowStreakWarning(Transform parent)
	{
		if (_streakWarning != null) Destroy(_streakWarning);
		_streakWarning = null;
		var manager = SpecialStreakManager.Ensure();
		var prefab = Snui.PrefabLibrary.Current?.LoseSpecialStreakPrefab;
		if (!manager.ShouldShowLoseItem() || prefab == null) return;
		_streakWarning = Instantiate(prefab, parent, false);
		Snui.Snui_Text.EnsureFontAssets(_streakWarning.transform);
		var slider = _streakWarning.GetComponentInChildren<SpecialStreakMultiplierSlider>(true);
		if (slider != null) { slider.Type = SpecialStreakMultiplierSliderType.PreLose; slider.Init(manager.MultiplierIndex); }
	}
	private void ShowRetryAfterLoss() => LoadRetryAfterLoss().Forget();
	private async UniTask LoadRetryAfterLoss()
	{
		if (_controller == null || _controller.EndState != GameEndState.Failed || _retryRoot != null || _retryLoading) return;
		_controller.CommitAbandonedLevel();
		HideFailTutorial();
		if (_streakWarning != null) Destroy(_streakWarning); _streakWarning = null;
		if (_failContent != null) _failContent.Hide();
		int version = _uiVersion;
		_retryLoading = true;
		GameObject loaded;
		try { loaded = await Snui.PopupAssets.Instantiate("RetryDialogContent", _canvas.transform, this.GetCancellationTokenOnDestroy()); }
		finally { if (this != null && version == _uiVersion) _retryLoading = false; }
		if (this == null || version != _uiVersion || _controller == null || _controller.EndState != GameEndState.Failed)
		{ Destroy(loaded); return; }
		_retryRoot = loaded;
		Snui.RewardDialog.PrepareOverlay(_retryRoot, 1320);
		_retryContent = _retryRoot.GetComponent<RetryDialogContent>();
		if (_retryContent == null) { Destroy(_retryRoot); _retryRoot = null; return; }
		_retryContent.Bind(_controller);
		_retryContent.Dismissed += OnRetryDismissed;
		_retryContent.Show();
		Transform area = _failDialog == null ? null : _failDialog.SafeArea;
		_streakBanner = SpecialStreakFailBanner.Show(area == null ? _canvas.transform : area).GetAwaiter().GetResult();
		if (_streakBanner != null) Snui.RewardDialog.PrepareOverlay(_streakBanner.gameObject, 1321);
	}
	private void OnRetryDismissed(int result)
	{
		if (result == Snui.DialogContent.RESULT_CLOSE && _controller != null) _controller.TryReturnHome();
	}
	private void ShowFailTutorial(FailDialogContent content)
	{
		if (!_controller.IsFailTutorial() || content.FreeButtonNode == null) return;
		var library = Snui.PrefabLibrary.Current;
		var prefab = library == null ? null : library.FailTutorialPrefab;
		if (prefab == null) return;
		var instance = Instantiate(prefab, content.transform, false);
		_failTutorial = instance.GetComponent<NewPlayerTutorial>();
		if (_failTutorial == null) { Destroy(instance); return; }
		Canvas.ForceUpdateCanvases();
		Transform capacity = UICanvas.Current == null ? null : UICanvas.Current.CapacityNode;
		_failTutorial.ShowFail(capacity, content.FreeButtonNode);
		GamePrefs.SetInt(GamePrefKeys.FailTutorial, 1);
		GamePrefs.Save();
	}
	private void HideFailTutorial()
	{
		if (_failTutorial != null) _failTutorial.CancelAndDismiss();
		_failTutorial = null;
	}
	private void OnGameRevived() => HideAll();

	private IEnumerator ShowWinAfterAnimations(GameController controller, GameData data)
	{
		int version = _uiVersion;
		while (controller != null && controller.IsFinishAnimationPlaying) yield return null;
		if (controller == null || _controller != controller || controller.Data != data ||
			controller.EndState != GameEndState.Won) yield break;
		yield return PrepareWinDialog().ToCoroutine();
		_showWinRoutine = null;
		if (!isActiveAndEnabled || version != _uiVersion || controller == null || controller.Data != data || controller.EndState != GameEndState.Won) yield break;
		if (_winContent != null)
			_winContent.SetLevel(controller.CurrentLevelNumber, controller.CanLoadNextLevel);
		if (_winDialog != null) _winDialog.Show();
	}

	private void OnLevelStarted(int level)
	{
		if (HudContainer.Current != null) HudContainer.Current.gameObject.SetActive(false);
		HideAll();
	}

	private void OnNextLevelUnavailable(int level)
	{
		if (_winContent != null) _winContent.SetNoMoreLevels();
	}

	private void HideAll(bool releaseWin = false)
	{
		_uiVersion++;
		_retryLoading = false;
		_resultLoad?.Cancel();
		_resultLoad = null;
		if (_retryContent != null) _retryContent.Dismissed -= OnRetryDismissed;
		if (_retryRoot != null) { _retryRoot.SetActive(false); Destroy(_retryRoot); }
		_retryContent = null; _retryRoot = null;
		if (_streakBanner != null) Destroy(_streakBanner.gameObject); _streakBanner = null;
		if (_streakWarning != null) Destroy(_streakWarning); _streakWarning = null;
		HudContainer.TryRevertToOriginLayer();
		if (HudContainer.Current != null && _canvas != null && HudContainer.Current.transform.IsChildOf(_canvas.transform))
			HudContainer.Current.gameObject.SetActive(false);
		HideFailTutorial();
		if (_showWinRoutine != null) StopCoroutine(_showWinRoutine);
		_showWinRoutine = null;
		if (_showFailRoutine != null) StopCoroutine(_showFailRoutine);
		_showFailRoutine = null;
		if (releaseWin) { _winLoad?.Cancel(); _winLoad = null; }
		if (_winDialog != null && !_winDialog.IsEnteringHome && !_winDialog.IsTransitioningNextLevel)
		{
			_winDialog.Hide();
			if (releaseWin) Destroy(_winDialog.gameObject);
		}
		if (_failDialog != null) { _failDialog.Hide(); Destroy(_failDialog.gameObject); }
		if (releaseWin) { _winDialog = null; _winContent = null; }
		_failDialog = null; _failContent = null; _preFailContent = null;
	}

	private void Unsubscribe()
	{
		if (_controller == null) return;
		_controller.GameEnded -= OnGameEnded;
		_controller.GameRevived -= OnGameRevived;
		_controller.LevelStarted -= OnLevelStarted;
		_controller.NextLevelUnavailable -= OnNextLevelUnavailable;
	}

	private void OnDisable() { HideAll(releaseWin: true); }
	private void OnDestroy() { Unsubscribe(); }
}
