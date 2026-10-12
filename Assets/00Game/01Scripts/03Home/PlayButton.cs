using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayButton : MonoBehaviour
{
	[SerializeField] private Animation _animation;
	[SerializeField] private CanvasGroup _canvasGroup;
	[SerializeField] private Button _btn;
	[SerializeField] private TMP_Text _btnText;
	[SerializeField] private TMP_Text _hardText;
	[SerializeField] private Transform _unlimitedTip;
	[SerializeField] private TMP_Text _unlimitedTipTime;
	[SerializeField] private Snui.Snui_Button _snuiButton;
	[SerializeField] private SpriteState PlayBtnNormalSpriteState;
	[SerializeField] private SpriteState PlayBtnHardSpriteState;
	[SerializeField] private SpriteState PlayBtnVeryHardSpriteState;
	[SerializeField] private Transform _hardTagNode;
	[SerializeField] private Sprite _hardCoinTagSp;
	[SerializeField] private Sprite _veryHardCoinTagSp;
	[SerializeField] private SkeletonGraphic _levelChangeSpine;
	[SerializeField] private SkeletonGraphic _levelLightSpine;
	[SerializeField] private AudioClip _levelChangeAudioClip;

	private bool _sweepAnimDone;
	private HardLevel _hardLevel;
	private HardLevel _targetHardLevel;
	private bool IsPlayLevelChangeAnim;
	private HomePage _homePage;
	private Color _normalOutlineColor = Color.black;
	private CancellationTokenSource _sweepCts;
	private bool _started;
	private CancellationTokenSource _clickCts;

	private void Awake()
	{
		if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
		if (_btn == null) _btn = GetComponent<Button>();
		if (_animation == null) _animation = GetComponent<Animation>();
		_homePage = HomeController.Current != null ? HomeController.Current.HomePage : GetComponentInParent<HomePage>(true);
		if (_btnText != null)
		{
			var realtime = _btnText.GetComponent<TMPSetRealtime>();
			if (realtime != null)
			{
				_normalOutlineColor = realtime.OutlineColor;
			}
		}
	}

	public void Bind(HomePage home)
	{
		_homePage = home;
	}

	private void Start()
	{
		_started = true;
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 1f;
		}

		if (_btn != null)
		{
			_btn.onClick.AddListener(() => OnClick(false).Forget());
		}

		RefreshLevelLabel();

		if (_unlimitedTipTime != null)
		{
			var countdown = _unlimitedTipTime.GetComponent<CountdownHelper>();
			if (countdown != null)
			{
				countdown.OnTimerTick.AddListener(OnUnlimitedTick);
				countdown.OnTimerStoped.AddListener(OnUnlimitedStop);
			}
		}

		if (Managers.Heart != null)
		{
			Managers.Heart.OnInfinityHeartChanged.AddListener(OnHeartHudChanged);
			Managers.Heart.OnHeartChanged.AddListener(OnHeartHudChanged);
		}

		InitAsync().Forget();
	}

	public async UniTask InitAsync()
	{
		UpdateUnlimitedTip();
		if (Managers.PuzzleStore != null)
		{
			_hardLevel = await Managers.PuzzleStore.GetCurHardLevel();
		}
		else
		{
			_hardLevel = HardLevel.Normal;
		}
		if (this == null) return;
		_targetHardLevel = _hardLevel;
		RefreshLevelLabel();

		if (!IsPlayLevelChangeAnim)
		{
			UpdateStyle(HomeLevelChangePresentation.HasPending ? HomeLevelChangePresentation.PreviousHardLevel : _targetHardLevel);
		}
		if (isActiveAndEnabled) PlaySweepAnim().Forget();
	}

	public void PlayEnterAnim()
	{
		if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 1f;
		}

		if (_animation != null)
		{
			_animation.Rewind();
			_animation.Play();
		}
	}

	public void PlayShakeAnim()
	{
		Transform parentTrans = transform.parent;
		if (parentTrans != null)
		{
			var sparksTrans = HierarchyReferenceCache.Child(parentTrans, "BtnSparks") ?? HierarchyReferenceCache.Descendant(parentTrans, "BtnSparks");
			if (sparksTrans != null)
			{
				var ps = sparksTrans.GetComponent<ParticleSystem>();
				if (ps != null)
				{
					ps.gameObject.SetActive(true);
					ps.Play();
				}
			}
		}

		transform.DOKill();
		Sequence seq = DOTween.Sequence();
		seq.Append(transform.DOScale(0.9f, 0.06f).SetEase(Ease.InOutSine));
		seq.Append(transform.DOScale(1.12f, 0.1f).SetEase(Ease.OutSine));
		seq.Append(transform.DOScale(1.0f, 0.13f).SetEase(Ease.InOutSine));
		seq.SetLink(gameObject);

		UpdateUnlimitedTip();
	}

	public async UniTaskVoid PlaySweepAnim()
	{
		if (_sweepCts != null || !isActiveAndEnabled || _levelLightSpine == null) return;
		var cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
		_sweepCts = cts;
		_sweepAnimDone = false;
		try
		{
			while (this != null && isActiveAndEnabled && !_sweepAnimDone && !cts.IsCancellationRequested)
			{
				string animName = _hardLevel == HardLevel.VeryHard ? "expert" :
					_hardLevel == HardLevel.Hard ? "hard" : "normal";
				_levelLightSpine.gameObject.SetActive(true);
				_levelLightSpine.Initialize(true);
				if (_levelLightSpine.AnimationState != null)
				{
					_levelLightSpine.AnimationState.SetAnimation(0, animName, false);
				}
				await UniTask.WaitForSeconds(5f, cancellationToken: cts.Token);
			}
		}
		catch (OperationCanceledException) { }
		finally
		{
			if (_sweepCts == cts) _sweepCts = null;
			cts.Dispose();
		}
	}

	private async UniTask PlayClickFeedback(float pressDuration = 0.08f)
	{
		if (_btn == null || EventSystem.current == null || !_btn.interactable || !_btn.enabled)
		{
			return;
		}

		var eventData = new PointerEventData(EventSystem.current)
		{
			pointerPress = _btn.gameObject
		};
		ExecuteEvents.Execute(_btn.gameObject, eventData, ExecuteEvents.pointerDownHandler);
		bool isCanceled = await UniTask.WaitForSeconds(pressDuration).SuppressCancellationThrow();
		if (!isCanceled && _btn != null)
		{
			ExecuteEvents.Execute(_btn.gameObject, eventData, ExecuteEvents.pointerUpHandler);
		}
	}

	public void TriggerSkipAnimationsClick()
	{
		if (_btn != null && (!_btn.IsInteractable() || !_btn.enabled)) return;
		PlayClickFeedback(0.08f).Forget();
		OnClick(true).Forget();
	}

	public async UniTask PlayChangeLevelAnim(CancellationToken cancellationToken = default)
	{
		if (IsPlayLevelChangeAnim) return;
		IsPlayLevelChangeAnim = true;
		int targetLevel = HomeLevelChangePresentation.TargetLevel;
		Sequence seq = null;
		try
		{
			transform.DOKill();
			seq = DOTween.Sequence();
			seq.Append(transform.DOScale(0.94f, 0.1f).SetEase(Ease.InQuad));
			seq.Append(transform.DOScale(1.2f, 0.23f).SetEase(Ease.OutQuart));
			seq.AppendCallback(() =>
			{
				if (_levelChangeAudioClip != null && AudioManager.Current != null)
				{
					AudioManager.Current.PlayOneShot(_levelChangeAudioClip);
				}
			});
			seq.Append(transform.DOScale(0.94f, 0.17f).SetEase(Ease.InCubic));
			seq.AppendCallback(() =>
			{
				HomeLevelChangePresentation.Consume(targetLevel);
				RefreshLevelLabel();
				if (_levelChangeSpine != null)
				{
					_levelChangeSpine.gameObject.SetActive(true);
					_levelChangeSpine.Initialize(false);
					_levelChangeSpine.AnimationState?.SetAnimation(0, "animation", false);
				}
				UpdateStyle(_targetHardLevel);
			});
			seq.Append(transform.DOScale(1.0f, 0.17f).SetEase(Ease.OutSine));
			seq.SetLink(gameObject, LinkBehaviour.KillOnDisable);
			await seq.ToUniTask().AttachExternalCancellation(cancellationToken);
		}
		finally
		{
			seq?.Kill();
			IsPlayLevelChangeAnim = false;
			if (this != null) transform.localScale = Vector3.one;
		}
	}

	private void OnHeartHudChanged()
	{
		UpdateUnlimitedTip();
	}

	private async UniTaskVoid OnClick(bool skipAnimations = false)
	{
		if (_clickCts != null || !isActiveAndEnabled || (_btn != null && !_btn.enabled)
			|| (_homePage != null && _homePage.IsEntryBlocked)) return;
		if (skipAnimations && _homePage != null && _homePage.IsSkipAnimationsBlocked())
		{
			return;
		}

		var cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
		_clickCts = cts;
		try
		{
			if (Managers.Heart != null && !Managers.Heart.IsHeartEnough())
			{
				var dialog = await MoreHeartDialogContent.ShowInDialog(JumpHeartDialogSourceScene.HomeBoosterIn)
					.AttachExternalCancellation(cts.Token);
				if (dialog == null) return;
				var closed = new UniTaskCompletionSource();
				void OnClosed() => closed.TrySetResult();
				dialog.Closed += OnClosed;
				try { await closed.Task.AttachExternalCancellation(cts.Token); }
				finally { if (dialog != null) dialog.Closed -= OnClosed; }
			}
			cts.Token.ThrowIfCancellationRequested();
			if (Managers.Heart == null || Managers.Heart.IsHeartEnough())
				await EnterGame(skipAnimations, cts.Token);
		}
		catch (OperationCanceledException) { }
		finally
		{
			if (_clickCts == cts) _clickCts = null;
			cts.Dispose();
		}
	}

	private async UniTask EnterGame(bool skipAnimations, CancellationToken token)
	{
		if (skipAnimations && _homePage != null)
		{
			if (!_homePage.CommitProgressBeforeEnterGame())
			{
				return;
			}
			await UniTask.WaitForSeconds(0.2f, cancellationToken: token);
		}
		if (!isActiveAndEnabled) return;
		bool entered = _homePage != null ? _homePage.TryPlay() :
			HomeController.Current != null && HomeController.Current.TryEnterGame();
		if (!entered) return;

		_sweepAnimDone = true;
		if (AudioManager.Current != null)
		{
			AudioManager.Current.StopMusic();
		}

	}

	public void RefreshLevelLabel()
	{
		if (_btnText == null) return;
		var catalog = Resources.Load<BuiltinLevelCatalog>(BuiltinLevelCatalog.ResourceName);
		bool complete = GameProgressStore.IsCampaignComplete;
		bool available = !complete && catalog != null && catalog.HasLevel(GameProgressStore.NextPlayableLevel);
		int displayLevel = HomeLevelChangePresentation.HasPending ? HomeLevelChangePresentation.PreviousLevel : GameProgressStore.NextPlayableLevel;
		_btnText.text = complete ? "ALL LEVELS COMPLETE" : available ? $"Level {displayLevel}" : "LEVEL DATA MISSING";
		_btnText.enableAutoSizing = true;
		_btnText.fontSizeMin = 18f;
		if (_btn != null) _btn.interactable = available;
	}

	private void UpdateStyle(HardLevel hardLevel)
	{
		_hardLevel = hardLevel;
		var normalSprite = PlayBtnNormalSpriteState.highlightedSprite ?? PlayBtnNormalSpriteState.pressedSprite;
		var hardSprite = PlayBtnHardSpriteState.highlightedSprite ?? PlayBtnHardSpriteState.pressedSprite;
		var veryHardSprite = PlayBtnVeryHardSpriteState.highlightedSprite ?? PlayBtnVeryHardSpriteState.pressedSprite;

		Color outlineColor;
		var btnImg = _btn != null ? _btn.GetComponent<Image>() : null;
		var btnTextRealtime = _btnText != null ? _btnText.GetComponent<TMPSetRealtime>() : null;
		var hardTextRealtime = _hardText != null ? _hardText.GetComponent<TMPSetRealtime>() : null;

		switch (hardLevel)
		{
			case HardLevel.VeryHard:
				if (_btn != null)
				{
					_btn.spriteState = PlayBtnVeryHardSpriteState;
				}
				if (btnImg != null && veryHardSprite != null)
				{
					btnImg.sprite = veryHardSprite;
				}
				outlineColor = ColorUtils.RGB(0x2b0001);
				if (btnTextRealtime != null) btnTextRealtime.OutlineColor = outlineColor;
				if (hardTextRealtime != null) hardTextRealtime.OutlineColor = outlineColor;
				if (_hardText != null)
				{
					_hardText.text = "SUPER HARD";
					_hardText.color = ColorUtils.FromString("#FFBFB4");
					_hardText.gameObject.SetActive(true);
				}
				if (_hardTagNode != null)
				{
					var tagImage = _hardTagNode.GetComponent<Image>();
					if (tagImage != null && _veryHardCoinTagSp != null) tagImage.sprite = _veryHardCoinTagSp;
					_hardTagNode.gameObject.SetActive(true);
				}
				break;

			case HardLevel.Hard:
				if (_btn != null)
				{
					_btn.spriteState = PlayBtnHardSpriteState;
				}
				if (btnImg != null && hardSprite != null)
				{
					btnImg.sprite = hardSprite;
				}
				outlineColor = ColorUtils.RGB(0x11003c);
				if (btnTextRealtime != null) btnTextRealtime.OutlineColor = outlineColor;
				if (hardTextRealtime != null) hardTextRealtime.OutlineColor = outlineColor;
				if (_hardText != null)
				{
					_hardText.text = "HARD";
					_hardText.color = ColorUtils.FromString("#FFB3F5");
					_hardText.gameObject.SetActive(true);
				}
				if (_hardTagNode != null)
				{
					var tagImage = _hardTagNode.GetComponent<Image>();
					if (tagImage != null && _hardCoinTagSp != null) tagImage.sprite = _hardCoinTagSp;
					_hardTagNode.gameObject.SetActive(true);
				}
				break;

			default:
				if (_btn != null)
				{
					_btn.spriteState = PlayBtnNormalSpriteState;
				}
				if (btnImg != null && normalSprite != null)
				{
					btnImg.sprite = normalSprite;
				}
				if (btnTextRealtime != null) btnTextRealtime.OutlineColor = _normalOutlineColor;
				if (_hardTagNode != null) _hardTagNode.gameObject.SetActive(false);
				if (_hardText != null) _hardText.gameObject.SetActive(false);
				break;
		}

		if (btnTextRealtime != null) btnTextRealtime.ApplyFontSettings();
		if (hardTextRealtime != null) hardTextRealtime.ApplyFontSettings();
	}

	private void UpdateUnlimitedTip()
	{
		int infiniteLeftTime = Managers.Heart != null ? Managers.Heart.InfiniteLeftTime : 0;
		if (_unlimitedTip != null)
		{
			_unlimitedTip.gameObject.SetActive(infiniteLeftTime > 0);
		}
		if (infiniteLeftTime > 0 && _unlimitedTipTime != null)
		{
			var countdown = _unlimitedTipTime.GetComponent<CountdownHelper>();
			if (countdown != null && Managers.Heart != null)
			{
				countdown.CountdownUntil(Managers.Heart.InfiniteHeartEndTime);
			}
		}
	}

	private void OnUnlimitedTick(long seconds)
	{
		if (_unlimitedTipTime != null)
		{
			_unlimitedTipTime.text = TimeFormatter.FormatTimeLeft(seconds);
		}
	}

	private void OnUnlimitedStop()
	{
		UpdateUnlimitedTip();
	}

	private void OnDestroy()
	{
		_sweepAnimDone = true;
		if (_unlimitedTipTime != null)
		{
			var countdown = _unlimitedTipTime.GetComponent<CountdownHelper>();
			if (countdown != null)
			{
				countdown.OnTimerTick.RemoveListener(OnUnlimitedTick);
				countdown.OnTimerStoped.RemoveListener(OnUnlimitedStop);
			}
		}
		if (Managers.Heart != null)
		{
			Managers.Heart.OnInfinityHeartChanged.RemoveListener(OnHeartHudChanged);
			Managers.Heart.OnHeartChanged.RemoveListener(OnHeartHudChanged);
		}
		transform.DOKill();
	}

	private void OnEnable()
	{
		if (_started) InitAsync().Forget();
	}

	private void OnDisable()
	{
		var clickCts = _clickCts;
		_clickCts = null;
		clickCts?.Cancel();
		_sweepAnimDone = true;
		var cts = _sweepCts;
		_sweepCts = null;
		cts?.Cancel();
	}
}
