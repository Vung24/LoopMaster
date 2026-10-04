using Spine.Unity;
using DG.Tweening;
using UnityEngine;

public class LoseContainer : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic _ipSpine;
    [SerializeField] private Transform _loseBg;
    [SerializeField] private string _entranceAnimation = "entrance";
    [SerializeField] private string _idleAnimation = "idle";
    private Tween _backgroundTween;
    private Tween _mascotFade;

    private void OnEnable() { Show(); }

    public void Show()
    {
        CancelTweens();
        if (_loseBg != null)
        {
            _loseBg.gameObject.SetActive(true);
            _loseBg.localScale = Vector3.zero;
            _backgroundTween = _loseBg.DOScale(1f, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        if (_ipSpine == null) _ipSpine = GetComponentInChildren<SkeletonGraphic>(true);
        if (_ipSpine == null || _ipSpine.skeletonDataAsset == null) return;
        _ipSpine.gameObject.SetActive(true);
        CanvasGroup group = _ipSpine.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            _mascotFade = group.DOFade(1f, 0.1f).SetUpdate(true);
        }
        _ipSpine.UnscaledTime = true;
        _ipSpine.Initialize(false);
        var data = _ipSpine.skeletonDataAsset.GetSkeletonData(false);
        if (data == null || _ipSpine.AnimationState == null) return;
        _ipSpine.Skeleton.SetToSetupPose();
        _ipSpine.AnimationState.ClearTracks();
        if (data.FindAnimation(_entranceAnimation) != null)
        {
            _ipSpine.AnimationState.SetAnimation(0, _entranceAnimation, false);
            if (data.FindAnimation(_idleAnimation) != null)
                _ipSpine.AnimationState.AddAnimation(0, _idleAnimation, true, 0f);
        }
        else if (data.FindAnimation(_idleAnimation) != null)
            _ipSpine.AnimationState.SetAnimation(0, _idleAnimation, true);
    }

    private void OnDisable()
    {
        CancelTweens();
        if (_ipSpine != null && _ipSpine.AnimationState != null) _ipSpine.AnimationState.ClearTracks();
    }

    private void CancelTweens()
    {
        _backgroundTween?.Kill();
        _mascotFade?.Kill();
        _backgroundTween = _mascotFade = null;
    }
}
