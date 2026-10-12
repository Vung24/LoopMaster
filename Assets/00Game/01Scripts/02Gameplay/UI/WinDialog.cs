using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

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
	private SkeletonGraphic[] _fireworkGraphics;
	private ParticleSystem[] _fireworkParticles;
	private bool _effectsPrepared;
	public bool IsEnteringHome { get; private set; }

	private void Awake() { ResolveReferences(); }

	public void Bind(GameController controller)
	{
		_controller = controller;
		ResolveReferences();
		if (_winDialogContent != null) _winDialogContent.Bind(controller);
		PrepareEffects();
	}

	private void PrepareEffects()
	{
		if (_effectsPrepared) return;
		_effectsPrepared = true;
		Snui.Snui_Text.EnsureFontAssets(transform);
		var graphics = new System.Collections.Generic.List<SkeletonGraphic>();
		var particles = new System.Collections.Generic.List<ParticleSystem>();
		if (_fireworkLeft != null)
		{
			graphics.AddRange(_fireworkLeft.GetComponentsInChildren<SkeletonGraphic>(true));
			particles.AddRange(_fireworkLeft.GetComponentsInChildren<ParticleSystem>(true));
		}
		if (_fireworkRight != null)
		{
			graphics.AddRange(_fireworkRight.GetComponentsInChildren<SkeletonGraphic>(true));
			particles.AddRange(_fireworkRight.GetComponentsInChildren<ParticleSystem>(true));
		}
		_fireworkGraphics = graphics.ToArray();
		_fireworkParticles = particles.ToArray();
		foreach (SkeletonGraphic graphic in _fireworkGraphics)
		{
			graphic.UnscaledTime = true;
			graphic.Initialize(false);
		}
		foreach (ParticleSystem effect in _fireworkParticles)
		{
			var main = effect.main;
			main.useUnscaledTime = true;
			// The dialog owns visibility. A child finishing must not disable the emitter root.
			main.stopAction = ParticleSystemStopAction.None;
			effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
		if (_winPopperCheerClip != null && _winPopperCheerClip.loadState == AudioDataLoadState.Unloaded)
			_winPopperCheerClip.LoadAudioData();
		StopFireworks();
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
		PrepareEffects();
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
		PlayFireworks();
		if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(_winPopperCheerClip, gameObject);
		if (_logoDialogContent != null)
		{
			yield return new WaitForSecondsRealtime(0.8f);
			if (_mask != null) _maskFade = _mask.DOFade(0.98f, 0.1f).SetUpdate(true);
			_canSkip = true;
			_logoDialogContent.PlayLogoAnimation();
			while (_logoDialogContent != null && _logoDialogContent.gameObject.activeSelf) yield return null;
		}
		else
		{
			SetMaskAlpha(0.98f);
			_canSkip = true;
		}
		if (_winDialogContent != null)
		{
			_winDialogContent.Show();
			while (_winDialogContent != null && _winDialogContent.gameObject.activeSelf) yield return null;
		}
		_presentation = null;

		EnterNextScene();
	}

	public void Hide()
	{
		CancelPresentation();
		if (_logoDialogContent != null) _logoDialogContent.Hide();
		if (_winDialogContent != null) _winDialogContent.Hide();
		StopFireworks();
		if (_canvasGroup != null)
		{
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}
		HudContainer.TryRevertToOriginLayer();
		gameObject.SetActive(false);
	}

	public void BeginCoinClaim()
	{
		_maskFade?.Kill();
		if (_mask != null) _maskFade = _mask.DOFade(0.85f, 0.15f).SetUpdate(true).SetLink(gameObject);
	}

	private void PlayFireworks()
	{
		if (_fireworkLeft != null) _fireworkLeft.gameObject.SetActive(true);
		if (_fireworkRight != null) _fireworkRight.gameObject.SetActive(true);
		foreach (SkeletonGraphic graphic in _fireworkGraphics)
		{
			if (graphic == null) continue;
			var data = graphic.skeletonDataAsset == null ? null : graphic.skeletonDataAsset.GetSkeletonData(false);
			if (data == null || data.FindAnimation("animation") == null || graphic.AnimationState == null) continue;
			graphic.Skeleton.SetToSetupPose();
			graphic.AnimationState.ClearTracks();
			graphic.AnimationState.SetAnimation(0, "animation", false);
		}
		foreach (ParticleSystem effect in _fireworkParticles)
		{
			if (effect == null) continue;
			effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
			effect.Play(false);
		}
	}

	private void StopFireworks()
	{
		if (_fireworkParticles != null)
			foreach (ParticleSystem effect in _fireworkParticles)
				if (effect != null) effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_fireworkLeft != null) _fireworkLeft.gameObject.SetActive(false);
		if (_fireworkRight != null) _fireworkRight.gameObject.SetActive(false);
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
		HudContainer.TryRevertToOriginLayer();
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
		else EnterNextScene();
	}

	public bool EnterNextScene()
	{
		if (!isActiveAndEnabled || _controller == null || _controller.EndState != GameEndState.Won ||
			!_controller.HasClaimedWinReward(_controller.Data) || IsEnteringHome || GameSceneManager.OnChangeScene)
			return false;
		if (!_controller.ShouldReturnHomeAfterWin) return _controller.TryLoadNextLevel();
		IsEnteringHome = true;
		FadeOutAndEnterHome().Forget();
		return true;
	}

	private async UniTaskVoid FadeOutAndEnterHome()
	{
		CancelPresentation();
		Transform previousParent = transform.parent;
		transform.SetParent(null, false);
		Canvas canvas = GetComponent<Canvas>();
		bool addedCanvas = canvas == null;
		if (addedCanvas) canvas = gameObject.AddComponent<Canvas>();
		RenderMode previousMode = canvas.renderMode;
		int previousOrder = canvas.sortingOrder;
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 9999;
		CanvasScaler scaler = GetComponent<CanvasScaler>();
		bool addedScaler = scaler == null;
		if (addedScaler)
		{
			scaler = gameObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(750f, 1624f);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
		}
		GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
		bool addedRaycaster = raycaster == null;
		if (addedRaycaster) raycaster = gameObject.AddComponent<GraphicRaycaster>();
		Snui.SafeArea safeArea = _safeArea == null ? null : _safeArea.GetComponent<Snui.SafeArea>();
		bool safeAreaEnabled = safeArea != null && safeArea.enabled;
		if (safeArea != null) safeArea.enabled = false;
		SetMaskAlpha(254f / 255f);
		LoadingPanel panel = gameObject.AddComponent<LoadingPanel>();
		if (await _controller.ReturnHomeAsync(withAnimation: false) || this == null) return;

		if (LoadingPanel.Current == panel) LoadingPanel.Current = null;
		Destroy(panel);
		if (previousParent != null)
		{
			UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, previousParent.gameObject.scene);
			transform.SetParent(previousParent, false);
		}
		if (addedRaycaster) Destroy(raycaster);
		if (addedScaler) Destroy(scaler);
		if (addedCanvas) Destroy(canvas);
		else { canvas.renderMode = previousMode; canvas.sortingOrder = previousOrder; }
		if (safeArea != null) safeArea.enabled = safeAreaEnabled;
		if (_canvasGroup != null) _canvasGroup.alpha = 1f;
		IsEnteringHome = false;
		_winDialogContent?.AllowNavigationRetry();
	}
	public void OnClickReplayLevel() { if (_controller != null) _controller.TryReplayLevel(); }
}
