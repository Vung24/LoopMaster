using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Object;

public sealed class GameLevelPresentationFlow
{
    private readonly GameController _controller;
    private readonly GameLoader _loader;
    private readonly Func<bool> _speedTutorialEnabled;
    private GameObject _canvas;
    private IReadOnlyList<Box> _boxes;
    private NewPlayerTutorial _newPlayerTutorial;
    private FeatureUnlockDialog _featureUnlockDialog;
    private LevelDifficultyPanel _difficultyPanel;
    private Snui.Dialog _speedTutorialDialog;
    private CancellationTokenSource _levelPresentation;
    public bool IsPlaying { get; private set; }

    public GameLevelPresentationFlow(GameController controller, GameLoader loader, Func<bool> speedTutorialEnabled)
    {
        _controller = controller;
        _loader = loader;
        _speedTutorialEnabled = speedTutorialEnabled;
    }

    public void OnBoxClicked(Transform capacityNode)
    {
        if (_newPlayerTutorial != null) _newPlayerTutorial.OnClickBox(capacityNode);
    }

	public async UniTask Play(GameData data, GameObject canvas, IReadOnlyList<Box> boxes)
	{
		Cancel();
		if (canvas == null || data == null || data.PathPoints.Count < 2) return;
		_canvas = canvas;
		_boxes = boxes;
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_controller.destroyCancellationToken);
		_levelPresentation = lifetime;
		IsPlaying = true;
		try
		{
			await ShowFeatureTutorial(lifetime.Token);
			lifetime.Token.ThrowIfCancellationRequested();
			string address = data.GetHardLevel() == HardLevel.Hard ? "LevelHardPanel" :
				data.GetHardLevel() == HardLevel.VeryHard ? "LevelExpertPanel" : null;
			if (address != null)
			{
				IsPlaying = true;
				GameObject instance = await Snui.PopupAssets.Instantiate(address, _canvas.transform, lifetime.Token);
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
			CheckNewPlayerTutorial(data);
			await CheckSpeedUpTutorial(lifetime.Token);
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
		finally
		{
			if (_levelPresentation == lifetime)
			{
				_levelPresentation = null;
				IsPlaying = false;
			}
			lifetime.Dispose();
		}
	}

	public FeatureType IsFeatureTutorial() => Snui.CheatManager.SkipTutorial ? FeatureType.None : FeatureUnlockDialog.GetUnseenFeature(_controller.Data,
		(FeatureType)PlayerPrefs.GetInt("offline_FeatureTutorialState", 0));

	private async UniTask ShowFeatureTutorial(CancellationToken token)
	{
		FeatureType feature = IsFeatureTutorial();
		if (feature == FeatureType.None) return;
		GameObject instance = await Snui.PopupAssets.Instantiate("FeatureUnlockDialog", _canvas.transform, token);
		FeatureUnlockDialog dialog = instance.GetComponent<FeatureUnlockDialog>();
		if (dialog == null) { Destroy(instance); return; }
		_featureUnlockDialog = dialog;
		IsPlaying = true;
		PlayerPrefs.SetInt("offline_FeatureTutorialState",
			PlayerPrefs.GetInt("offline_FeatureTutorialState", 0) | (int)feature);
		PlayerPrefs.Save();
		dialog.Init(feature);
		await UniTask.WaitUntil(() => dialog == null || !dialog.gameObject.activeInHierarchy, cancellationToken: token);
		if (_featureUnlockDialog == dialog) _featureUnlockDialog = null;
	}

	private void CheckNewPlayerTutorial(GameData data)
	{
		if (Snui.CheatManager.SkipTutorial) return;
		int stage = data.GetPuzzleActualLevel();
		if (stage < 1 || stage > 3 || _loader.NewPlayerTutorialPrefab == null) return;
		GameObject instance = Instantiate(_loader.NewPlayerTutorialPrefab, _canvas.transform, false);
		_newPlayerTutorial = instance.GetComponent<NewPlayerTutorial>();
		if (_newPlayerTutorial == null) { Destroy(instance); return; }
		_newPlayerTutorial.Initialize(stage);
		if (stage == 1)
		{
			Vector2 position = Vector2.zero;
			Box target = _boxes.Count > 1 ? _boxes[1] : _boxes.FirstOrDefault();
			Canvas canvas = _canvas.GetComponent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			Camera gameCamera = _controller.TopCamera != null ? _controller.TopCamera : _controller.MainCamera;
			if (target != null && gameCamera != null)
				RectTransformUtility.ScreenPointToLocalPointInRectangle(instance.transform as RectTransform,
					gameCamera.WorldToScreenPoint(target.transform.position), uiCamera, out position);
			_newPlayerTutorial.ShowLevel1(position);
		}
		else if (stage == 2) _newPlayerTutorial.ShowLevel2();
	}

	private bool ShouldShowSpeedUpTutorial()
	{
		if (_speedTutorialEnabled == null || !_speedTutorialEnabled()) return false;
		return !Snui.CheatManager.SkipTutorial && GameProgressStore.HighestUnlockedLevel >= 5 &&
			PlayerPrefs.GetInt("offline_HasShowSpeedUpTutorial", 0) == 0;
	}

	private async UniTask CheckSpeedUpTutorial(CancellationToken token)
	{
		if (!ShouldShowSpeedUpTutorial()) return;
		IsPlaying = true;
		SpeedUpTutorialDialogContent content = await SpeedUpTutorialDialogContent.Show(_canvas.transform);
		if (content == null) return;
		Snui.Dialog dialog = content.GetComponentInParent<Snui.Dialog>();
		try
		{
			token.ThrowIfCancellationRequested();
			_speedTutorialDialog = dialog;
			PlayerPrefs.SetInt("offline_HasShowSpeedUpTutorial", 1);
			PlayerPrefs.Save();
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

	public void Cancel()
	{
		CancellationTokenSource lifetime = _levelPresentation;
		_levelPresentation = null;
		lifetime?.Cancel();
		IsPlaying = false;
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

}
