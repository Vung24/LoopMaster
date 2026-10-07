using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using System.Collections.Generic;

public class SpecialStreakFailBanner : MonoBehaviour
{
    [SerializeField] private Timer _timer;
    private Tween _tween;
    private readonly List<Material> _materials = new List<Material>();
    private void Awake() => SpecialStreakPresentation.Prepare(transform, _materials);
    public static UniTask<SpecialStreakFailBanner> Show(Transform parent)
    {
        var manager = SpecialStreakManager.Ensure();
        var prefab = DataPrefabLibrary.Current?.SpecialStreakFailBannerPrefab;
        if (parent == null || prefab == null || !manager.ShouldShowFailBanner()) return UniTask.FromResult<SpecialStreakFailBanner>(null);
        var banner = Instantiate(prefab, parent, false).GetComponent<SpecialStreakFailBanner>();
        banner?.PlayShowAnim(); return UniTask.FromResult(banner);
    }
    public void PlayShowAnim()
    {
        SpecialStreakPresentation.SetActive(_timer, false);
        var slider = GetComponentInChildren<SpecialStreakMultiplierSlider>(true);
        if (slider != null) { slider.Type = SpecialStreakMultiplierSliderType.FailBanner; slider.Init(SpecialStreakManager.Ensure().LastMultiplierIndex); slider.PlayLoseMultiplierAnim(); }
        var rect = transform as RectTransform;
        if (rect == null) return;
        var p = rect.anchoredPosition; p.y = -290; rect.anchoredPosition = p;
        _tween?.Kill();
        _tween = DOTween.Sequence().Append(rect.DOAnchorPosY(11, .1f).SetEase(Ease.OutQuad))
            .Append(rect.DOAnchorPosY(0, .1f).SetEase(Ease.InOutSine)).SetUpdate(true).SetLink(gameObject);
    }
    private void OnDisable() { _tween?.Kill(); }
    private void OnDestroy() => SpecialStreakPresentation.Cleanup(_materials);
}
