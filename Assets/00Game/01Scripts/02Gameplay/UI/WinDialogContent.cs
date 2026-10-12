using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using Sequence = DG.Tweening.Sequence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui;

public class WinDialogContent : DialogContent
{
	[SerializeField] private Transform _artTitle;
	[SerializeField] private Button _claimButtonCenter;
	[SerializeField] private Button _claimButton;
	[SerializeField] private Button _adButton;
	[SerializeField] private Transform _coinsNode;
	[SerializeField] private Transform _coinsBgLight;
	[SerializeField] private ParticleSystem _coinShineEffect;
	[SerializeField] private ParticleSystem _coinSparkEffect;
	[SerializeField] private SkeletonGraphic _coinsSpine;
	[SerializeField] private Transform _featureProgressNode;
	[SerializeField] private ProgressBar _featureProgressBar;
	[SerializeField] private Image _featureProgressIcon;
	[SerializeField] private ParticleSystem _featureProgressEffect;
	[SerializeField] private Transform _featureProgressIconBackLight;
	[SerializeField] private AudioClip _showAudioClip;
	[SerializeField] private AudioClip _coinAudioClip;
	[SerializeField] private AudioClip _coinCollectAudioClip;
	[SerializeField] private GameObject _flyCoinAsset;

	private GameObject _eventBanner;
	private int _coinCount = 20;
	private float _artTitlePosY;
	private Sequence _seq1;
	private Sequence _seq2;
	private CancellationTokenSource _cancelTokenSource;
	private CancellationToken _token;
	private bool _isCanSkipAnims;
	private bool _isSkippingAnims;
	private bool _isAnimsDone;
	private bool _isEventBannerSetupDone;
	private bool _showRvButton;
	private Button _activeClaimButton;
	private int _featureProgressEndCount;
	private int _featureProgressTotalCount;
	private bool _shouldShowFeatureProgress;

	private GameController _controller;
	private GameData _offeredData;
	private bool _canAdvance = true;
	private bool _isClaiming;
	private bool _isNavigating;
	private Text _fallbackTitle;
	private Button _fallbackNextButton;
	private Button _fallbackReplayButton;
	private readonly List<Sequence> _doubleCoinTweens = new List<Sequence>();

	protected override void Awake()
	{
		base.Awake();
		_cancelTokenSource = new CancellationTokenSource();
		_token = _cancelTokenSource.Token;
		if (_artTitle != null)
		{
			_artTitlePosY = _artTitle.localPosition.y;
		}
		InitButtons();
		InitFeatureProgress();
		SetUpEventBannerIfAny().Forget();
	}

	private bool ShouldShowRvBtn()
	{
		int level = _controller != null && _controller.Data != null
			? _controller.Data.GetPuzzleActualLevel()
			: GameProgressStore.NextPlayableLevel;
		return level >= 10;
	}

	private void InitButtons()
	{
		_coinCount = 20;

		_showRvButton = ShouldShowRvBtn();

		if (_claimButton != null) _claimButton.gameObject.SetActive(_showRvButton);
		if (_claimButtonCenter != null) _claimButtonCenter.gameObject.SetActive(!_showRvButton);
		if (_adButton != null) _adButton.gameObject.SetActive(_showRvButton);

		_activeClaimButton = _showRvButton ? _claimButton : _claimButtonCenter;

		if (_activeClaimButton != null)
		{
			SetupButton(_activeClaimButton);
			SetButtonText(_activeClaimButton, _coinCount);
			_activeClaimButton.onClick.RemoveListener(OnClaimButtonClick);
			_activeClaimButton.onClick.AddListener(OnClaimButtonClick);
		}

		if (_showRvButton && _adButton != null)
		{
			SetupButton(_adButton);
			SetButtonText(_adButton, _coinCount * 2);
			_adButton.onClick.RemoveListener(OnAdButtonClick);
			_adButton.onClick.AddListener(OnAdButtonClick);
		}

		if (_fallbackNextButton != null)
		{
			_fallbackNextButton.onClick.RemoveListener(OnClickNextLevel);
			_fallbackNextButton.onClick.AddListener(OnClickNextLevel);
		}
		if (_fallbackReplayButton != null)
		{
			_fallbackReplayButton.onClick.RemoveListener(OnClickDoubleReward);
			_fallbackReplayButton.onClick.AddListener(OnClickDoubleReward);
		}
	}

	private void SetupButton(Button button)
	{
		if (button == null) return;
		CanvasGroup group = button.GetComponent<CanvasGroup>();
		if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
		group.alpha = 0f;
		button.interactable = false;
	}

	private void SetButtonText(Button button, int coinCount)
	{
		if (button == null) return;
		Transform content = HierarchyReferenceCache.Child(button.transform, "Content");
		if (content == null) content = button.transform;

		Snui_Text[] texts = content.GetComponentsInChildren<Snui_Text>(true);
		if (button == _claimButtonCenter && texts.Length > 1)
		{
			texts[1].SetText("+" + coinCount);
		}
		else if (texts.Length > 0)
		{
			texts[texts.Length - 1].SetText("+" + coinCount);
		}
		else
		{
			Transform textTransform = HierarchyReferenceCache.Path(button.transform, "Content", "Snui_Text");
			if (textTransform != null && textTransform.TryGetComponent<Snui_Text>(out var snuiText))
			{
				snuiText.SetText("+" + coinCount);
			}
		}

		TMP_Text[] tmpTexts = content.GetComponentsInChildren<TMP_Text>(true);
		if (button == _claimButtonCenter && tmpTexts.Length > 1)
		{
			tmpTexts[1].text = "+" + coinCount;
		}
		else if (tmpTexts.Length > 0 && button != _claimButtonCenter)
		{
			tmpTexts[tmpTexts.Length - 1].text = "+" + coinCount;
		}
	}

	private void InitFeatureProgress()
	{
		_shouldShowFeatureProgress = false;
		if (_featureProgressNode != null) _featureProgressNode.gameObject.SetActive(false);
	}

	private async UniTaskVoid SetUpEventBannerIfAny()
	{
		await UniTask.Yield();
		_isEventBannerSetupDone = true;
	}

	private async UniTask ShowEventBannerAsync()
	{
		await UniTask.Yield();
	}

	private void PlayBannerFadeOutAnim()
	{
		if (_eventBanner != null)
		{
			CanvasGroup cg = _eventBanner.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
		}
	}

	public override void Show()
	{
		_result = RESULT_UNKNOWN;
		_isClaiming = false;
		_isNavigating = false;
		_isSkippingAnims = false;
		_isCanSkipAnims = false;
		_isAnimsDone = false;
		_offeredData = _controller == null ? null : _controller.Data;

		_cancelTokenSource?.Cancel();
		_cancelTokenSource?.Dispose();
		_cancelTokenSource = new CancellationTokenSource();
		_token = _cancelTokenSource.Token;

		gameObject.SetActive(true);
		base.Show();

		if (HudContainer.Current != null) HudContainer.Current.gameObject.SetActive(false);

		InitButtons();
		HideEffects();

		if (_artTitle != null) _artTitle.gameObject.SetActive(false);
		if (_coinsNode != null) _coinsNode.gameObject.SetActive(true);

		PlayAnim().Forget();
	}

	public async UniTaskVoid PlayAnim()
	{
		if (_isSkippingAnims) return;
		CancellationToken token = _token;

		_isCanSkipAnims = true;
		_isAnimsDone = false;

		if (_artTitle != null)
		{
			CanvasGroup titleGroup = _artTitle.GetComponent<CanvasGroup>();
			if (titleGroup == null) titleGroup = _artTitle.gameObject.AddComponent<CanvasGroup>();
			titleGroup.alpha = 0f;
			_artTitle.localPosition = new Vector3(_artTitle.localPosition.x, _artTitlePosY + 300f, _artTitle.localPosition.z);
			_artTitle.localScale = Vector3.one * 0.8f;
			_artTitle.gameObject.SetActive(true);

			_seq1 = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
			_seq1.Append(_artTitle.DOScale(1.3f, 0.13f).SetEase(Ease.OutSine));
			_seq1.Join(titleGroup.DOFade(1f, 0.13f).SetEase(Ease.OutSine));
			_seq1.Append(_artTitle.DOScale(1.18f, 0.13f).SetEase(Ease.InSine));
			_seq1.Append(_artTitle.DOScale(1.24f, 0.13f).SetEase(Ease.OutSine));

			if (await UniTask.Delay(TimeSpan.FromSeconds(0.4f), cancellationToken: token).SuppressCancellationThrow()) return;
			if (_isSkippingAnims) return;

			_seq2 = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
			_seq2.Append(_artTitle.DOLocalMoveY(_artTitlePosY - 6f, 0.2f).SetEase(Ease.InSine));
			_seq2.Join(_artTitle.DOScale(0.96f, 0.2f).SetEase(Ease.InCubic));
			_seq2.Append(_artTitle.DOLocalMoveY(_artTitlePosY, 0.27f).SetEase(Ease.OutBack));
			_seq2.Join(_artTitle.DOScale(1.0f, 0.27f).SetEase(Ease.OutBack));

			if (await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: token).SuppressCancellationThrow()) return;
			if (_isSkippingAnims) return;
		}

		_isCanSkipAnims = false;

		if (AudioManager.Current != null)
		{
			AudioManager.Current.PlayOneShot(_showAudioClip, gameObject);
		}

		if (_coinsBgLight != null)
		{
			_coinsBgLight.gameObject.SetActive(true);
			_coinsBgLight.DORotate(new Vector3(0, 0, 360f), 8f, RotateMode.FastBeyond360)
				.SetEase(Ease.Linear)
				.SetLoops(-1)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		PlayCoinsOpenAnimation();

		if (_coinShineEffect != null)
		{
			var main = _coinShineEffect.main;
			main.useUnscaledTime = true;
			_coinShineEffect.Play(true);
		}
		if (_coinSparkEffect != null)
		{
			var main = _coinSparkEffect.main;
			main.useUnscaledTime = true;
			_coinSparkEffect.Play(true);
		}

		if (await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: token).SuppressCancellationThrow()) return;
		if (_isSkippingAnims) return;

		if (_activeClaimButton != null)
		{
			CanvasGroup cg = _activeClaimButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(1f, 0.13f).SetUpdate(true).SetLink(gameObject);
			_activeClaimButton.interactable = true;
		}

		if (_showRvButton && _adButton != null)
		{
			CanvasGroup cg = _adButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(1f, 0.13f).SetUpdate(true).SetLink(gameObject);
			_adButton.interactable = true;
		}

		if (_shouldShowFeatureProgress && _featureProgressNode != null)
		{
			CanvasGroup cg = _featureProgressNode.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(1f, 0.13f).SetUpdate(true).SetLink(gameObject);
		}

		await ShowEventBannerAsync();
		if (token.IsCancellationRequested) return;

		if (await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: token).SuppressCancellationThrow()) return;
		SetAnimsDoneState();
	}

	private void PlayCoinsOpenAnimation()
	{
		if (_coinsSpine == null) return;
		_coinsSpine.gameObject.SetActive(true);
		_coinsSpine.UnscaledTime = true;
		_coinsSpine.Initialize(false);
		if (_coinsSpine.AnimationState == null || _coinsSpine.Skeleton == null) return;
		_coinsSpine.AnimationState.ClearTracks();
		// Claim animations leave slots/bones hidden; ClearTracks does not reset their pose.
		_coinsSpine.Skeleton.SetToSetupPose();
		_coinsSpine.AnimationState.SetAnimation(0, "open", false);
		_coinsSpine.Update(0f);
		_coinsSpine.UpdateMesh();
	}

	public void OnSkipAnims()
	{
		if (!_isCanSkipAnims || _isSkippingAnims || _isAnimsDone) return;
		OnSkipAnimsAsync().Forget();
	}

	private async UniTaskVoid OnSkipAnimsAsync()
	{
		_isSkippingAnims = true;
		_cancelTokenSource?.Cancel();
		_cancelTokenSource?.Dispose();
		_cancelTokenSource = new CancellationTokenSource();
		_token = _cancelTokenSource.Token;
		_seq1?.Kill();
		_seq2?.Kill();
		await PlaySkipAnims();
	}

	private async UniTask PlaySkipAnims()
	{
		CancellationToken token = _token;
		if (_artTitle != null)
		{
			_artTitle.localPosition = new Vector3(_artTitle.localPosition.x, _artTitlePosY, _artTitle.localPosition.z);
			_artTitle.localScale = Vector3.one;
			CanvasGroup titleGroup = _artTitle.GetComponent<CanvasGroup>();
			if (titleGroup != null) titleGroup.alpha = 1f;
			_artTitle.gameObject.SetActive(true);
		}

		if (AudioManager.Current != null)
		{
			AudioManager.Current.PlayOneShot(_showAudioClip, gameObject);
		}

		if (_coinsBgLight != null)
		{
			_coinsBgLight.gameObject.SetActive(true);
			_coinsBgLight.DORotate(new Vector3(0, 0, 360f), 8f, RotateMode.FastBeyond360)
				.SetEase(Ease.Linear)
				.SetLoops(-1)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		PlayCoinsOpenAnimation();

		if (_coinShineEffect != null) _coinShineEffect.Play(true);
		if (_coinSparkEffect != null) _coinSparkEffect.Play(true);

		if (_activeClaimButton != null)
		{
			CanvasGroup cg = _activeClaimButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.alpha = 1f;
			_activeClaimButton.interactable = true;
		}

		if (_showRvButton && _adButton != null)
		{
			CanvasGroup cg = _adButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.alpha = 1f;
			_adButton.interactable = true;
		}

		await ShowEventBannerAsync();
		if (token.IsCancellationRequested) return;
		SetAnimsDoneState();
	}

	private void SetAnimsDoneState()
	{
		_isAnimsDone = true;
		_isSkippingAnims = false;
		_isCanSkipAnims = false;
	}

	public void OnClaimButtonClick()
	{
		if (_isClaiming || _isNavigating) return;
		_isClaiming = true;
		if (_activeClaimButton != null) _activeClaimButton.interactable = false;
		if (_adButton != null) _adButton.interactable = false;
		RewardCoins(false, "WinDialog").Forget();
	}

	public void OnAdButtonClick()
	{
		if (_isClaiming || _isNavigating) return;
		_isClaiming = true;
		if (_activeClaimButton != null) _activeClaimButton.interactable = false;
		if (_adButton != null) _adButton.interactable = false;

		RewardCoins(true, "WinDialogRv").Forget();
	}

	private async UniTask RewardCoins(bool isDouble, string way)
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		Transform parent = canvas != null ? canvas.rootCanvas.transform : transform.parent;
		await HudContainer.Show(parent);
		if (this == null || _token.IsCancellationRequested || !isActiveAndEnabled) return;
		HudContainer.TrySetTop(1100);
		HudContainer.Current?.BeginCoinClaim(canvas);
		GetComponentInParent<WinDialog>(true)?.BeginCoinClaim();
		HudContainer.Current?.DisableClick();
		Snui_CoinHud.TrySetAutoUpdate(false);

		if (_activeClaimButton != null)
		{
			CanvasGroup cg = _activeClaimButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
		}
		if (_showRvButton && _adButton != null)
		{
			CanvasGroup cg = _adButton.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
		}
		if (_artTitle != null)
		{
			CanvasGroup cg = _artTitle.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
		}
		PlayBannerFadeOutAnim();
		if (_shouldShowFeatureProgress && _featureProgressNode != null)
		{
			CanvasGroup cg = _featureProgressNode.GetComponent<CanvasGroup>();
			if (cg != null) cg.DOFade(0f, 0.2f).SetUpdate(true).SetLink(gameObject);
		}

		int coinsToGive = isDouble ? _coinCount * 2 : _coinCount;
		GameData data = _offeredData ?? (_controller != null ? _controller.Data : null);
		if (_controller != null && data != null)
		{
			_controller.TryClaimWinReward(data, isDouble, out _);
		}
		else
		{
			InventoryManager.Ensure()?.Add(InventoryKeys.COIN, coinsToGive, way);
		}

		if (isDouble)
		{
			PlayDoubleCoinAnim();
		}
		else
		{
			PlayCommonCoinAnim(_coinCount);
		}
	}

	private void PlayCommonCoinAnim(int coinCount)
	{
		HideEffects();
		if (_coinsSpine != null && _coinsSpine.AnimationState != null)
		{
			_coinsSpine.AnimationState.ClearTracks();
			_coinsSpine.AnimationState.SetAnimation(0, "claim_direct", false);
		}

		Vector2 startPos = _coinsSpine != null ? (Vector2)_coinsSpine.transform.position : (Vector2)transform.position;
		ClaimCoinAnim.Play(coinCount, startPos, () =>
		{
			FinishCoinClaim().Forget();
		}).Forget();
	}

	private async UniTask FinishCoinClaim()
	{
		bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(0.4f), ignoreTimeScale: true,
			cancellationToken: _token).SuppressCancellationThrow();
		if (!cancelled && this != null && isActiveAndEnabled) Dismiss(RESULT_CLOSE);
	}

	private void ReleaseClaimHud()
	{
		// The outer dialog owns the mask and releases sorting when that mask is removed.
		if (GetComponentInParent<WinDialog>(true) == null) HudContainer.TryRevertToOriginLayer();
	}

	private void PlayDoubleCoinAnim()
	{
		if (AudioManager.Current != null)
		{
			AudioManager.Current.PlayOneShot(_coinAudioClip, gameObject);
		}

		if (_coinsSpine != null && _coinsSpine.AnimationState != null)
		{
			_coinsSpine.AnimationState.ClearTracks();
			_coinsSpine.AnimationState.SetAnimation(0, "claim_double", false);
			bool triggered = false;
			_coinsSpine.AnimationState.Event += (entry, e) =>
			{
				if (!triggered && e != null && e.Data != null && !string.IsNullOrEmpty(e.Data.Name))
				{
					triggered = true;
					PlayDoubleCoinAnimAsync().Forget();
				}
			};
			DOVirtual.DelayedCall(0.5f, () =>
			{
				if (!triggered)
				{
					triggered = true;
					PlayDoubleCoinAnimAsync().Forget();
				}
			}).SetUpdate(true).SetLink(gameObject);
		}
		else
		{
			PlayDoubleCoinAnimAsync().Forget();
		}
	}

	private async UniTaskVoid PlayDoubleCoinAnimAsync()
	{
		HideEffects();
		ClearDoubleCoinTweens();

		Canvas canvas = GetComponentInParent<Canvas>();
		Transform parent = canvas != null ? canvas.rootCanvas.transform : transform;
		Vector3 startPos = _coinsSpine != null ? _coinsSpine.transform.position : transform.position;
		Vector3 targetPos = Snui_CoinHud.Current != null
			? Snui_CoinHud.Current.GetIconPos()
			: new Vector3(Screen.width * 0.15f, Screen.height * 0.92f, 0f);

		GameObject flightRoot = new GameObject("DoubleCoinFlightRoot", typeof(RectTransform), typeof(Canvas));
		flightRoot.transform.SetParent(parent, false);
		Canvas flightCanvas = flightRoot.GetComponent<Canvas>();
		flightCanvas.overrideSorting = true;
		flightCanvas.sortingLayerID = canvas != null ? canvas.sortingLayerID : 0;
		Canvas hudCanvas = Snui_CoinHud.Current == null ? null : Snui_CoinHud.Current.GetComponent<Canvas>();
		if (hudCanvas != null) flightCanvas.sortingLayerID = hudCanvas.sortingLayerID;
		flightCanvas.sortingOrder = hudCanvas != null ? hudCanvas.sortingOrder + 1 : 1101;

		GameObject coinPrefab = _flyCoinAsset;
		if (coinPrefab == null && PrefabLibrary.Current != null)
			coinPrefab = PrefabLibrary.Current.FlyCoinPrefab;

		Sprite[] sprites = null;
		if (coinPrefab != null)
			sprites = await FlyCoin.LoadSprites(coinPrefab);

		List<GameObject> spawnedCoins = new List<GameObject>();
		const int totalCoins = 20;

		for (int i = 0; i < totalCoins; i++)
		{
			if (coinPrefab == null) break;
			GameObject coin = Instantiate(coinPrefab, flightRoot.transform, false);
			coin.SetActive(true);
			foreach (var img in coin.GetComponentsInChildren<Graphic>(true)) img.raycastTarget = false;

			FlyCoin fly = coin.GetComponent<FlyCoin>();
			if (fly != null && sprites != null)
			{
				fly.SetSprites(sprites);
				fly.Play(true);
			}

			coin.transform.position = startPos;
			coin.transform.localScale = Vector3.zero;
			spawnedCoins.Add(coin);

			float angle = (i / (float)totalCoins) * Mathf.PI * 2f;
			float radius = UnityEngine.Random.Range(100f, 160f);
			Vector3 burstOffset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

			Sequence burst = DOTween.Sequence().SetUpdate(true).SetLink(coin);
			burst.Append(coin.transform.DOScale(UnityEngine.Random.Range(0.7f, 1.0f), 0.53f).SetEase(Ease.OutBack));
			burst.Join(coin.transform.DOMove(startPos + burstOffset, 0.53f).SetEase(Ease.OutQuad));
			_doubleCoinTweens.Add(burst);
		}

		await UniTask.Delay(TimeSpan.FromSeconds(0.56f), cancellationToken: _token).SuppressCancellationThrow();

		List<UniTask> flightTasks = new List<UniTask>();
		for (int i = 0; i < spawnedCoins.Count; i++)
		{
			GameObject coin = spawnedCoins[i];
			if (coin == null) continue;
			float delay = i * 0.02f;
			Sequence flight = DOTween.Sequence().SetUpdate(true).SetLink(coin);
			flight.AppendInterval(delay);
			flight.Append(coin.transform.DOMove(targetPos, 0.45f).SetEase(Ease.InQuad));
			flight.Join(coin.transform.DOScale(0.5f, 0.45f).SetEase(Ease.InCubic));
			flight.OnComplete(() =>
			{
				if (coin != null) Destroy(coin);
			});
			_doubleCoinTweens.Add(flight);
			flightTasks.Add(flight.ToUniTask().AttachExternalCancellation(_token));
		}

		if (flightTasks.Count > 0)
		{
			try
			{
				await UniTask.WhenAll(flightTasks);
			}
			catch (OperationCanceledException)
			{
			}
		}

		ClearDoubleCoinTweens();
		if (flightRoot != null) Destroy(flightRoot);

		if (Snui_CoinHud.Current != null)
		{
			Snui_CoinHud.Current.SetAutoUpdate(true);
			Snui_CoinHud.Current.PlaySparks();
			Snui_CoinHud.Current.PlayShakeAnimation();
		}
		if (_coinCollectAudioClip != null && AudioManager.Current != null)
		{
			AudioManager.Current.PlayOneShot(_coinCollectAudioClip, gameObject);
		}

		await FinishCoinClaim();
	}

	private void ClearDoubleCoinTweens()
	{
		foreach (var tween in _doubleCoinTweens) tween?.Kill();
		_doubleCoinTweens.Clear();
	}

	private void HideEffects()
	{
		if (_coinSparkEffect != null) _coinSparkEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_coinShineEffect != null) _coinShineEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
		if (_coinsBgLight != null)
		{
			_coinsBgLight.DOKill();
			_coinsBgLight.gameObject.SetActive(false);
		}
	}

	public override void Dismiss(int result = RESULT_CLOSE)
	{
		_isClaiming = false;
		ClearDoubleCoinTweens();
		ReleaseClaimHud();
		base.Dismiss(result);
	}

	public override void Hide()
	{
		_cancelTokenSource?.Cancel();
		_isClaiming = false;
		_seq1?.Kill();
		_seq2?.Kill();
		ClearDoubleCoinTweens();
		HideEffects();
		ReleaseClaimHud();
		base.Hide();
	}

	private void OnDisable()
	{
		_cancelTokenSource?.Cancel();
		_seq1?.Kill();
		_seq2?.Kill();
		ClearDoubleCoinTweens();
		HideEffects();
		ReleaseClaimHud();
	}

	private void OnDestroy()
	{
		_cancelTokenSource?.Cancel();
		_cancelTokenSource?.Dispose();
		_seq1?.Kill();
		_seq2?.Kill();
		ClearDoubleCoinTweens();
		ReleaseClaimHud();
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
	}

	private void OnLevelStarted(int level)
	{
		Hide();
		_offeredData = null;
	}

	public void Bind(GameController controller)
	{
		if (_controller != null) _controller.LevelStarted -= OnLevelStarted;
		_controller = controller;
		if (_controller != null) _controller.LevelStarted += OnLevelStarted;
		InitButtons();
	}

	public void Bind(GameController controller, Text title, Button nextButton, Button replayButton)
	{
		_fallbackTitle = title;
		_fallbackNextButton = nextButton;
		_fallbackReplayButton = replayButton;
		Bind(controller);
	}

	public void SetLevel(int level, bool canLoadNext)
	{
		_canAdvance = canLoadNext;
		if (_fallbackTitle != null)
			_fallbackTitle.text = GameProgressStore.IsCampaignComplete && !canLoadNext ? "ALL LEVELS COMPLETE" : "LEVEL " + level + " COMPLETE";
		InitButtons();
	}

	public void SetNoMoreLevels()
	{
		_canAdvance = false;
		if (_fallbackTitle != null)
			_fallbackTitle.text = GameProgressStore.IsCampaignComplete ? "ALL LEVELS COMPLETE" : "LEVEL COMPLETE";
		InitButtons();
	}

	public void OnClickNextLevel()
	{
		OnClaimButtonClick();
	}

	public void OnClickDoubleReward()
	{
		OnAdButtonClick();
	}

	public void AllowNavigationRetry()
	{
		_isNavigating = false;
		_isClaiming = false;
		if (_activeClaimButton != null) _activeClaimButton.interactable = true;
		if (_showRvButton && _adButton != null) _adButton.interactable = true;
	}

	public void OnClickReplayLevel()
	{
		if (_controller != null) _controller.TryReplayLevel();
	}
}
