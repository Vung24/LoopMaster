using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using TMPro;
using UnityEngine.UI;

public class SpecialStreakProgress : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private ProgressBar _progressBar;
    [SerializeField] private RewardCell _rewardCell;
    [SerializeField] private Transform _completedTickNode;
    [SerializeField] private Transform _progressBarText;
    [SerializeField] private Transform _lockTextNode;
    [SerializeField] private SpecialStreakMultiplierSlider _multiplierSlider;
    [SerializeField] private Transform _slidingBlock;
    [SerializeField] private TMP_Text _sliderValueText;
    [SerializeField] private TMP_Text _multiplierLabelText;
    [SerializeField] private Snui.Timer _timer;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private Transform _specialIcon;
    [SerializeField] private GameObject _collectItemPrefab;
    [SerializeField] private Sprite _lockProgressBg;
    [SerializeField] private Transform _lockIcon;
    [SerializeField] private ParticleSystem _boneSpark;
    [SerializeField] private ParticleSystem _multiplierLabelSpark;
    [SerializeField] private ParticleSystem _rewardCellSpark;
    [SerializeField] private Sprite _lockIconSprite;
    [SerializeField] private AudioClip _progressExpAudioClip;
    [SerializeField] private AudioClip _progressMaxAudioClip;
    [SerializeField] private AudioClip _collectAudioClip;
    [SerializeField] private AudioClip _multiplierSliderWhooshAudioClip;
    [SerializeField] private AudioClip _labelTextChangeAudioClip;

    private const float SkipAnimSpeed = 3f;
    private SpecialStreakCollectItem _collectItem;
    private Vector3 _rewardCellScale = Vector3.one;
    private float _multiplierSliderShownX;
    private Image _tutorialMask;
    private Finger _finger;
    private Transform _originParent;
    private int _pendingCurrCount;
    private int _pendingClaimedIndex;
    private int _pendingMultiplierIndex;
    private int _pendingTempCount;
    private readonly AnimSpeedController _animSpeedController = new AnimSpeedController();
    private bool _canSpeedUpAddCollectionAnim;
    private bool _isLeavingHome;
    private static bool _winCollectionPlaying;

    private HomeController _home;
    private SpecialStreakManager _manager;
    private SpecialStreakDialog _dialog;
    private bool _dialogLoading;
    private int _dialogVersion;
    private readonly List<Tween> _tweens = new List<Tween>();
    private readonly List<Material> _materials = new List<Material>();

    public bool IsAnimating { get; private set; }
    public bool HasDialogOpen => _dialogLoading || _dialog != null;
    public bool HasAuthoredLayout => _button != null && _progressBar != null;

    private void Awake()
    {
        SpecialStreakPresentation.Prepare(transform, _materials);
        ResolveHierarchyReferences();
        if (_multiplierSlider != null)
            _multiplierSliderShownX = _multiplierSlider.transform.localPosition.x;
    }

    private void Start()
    {
        if (_rewardCell != null)
        {
            _rewardCellScale = _rewardCell.transform.localScale;
        }

        if (_button != null)
        {
            _button.interactable = false;
            _button.onClick.AddListener(OnClick);
        }

        if (!IsAnimating) SpecialStreakPresentation.SetActive(_multiplierSlider, false);
        RefreshProgress();
    }

    private void ResolveHierarchyReferences()
    {
        if (_button == null)
            _button = HierarchyReferenceCache.ChildComponent<Button>(transform, "Button") ?? GetComponent<Button>();
        if (_progressBar == null)
            _progressBar = HierarchyReferenceCache.DescendantComponent<ProgressBar>(transform, "ProgressBar");
        if (_rewardCell == null)
            _rewardCell = HierarchyReferenceCache.DescendantComponent<RewardCell>(transform, "RewardCell");
        if (_completedTickNode == null)
            _completedTickNode = HierarchyReferenceCache.Descendant(transform, "CompletedTickNode") ??
                                 HierarchyReferenceCache.Descendant(transform, "Tick");
        if (_progressBarText == null)
            _progressBarText = HierarchyReferenceCache.Descendant(transform, "ProgressBarText");
        if (_lockTextNode == null)
            _lockTextNode = HierarchyReferenceCache.Descendant(transform, "LockTextNode") ??
                            HierarchyReferenceCache.Descendant(transform, "LockText");
        if (_multiplierSlider == null)
            _multiplierSlider = HierarchyReferenceCache.DescendantComponent<SpecialStreakMultiplierSlider>(transform, "MultiplierSlider");
        if (_slidingBlock == null)
            _slidingBlock = HierarchyReferenceCache.Descendant(transform, "SlidingBlock");
        if (_sliderValueText == null)
            _sliderValueText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "SliderValueText");
        if (_multiplierLabelText == null)
            _multiplierLabelText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "MultiplierLabelText") ??
                                   HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "MultiplierText");
        if (_timer == null)
            _timer = HierarchyReferenceCache.DescendantComponent<Snui.Timer>(transform, "Timer");
        if (_timerText == null)
            _timerText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "TimerText");
        if (_specialIcon == null)
            _specialIcon = HierarchyReferenceCache.Descendant(transform, "SpecialIcon") ??
                           HierarchyReferenceCache.Descendant(transform, "Icon");
        if (_lockIcon == null)
            _lockIcon = HierarchyReferenceCache.Descendant(transform, "LockIcon");

        if (_boneSpark == null)
            _boneSpark = HierarchyReferenceCache.DescendantComponent<ParticleSystem>(transform, "BoneSpark");
        if (_multiplierLabelSpark == null)
            _multiplierLabelSpark = HierarchyReferenceCache.DescendantComponent<ParticleSystem>(transform, "MultiplierLabelSpark");
        if (_rewardCellSpark == null)
            _rewardCellSpark = HierarchyReferenceCache.DescendantComponent<ParticleSystem>(transform, "RewardCellSpark");
        if (_collectItemPrefab == null && PrefabLibrary.Current?.SpecialStreakCollectItemPrefab != null)
            _collectItemPrefab = PrefabLibrary.Current.SpecialStreakCollectItemPrefab;
    }

    public void Bind(HomeController home, bool playAnimations = true)
    {
        _home = home;
        if (_manager != null) _manager.Changed -= RefreshProgress;
        _manager = SpecialStreakManager.Ensure();
        _manager.Changed += RefreshProgress;
        RefreshProgress();
        if (playAnimations) PlayAnims().Forget();
    }

    public void RefreshProgress()
    {
        if (IsAnimating) return;
        var manager = _manager ?? SpecialStreakManager.Ensure();

        if (manager.IsLocked)
        {
            SetLockState();
            return;
        }

        SpecialStreakPresentation.SetActive(_lockTextNode, false);
        SpecialStreakPresentation.SetActive(_lockIcon, false);

        if (manager.IsCompleted())
        {
            SetCompletedState();
            return;
        }

        SpecialStreakPresentation.SetActive(_completedTickNode, false);
        SpecialStreakPresentation.SetActive(_progressBarText, true);
        SpecialStreakPresentation.SetActive(_timer, false);
        if (_timerText != null) _timerText.text = "OFFLINE — NO TIME LIMIT";

        if (_multiplierLabelText != null)
        {
            _multiplierLabelText.text = "x" + manager.GetCurrentMultiplier();
            if (_multiplierLabelText.transform.parent != null)
            {
                _multiplierLabelText.transform.parent.gameObject.SetActive(true);
            }
        }

        _multiplierSlider?.Init(manager.MultiplierIndex);
        SpecialStreakPresentation.SetActive(_multiplierSlider, false);

        var stage = manager.GetCurrRewardInfo();
        _progressBar?.SetPercent(manager.CurrCount, stage?.Goal ?? 0);

        if (_rewardCell != null)
        {
            _rewardCell.gameObject.SetActive(stage != null);
            if (stage != null && stage.Items().Count > 0)
            {
                _rewardCell.Init(stage.Items()[0], true, true).Forget();
            }
        }

        if (_button != null)
        {
            _button.interactable = !manager.IsLocked && !IsAnimating;
        }
    }

    private void SetLockState()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();

        if (_specialIcon != null)
        {
            var img = _specialIcon.GetComponent<Image>();
            if (img != null && _lockIconSprite != null) img.sprite = _lockIconSprite;
            _specialIcon.localPosition = new Vector3(_specialIcon.localPosition.x, 4f, _specialIcon.localPosition.z);
        }

        SpecialStreakPresentation.SetActive(_timer, false);
        if (_multiplierLabelText != null && _multiplierLabelText.transform.parent != null)
        {
            _multiplierLabelText.transform.parent.gameObject.SetActive(false);
        }

        _progressBar?.SetPercent(0, 1);
        SpecialStreakPresentation.SetActive(_progressBarText, false);
        if (_progressBar != null && _lockProgressBg != null)
        {
            var img = _progressBar.GetComponent<Image>();
            if (img != null) img.sprite = _lockProgressBg;
        }

        SpecialStreakPresentation.SetActive(_lockTextNode, true);
        SpecialStreakPresentation.SetActive(_lockIcon, true);
        SpecialStreakPresentation.SetActive(_completedTickNode, false);
        if (_rewardCell != null) _rewardCell.gameObject.SetActive(false);

        if (_lockTextNode != null)
        {
            var txt = _lockTextNode.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = "Unlock at LV " + manager.GetUnlockLevel();
        }

        if (_timerText != null)
        {
            _timerText.text = "UNLOCKS AT LEVEL " + manager.GetUnlockLevel();
        }

        if (_button != null) _button.interactable = false;
    }

    private void SetCompletedState()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        int lastGoal = manager.GetLastGoal();
        _progressBar?.SetPercent(lastGoal, lastGoal);
        SpecialStreakPresentation.SetActive(_completedTickNode, true);
        SpecialStreakPresentation.SetActive(_progressBarText, false);
        if (_rewardCell != null) _rewardCell.gameObject.SetActive(false);
        SpecialStreakPresentation.SetActive(_lockTextNode, false);
        SpecialStreakPresentation.SetActive(_lockIcon, false);
        if (_timerText != null) _timerText.text = "COMPLETED";
        if (_button != null) _button.interactable = true;
    }

    private bool ShouldStopAnimations()
    {
        return _isLeavingHome || this == null || !gameObject.activeInHierarchy;
    }

    public bool HasNonSkippableAnimation()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (HasDialogOpen || manager.ShouldShowTutorial() || manager.ShouldPopDialog())
        {
            return true;
        }

        var curr = manager.GetCurrRewardInfo();
        if (curr == null) return false;

        int mult = manager.GetMultiplier(manager.MultiplierIndex);
        return curr.Goal <= manager.CurrCount + mult * manager.TempCount;
    }

    public void OnSkipAnimEvent()
    {
        if (_canSpeedUpAddCollectionAnim)
        {
            _animSpeedController.SpeedUp(SkipAnimSpeed);
            _collectItem?.SpeedUpAnims();
        }
    }

    public void CommitProgressBeforeEnterGame()
    {
        _isLeavingHome = true;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (manager.TempCount < 1) return;
        if (HasNonSkippableAnimation()) return;

        manager.SetNextMultiplierIndex();
        CancelAnimation();
    }

    public async UniTask PlayAnims(bool showTutorial = true)
    {
        if (IsAnimating || !isActiveAndEnabled) return;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (manager.IsLocked) return;

        if (manager.ShouldSettleCollectionResult())
        {
            IsAnimating = true;
            if (_button != null) _button.interactable = false;

            _pendingTempCount = manager.TempCount;
            _pendingClaimedIndex = manager.ClaimedIndex;
            _pendingMultiplierIndex = manager.MultiplierIndex;
            _pendingCurrCount = manager.CurrCount;

            manager.SettlePending();

            await PlayAddCollectionAnim();
            if (ShouldStopAnimations()) { IsAnimating = false; return; }

            if (!await PlayRewardClaimAnim())
            {
                IsAnimating = false;
                RefreshProgress();
                return;
            }
            if (ShouldStopAnimations()) { IsAnimating = false; return; }

            bool isIncreaseMultiplier = PreCalculateIncreaseMultiplier();
            manager.CommitCollectionResult(_pendingCurrCount, _pendingClaimedIndex, _pendingMultiplierIndex);

            await PlayOtherProgressAnim();
            if (ShouldStopAnimations()) { IsAnimating = false; return; }

            await PlayIncreaseMultiplierAnim(isIncreaseMultiplier);
            if (ShouldStopAnimations()) { IsAnimating = false; return; }
        }

        if (showTutorial)
        {
            await CheckTutorial();
            if (ShouldStopAnimations()) { IsAnimating = false; return; }
            await CheckAutoPopDialog();
        }
        if (!ShouldStopAnimations() && _button != null)
        {
            _button.interactable = !manager.IsLocked;
        }

        IsAnimating = false;
        RefreshProgress();
    }

    private async UniTask CheckTutorial()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (!manager.ShouldShowTutorial()) return;

        await ShowFingerTutorial();
        await UniTask.WaitUntil(() => _tutorialMask == null, cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    private async UniTask ShowFingerTutorial()
    {
        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        _tutorialMask = UiUtils.CreateMask(canvas);
        if (_tutorialMask == null) return;

        _originParent = transform.parent;
        transform.SetParent(_tutorialMask.transform);

        await _tutorialMask.DOFade(0.99f, 0.2f).SetLink(gameObject).AsyncWaitForCompletion();
        await UniTask.WaitForSeconds(0.3f, cancellationToken: this.GetCancellationTokenOnDestroy());

        _finger = await Finger.Show(transform);
        if (_finger != null)
        {
            SpecialStreakPresentation.Prepare(_finger.transform, _materials);
            _finger.ShowFinger(Vector2.zero);
        }

        if (_button != null) _button.interactable = true;
    }

    private async UniTask PlayAddCollectionAnim()
    {
        if (_pendingTempCount < 1) return;
        _canSpeedUpAddCollectionAnim = true;
        _animSpeedController.Reset();

        var manager = _manager ?? SpecialStreakManager.Ensure();
        int multiplier = manager.GetMultiplier(_pendingMultiplierIndex);
        _pendingCurrCount += multiplier * _pendingTempCount;

        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        var mask = UiUtils.CreateMask(canvas);
        var oldParent = transform.parent;

        if (mask != null)
        {
            transform.SetParent(mask.transform);
            if (_collectItemPrefab != null)
            {
                var go = Instantiate(_collectItemPrefab, mask.transform, false);
                _collectItem = go.GetComponent<SpecialStreakCollectItem>();
            }
            var fadeTween = mask.DOFade(0.98f, 0.2f).SetLink(gameObject);
            _animSpeedController.Track(fadeTween);
        }

        if (_collectItem != null)
        {
            await _collectItem.PlayShowAnim(_pendingTempCount, _pendingMultiplierIndex);
        }

        if (mask != null)
        {
            var fadeOutTween = mask.DOFade(0f, 0.2f).SetLink(gameObject);
            _animSpeedController.Track(fadeOutTween);
        }

        if (_collectItem != null)
        {
            var target = _specialIcon != null ? _specialIcon : transform;
            await _collectItem.PlayFlyToTargetAnim(target);
        }

        if (!ShouldStopAnimations())
        {
            _boneSpark?.Play();
            transform.SetParent(oldParent);
            if (mask != null) Destroy(mask.gameObject);
            if (_collectItem != null)
            {
                Destroy(_collectItem.gameObject);
                _collectItem = null;
            }
            SpecialStreakPresentation.Sound(_collectAudioClip, gameObject);
            _canSpeedUpAddCollectionAnim = false;
        }
    }

    private async UniTask<bool> PlayRewardClaimAnim()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        var stage = manager.GetRewardInfo(_pendingClaimedIndex);

        if (stage != null && stage.Goal <= _pendingCurrCount)
        {
            bool isFirst = true;
            do
            {
                if (_pendingCurrCount < stage.Goal)
                {
                    if (stage != null)
                    {
                        _progressBar?.SetPercent(0, stage.Goal);
                        if (_rewardCell != null && stage.Items().Count > 0)
                        {
                            _rewardCell.Init(stage.Items()[0], true, true).Forget();
                        }
                        PlayRewardCellScaleShow().Forget();
                    }
                    break;
                }

                if (!isFirst)
                {
                    if (_rewardCell != null && stage.Items().Count > 0)
                    {
                        _rewardCell.Init(stage.Items()[0], true, true).Forget();
                    }
                    _progressBar?.SetPercent(0, stage.Goal);
                    PlayRewardCellScaleShow().Forget();
                }
                else
                {
                    isFirst = false;
                }

                SpecialStreakPresentation.Sound(_progressExpAudioClip, gameObject);

                if (_progressBar != null && _progressBar.CurrentValue < stage.Goal)
                {
                    var tween = _progressBar.IncreaseTo(stage.Goal, 0.4f);
                    if (tween != null)
                    {
                        tween.SetEase(Ease.Linear).SetLink(gameObject);
                        _tweens.Add(tween);
                        await tween.AsyncWaitForCompletion();
                    }
                    if (ShouldStopAnimations()) break;
                }

                SpecialStreakPresentation.Sound(_progressMaxAudioClip, gameObject);
                if (!PlayStageRewardsClaimAnim(_pendingClaimedIndex, stage.Items())) return false;

                _pendingClaimedIndex++;
                _pendingCurrCount -= stage.Goal;
                stage = manager.GetRewardInfo(_pendingClaimedIndex);
            } while (stage != null);
        }
        return !ShouldStopAnimations();
    }

    private bool PlayStageRewardsClaimAnim(int stageIndex, List<RewardItem> items)
    {
        if (_rewardCell != null && _rewardCell.RewardItem != null)
        {
            var type = _rewardCell.RewardItem.Type;
            if (type == RewardType.Coin)
            {
                Snui_CoinHud.TrySetAutoUpdate(false);
            }
            else if (type == RewardType.Heart || type == RewardType.InfinityHeart)
            {
                Snui_HeartHud.TrySetAutoUpdate(false);
            }
        }

        if (items != null)
        {
            var inventory = InventoryManager.Ensure();
            if (!inventory.TryClaimRewards(SpecialStreakManager.ClaimId(stageIndex), items, out _))
            {
                Debug.LogWarning("SpecialStreak reward could not be saved; collection remains pending.");
                return false;
            }
        }
        _rewardCellSpark?.Play();
        return true;
    }

    public static async UniTask<bool> ShowWinCollection(Transform parent)
    {
        if (_winCollectionPlaying) return false;
        var manager = SpecialStreakManager.Ensure();
        if (!manager.ShouldSettleCollectionResult()) return true;
        var prefab = PrefabLibrary.Current?.SpecialStreakProgressPrefab;
        if (parent == null || prefab == null) return false;
        var instance = Instantiate(prefab, parent, false);
        RewardDialog.PrepareOverlay(instance, 1340);
        var progress = instance.GetComponent<SpecialStreakProgress>();
        if (progress == null) { Destroy(instance); return false; }
        _winCollectionPlaying = true;
        RewardDialog dialog = null;
        try
        {
            int claimedBefore = manager.ClaimedIndex;
            progress.Bind(null, false);
            await progress.PlayAnims(false);
            if (progress == null || manager.ShouldSettleCollectionResult()) return false;
            if (manager.ClaimedIndex > claimedBefore)
            {
                var rewards = new List<RewardItem>();
                for (int stage = claimedBefore; stage < manager.ClaimedIndex; stage++)
                    rewards.AddRange(manager.GetRewardInfo(stage).Items());
                dialog = await RewardDialog.ShowSpecialStreakRewards(rewards, parent);
                if (dialog != null)
                    await UniTask.WaitUntil(() => dialog == null || !dialog.isActiveAndEnabled,
                        cancellationToken: instance.GetCancellationTokenOnDestroy());
            }
            return true;
        }
        finally
        {
            _winCollectionPlaying = false;
            if (dialog != null) Destroy(dialog.gameObject);
            if (instance != null) Destroy(instance);
        }
    }

    private async UniTask PlayRewardCellScaleShow()
    {
        if (_rewardCell == null) return;
        _rewardCell.transform.localScale = Vector3.zero;
        var seq = DOTween.Sequence();
        seq.Append(_rewardCell.transform.DOScale(_rewardCellScale * 1.2f, 0.1f).SetEase(Ease.OutSine));
        seq.Append(_rewardCell.transform.DOScale(_rewardCellScale, 0.07f).SetEase(Ease.InSine));
        seq.SetLink(gameObject);
        _tweens.Add(seq);
        await seq.AsyncWaitForCompletion();
    }

    private bool PreCalculateIncreaseMultiplier()
    {
        int original = _pendingMultiplierIndex;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (!manager.IsExpired() && !manager.IsCompleted(_pendingClaimedIndex))
        {
            _pendingMultiplierIndex = manager.GetNextMultiplierIndex(_pendingMultiplierIndex);
        }
        return original < _pendingMultiplierIndex;
    }

    private async UniTask PlayOtherProgressAnim()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (manager.IsCompleted(_pendingClaimedIndex))
        {
            SetCompletedState();
            return;
        }

        if (_pendingCurrCount >= 1 && _progressBar != null)
        {
            var tween = _progressBar.IncreaseTo(_pendingCurrCount, 0.4f);
            if (tween != null)
            {
                tween.SetEase(Ease.OutSine).SetLink(gameObject);
                _tweens.Add(tween);
                await tween.AsyncWaitForCompletion();
            }
        }
    }

    private async UniTask PlayIncreaseMultiplierAnim(bool isIncreaseMultiplier)
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (!isIncreaseMultiplier)
        {
            if (manager.IsMaxMultiplierIndex(_pendingMultiplierIndex))
            {
                _multiplierLabelSpark?.Play();
                PlayLabelTextScale();
            }
            return;
        }

        int multiplier = manager.GetMultiplier(_pendingMultiplierIndex);
        if (_multiplierSlider != null)
        {
            _multiplierSlider.gameObject.SetActive(true);
            _multiplierSlider.transform.localPosition = new Vector3(_multiplierSliderShownX - 480f, _multiplierSlider.transform.localPosition.y, _multiplierSlider.transform.localPosition.z);
        }
        SpecialStreakPresentation.Sound(_multiplierSliderWhooshAudioClip, gameObject);

        if (_multiplierSlider != null)
        {
            var seq = DOTween.Sequence();
            seq.Append(_multiplierSlider.transform.DOLocalMoveX(_multiplierSliderShownX + 40f, 0.23f).SetEase(Ease.OutSine));
            seq.Append(_multiplierSlider.transform.DOLocalMoveX(_multiplierSliderShownX, 0.17f).SetEase(Ease.OutQuad));
            seq.SetLink(gameObject);
            _tweens.Add(seq);
            await seq.AsyncWaitForCompletion();
        }

        if (ShouldStopAnimations()) return;
        await UniTask.WaitForSeconds(0.18f, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (ShouldStopAnimations()) return;

        _multiplierSlider?.Init(_pendingMultiplierIndex);
        if (_sliderValueText != null) _sliderValueText.text = "x" + multiplier;
        SpecialStreakPresentation.Sound(_labelTextChangeAudioClip, gameObject);
        if (_multiplierLabelText != null) _multiplierLabelText.text = "x" + multiplier;
        _multiplierLabelSpark?.Play();
        PlayLabelTextScale();

        await HideMultiplierSliderAfterDelay();
    }

    private async UniTask HideMultiplierSliderAfterDelay()
    {
        await UniTask.WaitForSeconds(1.12f, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (ShouldStopAnimations() || _multiplierSlider == null) return;

        SpecialStreakPresentation.Sound(_multiplierSliderWhooshAudioClip, gameObject);
        var seq = DOTween.Sequence();
        seq.Append(_multiplierSlider.transform.DOLocalMoveX(_multiplierSliderShownX + 188f, 0.17f).SetEase(Ease.OutSine));
        seq.Append(_multiplierSlider.transform.DOLocalMoveX(_multiplierSliderShownX - 480f, 0.23f).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            if (_multiplierSlider != null) _multiplierSlider.gameObject.SetActive(false);
        });
        seq.SetLink(gameObject);
        _tweens.Add(seq);
        await seq.AsyncWaitForCompletion();
    }

    private void PlayLabelTextScale()
    {
        if (_multiplierLabelText == null) return;
        var seq = DOTween.Sequence();
        seq.Append(_multiplierLabelText.transform.DOScale(1.24f, 0.1f));
        seq.Append(_multiplierLabelText.transform.DOScale(1f, 0.1f));
        seq.SetLink(gameObject);
        _tweens.Add(seq);
    }

    private async UniTask CheckAutoPopDialog()
    {
        var manager = _manager ?? SpecialStreakManager.Ensure();
        if (manager.ShouldPopDialog())
        {
            await ShowDetailDialog();
        }
    }

    private async UniTask ShowDetailDialog()
    {
        if (HasDialogOpen) return;
        int version = _dialogVersion;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        var parent = GetComponentInParent<Canvas>()?.rootCanvas.transform;
        _dialogLoading = true;
        try
        {
            var dialog = await SpecialStreakDialog.Show(parent);
            if (this == null || version != _dialogVersion || !isActiveAndEnabled)
            { if (dialog != null) dialog.Close(); return; }
            _dialog = dialog;
        }
        finally { if (this != null && version == _dialogVersion) _dialogLoading = false; }
        if (_dialog == null) return;
        manager.SetDialogPopped();

        if (_tutorialMask != null)
        {
            _tutorialMask.color = new Color(_tutorialMask.color.r, _tutorialMask.color.g, _tutorialMask.color.b, 0f);
            if (_finger != null) _finger.gameObject.SetActive(false);
        }

        await UniTask.WaitUntil(() => _dialog == null, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (_tutorialMask != null)
        {
            transform.SetParent(_originParent);
            Destroy(_tutorialMask.gameObject);
            _tutorialMask = null;
            if (_finger != null)
            {
                Destroy(_finger.gameObject);
                _finger = null;
            }
        }

        RefreshProgress();
    }

    public void OnClick()
    {
        if (!isActiveAndEnabled || IsAnimating || HasDialogOpen || _manager == null || _manager.IsLocked || (_home != null && !_home.CanOpenSpecialStreak)) return;
        ShowDetailDialog().Forget();
    }

    private void CancelAnimation()
    {
        foreach (var tween in _tweens)
        {
            if (tween.IsActive()) tween.Kill();
        }
        _tweens.Clear();

        if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject);
        if (_collectItem != null)
        {
            Destroy(_collectItem.gameObject);
            _collectItem = null;
        }
    }

    private void OnDisable()
    {
        _dialogVersion++;
        _dialogLoading = false;
        if (_manager != null) _manager.Changed -= RefreshProgress;
        CancelAnimation();
        foreach (var particle in new[] { _boneSpark, _multiplierLabelSpark, _rewardCellSpark })
        {
            if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (_finger != null)
        {
            Destroy(_finger.gameObject);
            _finger = null;
        }

        if (_dialog != null)
        {
            _dialog.Close();
            _dialog = null;
        }
    }

    private void OnEnable()
    {
        if (_manager == null) return;
        _manager.Changed -= RefreshProgress;
        _manager.Changed += RefreshProgress;
        RefreshProgress();
    }

    private void OnDestroy()
    {
        OnDisable();
        if (_manager != null) _manager.Changed -= RefreshProgress;
        if (_button != null) _button.onClick.RemoveListener(OnClick);
        SpecialStreakPresentation.Cleanup(_materials);
    }
}
