using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Object;

public sealed class GameLevelPresentationFlow
{
    private readonly CancellationToken _destroyToken;
    private readonly GameController _controller;
    private GameObject _canvas;
    private IReadOnlyList<Box> _boxes;
    private NewPlayerTutorial _newPlayerTutorial;
    private FeatureUnlockDialog _featureUnlockDialog;
    private LevelDifficultyPanel _difficultyPanel;
    private CancellationTokenSource _levelPresentation;
    public bool IsPlaying { get; private set; }

    public GameLevelPresentationFlow(GameController controller, CancellationToken destroyToken)
    {
        _controller = controller;
        _destroyToken = destroyToken;
    }

    public void OnBoxClicked(Transform capacityNode)
    {
        if (_newPlayerTutorial != null) _newPlayerTutorial.OnClickBox(capacityNode);
    }

	public async UniTask Play(GameData data, GameObject canvas, IReadOnlyList<Box> boxes,
		Camera gameCamera, GameObject tutorialPrefab)
	{
		Cancel();
		if (canvas == null || data == null || data.PathPoints.Count < 2) return;
		_canvas = canvas;
		_boxes = boxes;
		var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
		_levelPresentation = lifetime;
		IsPlaying = true;
		MonitorInterruption(lifetime).Forget();
		try
		{
			await UniTask.WaitUntil(() => LoadingPanel.Current == null, cancellationToken: lifetime.Token);
			if (BgmSource.Current != null) BgmSource.Current.Play();
			if (AudioManager.Current != null && GameAssetsConfig.Instance != null)
				AudioManager.Current.PlayOneShot(GameAssetsConfig.Instance.StartShakeAudio);
			await ShowFeatureTutorial(data, lifetime.Token);
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
			await ShowBoosterIntroduction(data, lifetime.Token);
			lifetime.Token.ThrowIfCancellationRequested();
			CheckNewPlayerTutorial(data, gameCamera, tutorialPrefab);
		}
		catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
		finally
		{
			if (_levelPresentation == lifetime)
			{
				_levelPresentation = null;
				IsPlaying = false;
			}
			lifetime.Cancel();
			lifetime.Dispose();
		}
	}

	private async UniTask MonitorInterruption(CancellationTokenSource lifetime)
	{
		try
		{
			await UniTask.WaitUntil(PresentationInterrupted, cancellationToken: lifetime.Token);
			if (_levelPresentation == lifetime) Cancel();
		}
		catch (OperationCanceledException) { }
	}

	private bool PresentationInterrupted()
	{
		return Time.timeScale <= 0f || _controller == null || !_controller.isActiveAndEnabled ||
			_canvas == null || !_canvas.activeInHierarchy || _controller.EndState != GameEndState.Playing;
	}

	private async UniTask ShowBoosterIntroduction(GameData data, CancellationToken token)
	{
		string key = BoosterUnlockConfig.GetBoosterForLevel(data.GetPuzzleActualLevel());
		if (key == null) return;
		BoosterUnlockConfig.GetInfo(key, out BoosterUnlockInfo info);
		if (Snui.CheatManager.SkipTutorial)
		{
			BoosterUnlockProgress.MarkIntroSeen(key);
			return;
		}
		UICanvas canvas = _canvas.GetComponent<UICanvas>();
		ToolBase tool = canvas == null ? null : canvas.GetTool(key);
		Sprite icon = (tool != null && tool.IconSprite != null) ? tool.IconSprite : info.Icon;
		if (tool == null || icon == null)
		{
			Debug.LogError("Booster introduction requires a bound HUD tool with an icon: " + key, _controller);
			return;
		}
		if (!BoosterUnlockProgress.HasSeenIntro(key))
		{
			tool.SetIntroLocked(true);
			try
			{
				await UniTask.Delay(400, cancellationToken: token);
				GameObject instance = await Snui.PopupAssets.Instantiate("FeatureUnlockDialog", _canvas.transform, token);
				FeatureUnlockDialog dialog = instance.GetComponent<FeatureUnlockDialog>();
				_featureUnlockDialog = dialog;
				Vector3 origin = Vector3.zero;
				Vector2 originSize = Vector2.zero;
				if (dialog != null)
				{
					dialog.InitBooster(info.Title, info.Description, icon);
					Canvas.ForceUpdateCanvases();
					origin = dialog.IconPosition;
					originSize = dialog.IconWorldSize;
					await UniTask.WaitUntil(() => dialog == null || !dialog.gameObject.activeInHierarchy, cancellationToken: token);
					if (_featureUnlockDialog == dialog) _featureUnlockDialog = null;
				}
				else
				{
					Destroy(instance);
				}
				await BoosterFlyPresenter.PlayFlight(_canvas.transform, icon, origin, originSize, tool,
					() => CompleteBoosterIntroduction(tool), token);
				token.ThrowIfCancellationRequested();
			}
			finally
			{
				if (tool != null) tool.SetIntroLocked(false);
			}
		}
		// Guidance must allow the regular gameplay commands and conveyor to run.
		IsPlaying = false;
		await BoosterTutorialGuide.PlayGuide(_controller, canvas, _boxes, key, token);
	}

	private static void CompleteBoosterIntroduction(ToolBase tool)
	{
		if (tool == null) return;
		BoosterUnlockProgress.MarkIntroSeen(tool.Key);
		tool.SetIntroLocked(false);
	}

	public FeatureType IsFeatureTutorial(GameData data) => Snui.CheatManager.SkipTutorial ? FeatureType.None : FeatureUnlockDialog.GetUnseenFeature(data,
		(FeatureType)GamePrefs.GetInt(GamePrefKeys.FeatureTutorial, 0));

	private async UniTask ShowFeatureTutorial(GameData data, CancellationToken token)
	{
		FeatureType feature = IsFeatureTutorial(data);
		if (feature == FeatureType.None) return;
		GameObject instance = await Snui.PopupAssets.Instantiate("FeatureUnlockDialog", _canvas.transform, token);
		FeatureUnlockDialog dialog = instance.GetComponent<FeatureUnlockDialog>();
		if (dialog == null) { Destroy(instance); return; }
		_featureUnlockDialog = dialog;
		IsPlaying = true;
		GamePrefs.SetInt(GamePrefKeys.FeatureTutorial,
			GamePrefs.GetInt(GamePrefKeys.FeatureTutorial, 0) | (int)feature);
		GamePrefs.Save();
		dialog.Init(feature);
		try
		{
			await dialog.WaitForCloseAsync(token);
		}
		finally
		{
			if (_featureUnlockDialog == dialog) _featureUnlockDialog = null;
		}
	}

	private void CheckNewPlayerTutorial(GameData data, Camera gameCamera, GameObject tutorialPrefab)
	{
		if (Snui.CheatManager.SkipTutorial || data.LinkedPair != null) return;
		int stage = data.GetPuzzleActualLevel();
		if (stage < 1 || stage > 3 || tutorialPrefab == null) return;
		GameObject instance = Instantiate(tutorialPrefab, _canvas.transform, false);
		_newPlayerTutorial = instance.GetComponent<NewPlayerTutorial>();
		if (_newPlayerTutorial == null) { Destroy(instance); return; }
		_newPlayerTutorial.Initialize(stage);
		if (stage == 1)
		{
			Vector2 position = Vector2.zero;
			Box target = _boxes != null && _boxes.Count > 0 ? _boxes[0] : null;
			Canvas canvas = _canvas != null ? _canvas.GetComponent<Canvas>() : null;
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			RectTransform targetRect = _newPlayerTutorial != null ? _newPlayerTutorial.FingerParentRect : (instance.transform as RectTransform);
			if (target != null && gameCamera != null && targetRect != null)
			{
				Vector3 worldPos = target.GetTopSlicePos();
				Vector3 screenPoint = gameCamera.WorldToScreenPoint(worldPos);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPoint, uiCamera, out position);
			}
			_newPlayerTutorial.ShowLevel1(position);
		}
		else if (stage == 2) _newPlayerTutorial.ShowLevel2();
	}

	public void Cancel()
	{
		CancellationTokenSource lifetime = _levelPresentation;
		FeatureUnlockDialog dialog = _featureUnlockDialog;
		_levelPresentation = null;
		_featureUnlockDialog = null;
		lifetime?.Cancel();
		IsPlaying = false;
		if (dialog != null)
		{
			dialog.gameObject.SetActive(false);
			if (dialog != null) Destroy(dialog.gameObject);
		}
		if (_newPlayerTutorial != null) _newPlayerTutorial.CancelAndDismiss();
		_newPlayerTutorial = null;
		if (_difficultyPanel != null)
		{
			_difficultyPanel.gameObject.SetActive(false);
			Destroy(_difficultyPanel.gameObject);
		}
		_difficultyPanel = null;
	}

}
