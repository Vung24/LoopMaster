using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using Spine.Unity;

public class SpecialStreakCollectItem : MonoBehaviour
{
    [SerializeField] private Transform _backLight, _icon, _numText;
    [SerializeField] private ParticleSystem _numParticle, _boneParticle;
    [SerializeField] private SkeletonGraphic _multiplierLabel;
    [SerializeField] private TMP_Text _multiplierLabelText;
    [SerializeField] private AudioClip _showAudioClip, _flyAudioClip, _doubleAudioClip, _doubleTipAudioClip;
    private readonly List<Tween> _tweens = new List<Tween>();
    private readonly List<Material> _materials = new List<Material>();
    private CancellationTokenSource _animation;
    private float _speed = 1f;
    private bool _labelSubscribed;
    private void Awake() => SpecialStreakPresentation.Prepare(transform, _materials);
    public void SpeedUpAnims() { _speed = 3f; foreach (var tween in _tweens) if (tween.IsActive()) tween.timeScale = 3f; }
    private Sequence Pop(Transform node)
    {
        if (node == null) return null;
        node.gameObject.SetActive(true); node.localScale = Vector3.zero;
        // Native 0x1283688: 1.5/.9 in .13s, 1.02/1 in .07s.
        var sequence = DOTween.Sequence().Append(node.DOScale(1.5f, .13f).SetEase(Ease.OutQuad)).Append(node.DOScale(.9f, .13f).SetEase(Ease.InQuart))
            .Append(node.DOScale(1.02f, .07f).SetEase(Ease.OutQuad)).Append(node.DOScale(1f, .07f).SetEase(Ease.InOutSine)).SetUpdate(true).SetLink(gameObject);
        sequence.timeScale = _speed; _tweens.Add(sequence); return sequence;
    }
    public async UniTask PlayShowAnim(int count, int multiplierIndex)
    {
        Cancel(); _animation = new CancellationTokenSource(); var token = _animation.Token;
        int total = SpecialStreakManager.Ensure().GetMultipliedCount(count, multiplierIndex);
        var text = _numText == null ? null : _numText.GetComponent<TMP_Text>();
        if (text != null) text.text = count.ToString();
        SpecialStreakPresentation.SetActive(_multiplierLabel, false);
        Pop(_icon); Pop(_numText); SpecialStreakPresentation.Sound(_showAudioClip, gameObject);
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds((multiplierIndex > 0 ? .23f : .41f) / _speed), ignoreTimeScale: true, cancellationToken: token);
            if (multiplierIndex > 0)
            {
                if (_multiplierLabelText != null) _multiplierLabelText.text = "x" + SpecialStreakManager.Ensure().GetMultiplier(multiplierIndex);
                SpecialStreakPresentation.SetActive(_multiplierLabel, true);
                var labelAnimation = _multiplierLabel?.SkeletonData?.FindAnimation("animation");
                if (labelAnimation != null)
                {
                    _multiplierLabel.AnimationState.Event += OnLabelEvent; _labelSubscribed = true;
                    _multiplierLabel.AnimationState.SetAnimation(0, "animation", false);
                    SpecialStreakPresentation.Sound(_doubleTipAudioClip, gameObject);
                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0, labelAnimation.Duration - .1f) / _speed), ignoreTimeScale: true, cancellationToken: token);
                    RemoveLabelListener();
                }
                _numParticle?.Play(); _boneParticle?.Play(); SpecialStreakPresentation.Sound(_doubleAudioClip, gameObject);
                if (_numText != null)
                {
                    var change = DOTween.Sequence().Append(_numText.DOScale(.4f, .1f))
                        .AppendCallback(() => { if (text != null) text.text = total.ToString(); })
                        .Append(_numText.DOScale(2f, .17f).SetEase(Ease.InQuart))
                        .Append(_numText.DOScale(2.02f, .03f).SetEase(Ease.OutQuad))
                        .Append(_numText.DOScale(1f, .27f).SetEase(Ease.InOutSine)).SetUpdate(true).SetLink(gameObject);
                    change.timeScale = _speed; _tweens.Add(change);
                    await UniTask.WaitUntil(() => !change.IsActive() || change.IsComplete(), cancellationToken: token);
                }
                else if (text != null) text.text = total.ToString();
            }
            await UniTask.Delay(TimeSpan.FromSeconds(.2f / _speed), ignoreTimeScale: true, cancellationToken: token);
        }
        catch (OperationCanceledException) { }
    }
    public async UniTask PlayFlyToTargetAnim(Transform target)
    {
        if (target == null || _animation == null || _animation.IsCancellationRequested) return;
        var token = _animation.Token;
        SpecialStreakPresentation.Sound(_flyAudioClip, gameObject);
        Vector3 destination = transform.parent == null ? target.position : transform.parent.InverseTransformPoint(target.position);
        Vector3 delta = destination - transform.localPosition;
        // 0x12845FC; ELF literal RVA 0x1284CEC = 150.
        var sequence = DOTween.Sequence().Append(transform.DOBlendableLocalMoveBy(new Vector3(0, -150, 0), .17f).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(1.1f, .17f))
            .Append(transform.DOBlendableLocalMoveBy(new Vector3(0, delta.y + 150, 0), .27f).SetEase(Ease.InCubic))
            .Join(transform.DOScale(.4f, .27f))
            .Insert(0, transform.DOBlendableLocalMoveBy(new Vector3(delta.x, 0, delta.z), .43f).SetEase(Ease.InQuad)).SetUpdate(true).SetLink(gameObject);
        sequence.timeScale = _speed; _tweens.Add(sequence);
        try { await UniTask.WaitUntil(() => !sequence.IsActive() || sequence.IsComplete(), cancellationToken: token); }
        catch (OperationCanceledException) { }
    }
    private void OnLabelEvent(Spine.TrackEntry entry, Spine.Event spineEvent)
    { if (isActiveAndEnabled && spineEvent.Data.Name == "disappear") SpecialStreakPresentation.SetActive(_multiplierLabel, false); }
    private void RemoveLabelListener()
    { if (_labelSubscribed && _multiplierLabel != null && _multiplierLabel.AnimationState != null) _multiplierLabel.AnimationState.Event -= OnLabelEvent; _labelSubscribed = false; }
    private void Cancel() { RemoveLabelListener(); _animation?.Cancel(); _animation?.Dispose(); _animation = null; foreach (var tween in _tweens) tween.Kill(); _tweens.Clear(); }
    private void OnDisable() { Cancel(); if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject); }
    private void OnDestroy() { Cancel(); SpecialStreakPresentation.Cleanup(_materials); }
}
