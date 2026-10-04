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
    [SerializeField] private Transform _completedTickNode, _progressBarText, _lockTextNode, _slidingBlock, _specialIcon, _lockIcon;
    [SerializeField] private SpecialStreakMultiplierSlider _multiplierSlider;
    [SerializeField] private TMP_Text _sliderValueText, _multiplierLabelText, _timerText;
    [SerializeField] private Snui.Timer _timer;
    [SerializeField] private GameObject _collectItemPrefab;
    [SerializeField] private Sprite _lockProgressBg, _lockIconSprite;
    [SerializeField] private ParticleSystem _boneSpark, _multiplierLabelSpark, _rewardCellSpark;
    [SerializeField] private AudioClip _progressExpAudioClip, _progressMaxAudioClip, _collectAudioClip, _multiplierSliderWhooshAudioClip, _labelTextChangeAudioClip;
    private HomeController _home;
    private SpecialStreakManager _manager;
    private SpecialStreakDialog _dialog;
    private SpecialStreakCollectItem _collectItem;
    private Finger _finger;
    private CancellationTokenSource _animation;
    private readonly List<Tween> _tweens = new List<Tween>();
    private readonly List<Material> _materials = new List<Material>();
    public bool IsAnimating { get; private set; }
    public bool HasDialogOpen => _dialog != null;
    public bool HasAuthoredLayout => _button != null && _progressBar != null;
    private void Awake()
    {
        SpecialStreakPresentation.Prepare(transform, _materials);
        if (_button != null) _button.onClick.AddListener(OnClick);
    }
    public void Bind(HomeController home)
    {
        _home = home;
        if (_manager != null) _manager.Changed -= RefreshProgress;
        _manager = SpecialStreakManager.Ensure(); _manager.Changed += RefreshProgress;
        RefreshProgress(); PlayAnims().Forget();
    }
    public void RefreshProgress()
    {
        if (IsAnimating) return;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        bool locked = manager.IsLocked, completed = manager.IsCompleted();
        SpecialStreakPresentation.SetActive(_lockTextNode, locked);
        SpecialStreakPresentation.SetActive(_lockIcon, locked);
        SpecialStreakPresentation.SetActive(_completedTickNode, completed);
        SpecialStreakPresentation.SetActive(_progressBarText, !locked && !completed);
        SpecialStreakPresentation.SetActive(_timer, false);
        if (_timerText != null) _timerText.text = locked ? "UNLOCKS AT LEVEL 21" : completed ? "COMPLETED" : "OFFLINE — NO TIME LIMIT";
        if (_button != null) _button.interactable = !locked && !IsAnimating;
        var stage = manager.GetCurrRewardInfo();
        _progressBar?.SetPercent(manager.CurrCount, stage?.Goal ?? 0);
        if (_rewardCell != null) { _rewardCell.gameObject.SetActive(stage != null); if (stage != null) _rewardCell.Init(stage.Items()[0], true, false).Forget(); }
        _multiplierSlider?.Init(manager.MultiplierIndex);
        SpecialStreakPresentation.SetActive(_multiplierSlider, false); // Native Start hides the expanded multiplier track.
        if (_multiplierLabelText != null) _multiplierLabelText.text = "x" + manager.GetCurrentMultiplier();
    }
    public bool HasNonSkippableAnimation() => HasDialogOpen;
    public void OnSkipAnimEvent() => _collectItem?.SpeedUpAnims();
    public void CommitProgressBeforeEnterGame() { _manager?.SettlePending(); CancelAnimation(); }
    public async UniTask PlayAnims()
    {
        if (IsAnimating || !isActiveAndEnabled) return;
        var manager = _manager ?? SpecialStreakManager.Ensure();
        int count = manager.TempCount, multiplier = manager.MultiplierIndex;
        int firstStage = manager.ClaimedIndex, originalCount = manager.CurrCount;
        if (!manager.ShouldSettleCollectionResult()) { CheckTutorial(); return; }
        IsAnimating = true;
        if (_button != null) _button.interactable = false;
        if (!manager.SettlePending()) { IsAnimating = false; if (_timerText != null) _timerText.text = manager.LastError; return; }
        _animation = new CancellationTokenSource(); var token = _animation.Token;
        try
        {
            if (count > 0 && _collectItemPrefab != null)
            {
                var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
                if (canvas != null)
                {
                    _collectItem = Instantiate(_collectItemPrefab, canvas.transform, false).GetComponent<SpecialStreakCollectItem>();
                    if (_collectItem != null)
                    {
                        await _collectItem.PlayShowAnim(count, multiplier);
                        token.ThrowIfCancellationRequested();
                        await _collectItem.PlayFlyToTargetAnim(_specialIcon != null ? _specialIcon : transform);
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            _boneSpark?.Play(); _multiplierLabelSpark?.Play();
            SpecialStreakPresentation.Sound(_collectAudioClip, gameObject);
            for (int index = firstStage; index <= manager.ClaimedIndex; index++)
            {
                var stage = manager.GetRewardInfo(index);
                if (stage == null) break;
                bool claimed = index < manager.ClaimedIndex;
                if (_rewardCell != null) await _rewardCell.Init(stage.Items()[0], true, false);
                token.ThrowIfCancellationRequested();
                _progressBar?.SetPercent(index == firstStage ? originalCount : 0, stage.Goal);
                // Reward-claim and remaining-progress state machines both use 0.4 seconds.
                var tween = _progressBar?.IncreaseTo(claimed ? stage.Goal : manager.CurrCount, .4f);
                if (tween != null) { tween.SetEase(claimed ? Ease.Linear : Ease.OutQuad); _tweens.Add(tween); }
                SpecialStreakPresentation.Sound(_progressExpAudioClip, gameObject);
                await UniTask.Delay(TimeSpan.FromSeconds(.4f), ignoreTimeScale: true, cancellationToken: token);
                if (claimed) { _rewardCellSpark?.Play(); SpecialStreakPresentation.Sound(_progressMaxAudioClip, gameObject); }
            }
            SpecialStreakPresentation.Sound(_multiplierSliderWhooshAudioClip, gameObject);
            SpecialStreakPresentation.SetActive(_multiplierSlider, true);
            _multiplierSlider?.Init(manager.MultiplierIndex);
            SpecialStreakPresentation.Sound(_labelTextChangeAudioClip, gameObject);
            await UniTask.Delay(TimeSpan.FromSeconds(.4f), ignoreTimeScale: true, cancellationToken: token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_collectItem != null) Destroy(_collectItem.gameObject); _collectItem = null;
            IsAnimating = false; _animation?.Dispose(); _animation = null;
            if (this != null && isActiveAndEnabled) { RefreshProgress(); CheckTutorial(); }
        }
    }
    private void CheckTutorial()
    {
        if (_manager == null || !_manager.ShouldShowTutorial() || _finger != null || !isActiveAndEnabled) return;
        if (DataPrefabLibrary.Current?.FingerPrefab == null) return;
        _finger = Finger.Show(transform).GetAwaiter().GetResult();
        SpecialStreakPresentation.Prepare(_finger.transform, _materials);
        _finger.ShowFinger(new Vector2(100, -30));
    }
    public void OnClick()
    {
        if (!isActiveAndEnabled || IsAnimating || HasDialogOpen || _manager == null || _manager.IsLocked || (_home != null && !_home.CanOpenSpecialStreak)) return;
        var parent = GetComponentInParent<Canvas>()?.rootCanvas.transform;
        _dialog = SpecialStreakDialog.Show(parent).GetAwaiter().GetResult();
        if (_dialog == null) return;
        if (_finger != null) Destroy(_finger.gameObject); _finger = null;
        _manager.SetTutorialShown(); _dialog.Closed += OnDialogClosed;
    }
    private void OnDialogClosed() { if (_dialog != null) _dialog.Closed -= OnDialogClosed; _dialog = null; RefreshProgress(); }
    private void CancelAnimation()
    {
        _animation?.Cancel();
        foreach (var tween in _tweens) if (tween.IsActive()) tween.Kill(); _tweens.Clear();
        if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject);
        if (_collectItem != null) { Destroy(_collectItem.gameObject); _collectItem = null; }
    }
    private void OnDisable()
    {
        if (_manager != null) _manager.Changed -= RefreshProgress;
        CancelAnimation();
        foreach (var particle in new[] { _boneSpark, _multiplierLabelSpark, _rewardCellSpark })
            if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_finger != null) Destroy(_finger.gameObject); _finger = null;
        if (_dialog != null) { _dialog.Closed -= OnDialogClosed; _dialog.Close(); _dialog = null; }
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
        OnDisable(); if (_manager != null) _manager.Changed -= RefreshProgress;
        if (_button != null) _button.onClick.RemoveListener(OnClick);
        SpecialStreakPresentation.Cleanup(_materials);
    }
}
