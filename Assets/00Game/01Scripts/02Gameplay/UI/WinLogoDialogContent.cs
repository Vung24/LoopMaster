using System.Collections;
using Snui;
using Spine.Unity;
using UnityEngine;

public class WinLogoDialogContent : DialogContent
{
    [SerializeField] private AudioClip _winPre;
    [SerializeField] private AudioClip _winIp;
    [SerializeField] private SkeletonGraphic _logo;
    [SerializeField] private SkeletonGraphic _ip;
    private Coroutine _animation;

    protected override void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        SetGraphicsActive(false);
    }

    public void PlayLogoAnimation()
    {
        CancelAnimation();
        SetGraphicsActive(false);
        base.Show();
        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        _animation = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        PlayAudio(_winPre);
        var logoEntry = PlayGraphic(_logo);
        if (logoEntry != null) yield return new WaitForSecondsRealtime(1.76f);
        PlayAudio(_winIp);
        var ipEntry = PlayGraphic(_ip);
        Dismiss();
        while (ipEntry != null && _ip != null && _ip.isActiveAndEnabled && !ipEntry.IsComplete)
            yield return null;
        _animation = null;
        Hide();
    }

    private static Spine.TrackEntry PlayGraphic(SkeletonGraphic graphic)
    {
        if (graphic == null || graphic.skeletonDataAsset == null) return null;
        graphic.gameObject.SetActive(true);
        graphic.UnscaledTime = true;
        graphic.Initialize(false);
        var data = graphic.skeletonDataAsset.GetSkeletonData(false);
        if (data == null || data.FindAnimation("animation") == null || graphic.AnimationState == null)
        {
            graphic.gameObject.SetActive(false);
            return null;
        }
        graphic.Skeleton.SetToSetupPose();
        graphic.AnimationState.ClearTracks();
        return graphic.AnimationState.SetAnimation(0, "animation", false);
    }

    private void PlayAudio(AudioClip clip)
    {
        if (AudioManager.Current != null) AudioManager.Current.PlayOneShot(clip, gameObject);
    }

    public void OnSkipAnims()
    {
        if (!gameObject.activeSelf) return;
        CancelAnimation();
        Dismiss();
        Hide();
    }

    public override void Dismiss(int result = RESULT_CLOSE) { _result = result; }

    public override void Hide()
    {
        CancelAnimation();
        SetGraphicsActive(false);
        base.Hide();
    }

    private void OnDisable() { CancelAnimation(); }

    private void SetGraphicsActive(bool active)
    {
        if (_logo != null) _logo.gameObject.SetActive(active);
        if (_ip != null) _ip.gameObject.SetActive(active);
    }

    private void CancelAnimation()
    {
        if (_animation != null) StopCoroutine(_animation);
        _animation = null;
        if (AudioManager.Current != null) AudioManager.Current.StopAudio(gameObject);
    }
}
