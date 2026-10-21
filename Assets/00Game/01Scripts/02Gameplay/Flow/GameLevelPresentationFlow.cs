using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Object;

public sealed class GameLevelPresentationFlow
{
    private readonly CancellationToken _destroyToken;
    private GameObject _canvas;
    private IReadOnlyList<Box> _boxes;
    private NewPlayerTutorial _newPlayerTutorial;
    private FeatureUnlockDialog _featureUnlockDialog;
    private LevelDifficultyPanel _difficultyPanel;
    private CancellationTokenSource _levelPresentation;
    public bool IsPlaying { get; private set; }

    public GameLevelPresentationFlow(CancellationToken destroyToken)
    {
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
		try
		{
			// Keep tutorial/intro animations from running beneath the scene loading cover.
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
			lifetime.Dispose();
		}
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
		await UniTask.WaitUntil(() => dialog == null || !dialog.gameObject.activeInHierarchy, cancellationToken: token);
		if (_featureUnlockDialog == dialog) _featureUnlockDialog = null;
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
			Box target = _boxes.Count > 1 ? _boxes[1] : (_boxes.Count > 0 ? _boxes[0] : null);
			Canvas canvas = _canvas.GetComponent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
			if (target != null && gameCamera != null)
				RectTransformUtility.ScreenPointToLocalPointInRectangle(instance.transform as RectTransform,
					gameCamera.WorldToScreenPoint(target.transform.position), uiCamera, out position);
			_newPlayerTutorial.ShowLevel1(position);
		}
		else if (stage == 2) _newPlayerTutorial.ShowLevel2();
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
	}

}
