using System.Collections;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime bridge from the authored win prefab to level flow.</summary>
public class WinDialog : MonoBehaviour
{
	[SerializeField] private Image _mask;
	[SerializeField] private Transform _safeArea;
	[SerializeField] private Transform _fireworkLeft;
	[SerializeField] private Transform _fireworkRight;
	[SerializeField] private WinLogoDialogContent _logoDialogContent;
	[SerializeField] private WinDialogContent _winDialogContent;
	[SerializeField] private CanvasGroup _canvasGroup;
	[SerializeField] private AudioClip _winPopperCheerClip;
	[SerializeField] private UnityEngine.Events.UnityEvent OnSkipAnims;
	[SerializeField] private ClickDetector _clickDetector;

	private GameController _controller;
	private Coroutine _presentation;
	private Tween _maskFade;
	private bool _canSkip;

	private void Awake() { ResolveReferences(); }

	public void Bind(GameController controller)
	{
		_controller = controller;
		ResolveReferences();
		if (_winDialogContent != null) _winDialogContent.Bind(controller);
	}

	private void ResolveReferences()
	{
		if (_winDialogContent == null)
			_winDialogContent = GetComponentInChildren<WinDialogContent>(true);
		if (_logoDialogContent == null)
			_logoDialogContent = GetComponentInChildren<WinLogoDialogContent>(true);
		if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
		if (_clickDetector == null) _clickDetector = GetComponent<ClickDetector>();
		if (_clickDetector == null) _clickDetector = gameObject.AddComponent<ClickDetector>();
		_clickDetector.OnClick.RemoveListener(OnClick);
		_clickDetector.OnClick.AddListener(OnClick);
	}

	public void Show()
	{
		CancelPresentation();
		ResolveReferences();
		Snui.Snui_Text.EnsureFontAssets(transform);
		if (_winDialogContent != null) _winDialogContent.Hide();
		if (_logoDialogContent != null) _logoDialogContent.Hide();
		SetMaskAlpha(0f);
		gameObject.SetActive(true);
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 1f;
			_canvasGroup.interactable = true;
			_canvasGroup.blocksRaycasts = true;
		}
		_presentation = StartCoroutine(PlayPresentation());
	}

	private IEnumerator PlayPresentation()
	{
		PlayFireworks(_fireworkLeft);
		PlayFireworks(_fireworkRight);
		if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(_winPopperCheerClip, gameObject);
		if (_logoDialogContent != null)
		{
			// Native StartAsync: fireworks first, then logo and skip input after 0.8s.
			yield return new WaitForSecondsRealtime(0.8f);
			if (_mask != null) _maskFade = _mask.DOFade(0.98f, 0.1f).SetUpdate(true);
			_canSkip = true;
			_logoDialogContent.PlayLogoAnimation();
			while (_logoDialogContent != null && _logoDialogContent.gameObject.activeSelf &&
				_logoDialogContent.Result == Snui.DialogContent.RESULT_UNKNOWN) yield return null;
		}
		else
		{
			SetMaskAlpha(0.98f);
			_canSkip = true;
		}
		if (_winDialogContent != null) _winDialogContent.Show();
		_presentation = null;
	}

	public void Hide()
	{
		CancelPresentation();
		if (_logoDialogContent != null) _logoDialogContent.Hide();
		if (_winDialogContent != null) _winDialogContent.Hide();
		StopFireworks(_fireworkLeft);
		StopFireworks(_fireworkRight);
		if (_canvasGroup != null)
		{
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}
		gameObject.SetActive(false);
	}

	private static void PlayFireworks(Transform root)
	{
		if (root == null) return;
		root.gameObject.SetActive(true);
		foreach (SkeletonGraphic graphic in root.GetComponentsInChildren<SkeletonGraphic>(true))
		{
			graphic.UnscaledTime = true;
			graphic.Initialize(false);
			var data = graphic.skeletonDataAsset == null ? null : graphic.skeletonDataAsset.GetSkeletonData(false);
			if (data == null || data.FindAnimation("animation") == null || graphic.AnimationState == null) continue;
			graphic.Skeleton.SetToSetupPose();
			graphic.AnimationState.ClearTracks();
			graphic.AnimationState.SetAnimation(0, "animation", false);
		}
		foreach (ParticleSystem effect in root.GetComponentsInChildren<ParticleSystem>(true))
		{
			var main = effect.main;
			main.useUnscaledTime = true;
			effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			effect.Play();
		}
	}

	private static void StopFireworks(Transform root)
	{
		if (root == null) return;
		foreach (ParticleSystem effect in root.GetComponentsInChildren<ParticleSystem>(true))
			effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		root.gameObject.SetActive(false);
	}

	private void SetMaskAlpha(float alpha)
	{
		if (_mask == null) return;
		Color color = _mask.color;
		color.a = alpha;
		_mask.color = color;
	}

	private void CancelPresentation()
	{
		_canSkip = false;
		if (_presentation != null) StopCoroutine(_presentation);
		_presentation = null;
		_maskFade?.Kill();
		_maskFade = null;
		if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject);
	}

	private void OnDisable()
	{
		CancelPresentation();
		if (_logoDialogContent != null) _logoDialogContent.Hide();
	}

	private void OnDestroy()
	{
		CancelPresentation();
		if (_clickDetector != null) _clickDetector.OnClick.RemoveListener(OnClick);
	}

	public void OnClick()
	{
		if (!_canSkip) return;
		if (_logoDialogContent != null && _logoDialogContent.gameObject.activeSelf)
			_logoDialogContent.OnSkipAnims();
		if (_winDialogContent != null && _winDialogContent.gameObject.activeSelf)
			_winDialogContent.OnSkipAnims();
		OnSkipAnims?.Invoke();
	}
	public void OnClickNextLevel()
	{
		if (_winDialogContent != null) _winDialogContent.OnClickNextLevel();
		else if (_controller != null && _controller.HasClaimedWinReward(_controller.Data) && !_controller.TryLoadNextLevel())
			_controller.TryReturnHome();
	}
	public void OnClickReplayLevel() { if (_controller != null) _controller.TryReplayLevel(); }
}
