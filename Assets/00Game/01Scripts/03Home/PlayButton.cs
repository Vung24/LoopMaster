using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
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
	private bool IsPlayLevelChangeAnim;
	private HomePage _homePage;

	private void Awake()
	{
		_homePage = HomeController.Current != null ? HomeController.Current.HomePage : GetComponentInParent<HomePage>(true);
	}

	public void Bind(HomePage home)
	{
		_homePage = home;
	}

	private void Start()
	{
		if (_btn != null)
		{
			_btn.onClick.AddListener(() => OnClick(false).Forget());
		}

		if (_btnText != null)
		{
			_btnText.text = GameProgressStore.NextPlayableLevel.ToString();
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

		if (!IsPlayLevelChangeAnim)
		{
			UpdateStyle(_hardLevel);
		}
	}

	public void PlayEnterAnim()
	{
		if (_animation != null)
		{
			_animation.Play();
		}
		else if (_canvasGroup != null)
		{
			_canvasGroup.alpha = 1f;
		}
	}

	public void PlayShakeAnim()
	{
		transform.DOKill();
		transform.DOShakeScale(0.3f, 0.1f, 10, 90f, true);
	}

	public async UniTaskVoid PlaySweepAnim()
	{
		_sweepAnimDone = false;
		string animName = _hardLevel == HardLevel.VeryHard ? "very_hard_light" : (_hardLevel == HardLevel.Hard ? "hard_light" : "normal_light");

		while (this != null && !_sweepAnimDone)
		{
			if (_levelLightSpine != null)
			{
				_levelLightSpine.gameObject.SetActive(true);
				if (_levelLightSpine.AnimationState != null)
				{
					_levelLightSpine.AnimationState.SetAnimation(0, animName, false);
				}
			}
			await UniTask.Delay(TimeSpan.FromSeconds(3f));
		}
	}

	private async UniTask PlayClickFeedback(float pressDuration = 0.08f)
	{
		transform.DOKill();
		await transform.DOScale(0.92f, pressDuration).SetEase(Ease.OutQuad).ToUniTask();
		await transform.DOScale(1f, pressDuration).SetEase(Ease.OutBack).ToUniTask();
	}

	public void TriggerSkipAnimationsClick()
	{
		PlayClickFeedback(0.08f).Forget();
		OnClick(true).Forget();
	}

	public async UniTask PlayChangeLevelAnim()
	{
		Sequence seq = DOTween.Sequence();
		seq.Append(transform.DOScale(0.94f, 0.1f).SetEase(Ease.OutQuad));
		seq.Append(transform.DOScale(1.2f, 0.23f).SetEase(Ease.OutBack));
		seq.AppendCallback(() =>
		{
			if (_levelChangeAudioClip != null)
			{
				AudioSource.PlayClipAtPoint(_levelChangeAudioClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
			}
		});
		seq.Append(transform.DOScale(0.94f, 0.17f).SetEase(Ease.OutQuad));
		seq.AppendCallback(() =>
		{
			if (_btnText != null)
			{
				_btnText.text = GameProgressStore.NextPlayableLevel.ToString();
			}
			if (_levelChangeSpine != null)
			{
				_levelChangeSpine.gameObject.SetActive(true);
				_levelChangeSpine.AnimationState?.SetAnimation(0, "level_change", false);
			}
			UpdateStyle(_hardLevel);
		});
		seq.Append(transform.DOScale(1.0f, 0.17f).SetEase(Ease.OutBack));
		await seq.ToUniTask();
	}

	private void OnHeartHudChanged()
	{
		UpdateUnlimitedTip();
	}

	private async UniTaskVoid OnClick(bool skipAnimations = false)
	{
		if (skipAnimations && _homePage != null && _homePage.IsSkipAnimationsBlocked())
		{
			return;
		}

		if (Managers.Heart != null && !Managers.Heart.IsHeartEnough())
		{
			var dialog = await MoreHeartDialogContent.ShowInDialog(JumpHeartDialogSourceScene.HomeBoosterIn);
			if (dialog != null)
			{
				return;
			}
		}

		EnterGame(skipAnimations).Forget();
	}

	private async UniTaskVoid EnterGame(bool skipAnimations)
	{
		if (skipAnimations && _homePage != null)
		{
			if (!_homePage.CommitProgressBeforeEnterGame())
			{
				return;
			}
			await UniTask.Delay(TimeSpan.FromSeconds(0.2f));
		}

		_sweepAnimDone = true;
		if (_homePage != null)
		{
			_homePage.OnClickPlay();
		}
		else if (HomeController.Current != null)
		{
			HomeController.Current.TryEnterGame();
		}
	}

	private void UpdateStyle(HardLevel hardLevel)
	{
		_hardLevel = hardLevel;
		if (_btn != null)
		{
			switch (hardLevel)
			{
				case HardLevel.VeryHard:
					_btn.spriteState = PlayBtnVeryHardSpriteState;
					break;
				case HardLevel.Hard:
					_btn.spriteState = PlayBtnHardSpriteState;
					break;
				default:
					_btn.spriteState = PlayBtnNormalSpriteState;
					break;
			}
		}

		bool isHardOrVeryHard = hardLevel != HardLevel.Normal;
		if (_hardTagNode != null)
		{
			_hardTagNode.gameObject.SetActive(isHardOrVeryHard);
			var tagImage = _hardTagNode.GetComponent<Image>();
			if (tagImage != null)
			{
				tagImage.sprite = hardLevel == HardLevel.VeryHard ? _veryHardCoinTagSp : _hardCoinTagSp;
			}
		}

		if (_hardText != null)
		{
			_hardText.gameObject.SetActive(isHardOrVeryHard);
			if (isHardOrVeryHard)
			{
				_hardText.text = hardLevel == HardLevel.VeryHard ? "SUPER HARD" : "HARD";
			}
		}
	}

	private CountdownHelper _unlimitedCountdown;

	private void UpdateUnlimitedTip()
	{
		bool isInfinite = Managers.Heart != null && Managers.Heart.IsInfinite();
		if (_unlimitedTip != null)
		{
			_unlimitedTip.gameObject.SetActive(isInfinite);
		}
		if (isInfinite && _unlimitedTipTime != null)
		{
			if (_unlimitedCountdown == null)
				_unlimitedCountdown = _unlimitedTipTime.GetComponent<CountdownHelper>() ?? _unlimitedTipTime.gameObject.AddComponent<CountdownHelper>();
			_unlimitedCountdown.OnTimerTick.RemoveListener(OnUnlimitedTick);
			_unlimitedCountdown.OnTimerStoped.RemoveListener(OnUnlimitedStop);
			_unlimitedCountdown.OnTimerTick.AddListener(OnUnlimitedTick);
			_unlimitedCountdown.OnTimerStoped.AddListener(OnUnlimitedStop);
			_unlimitedCountdown.CountdownUntil(Managers.Heart.InfiniteHeartEndTime);
		}
		else if (_unlimitedCountdown != null)
		{
			_unlimitedCountdown.StopTimer();
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
		if (Managers.Heart != null)
		{
			Managers.Heart.OnInfinityHeartChanged.RemoveListener(OnHeartHudChanged);
			Managers.Heart.OnHeartChanged.RemoveListener(OnHeartHudChanged);
		}
		transform.DOKill();
	}
}
