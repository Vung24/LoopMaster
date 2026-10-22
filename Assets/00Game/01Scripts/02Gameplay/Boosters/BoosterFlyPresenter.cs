using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public static class BoosterFlyPresenter
{
    public static async UniTask PlayFlight(Transform parent, Sprite sprite, Vector3 origin,
        RectTransform target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target == null) return;
        BoosterPresentationUI view = BoosterPresentationUI.Create(parent);
        RectTransform overlay = (RectTransform)view.transform;
        var icon = view.ShowFlight();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        RectTransform item = icon.rectTransform;
        item.position = origin;
        Vector3 destination = target.TransformPoint(target.rect.center);
        Vector3 middle = (origin + destination) * 0.5f + overlay.up * (150f * overlay.lossyScale.y);
        Vector3 scale = target.localScale;
        Sequence flight = null;
        Tween arrival = null;
        try
        {
            flight = DOTween.Sequence().SetLink(overlay.gameObject);
            flight.Append(item.DOScale(1.2f, 0.15f));
            flight.Append(item.DOPath(new[] { origin, middle, destination }, 0.65f, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad));
            flight.Join(item.DOScale(0.5f, 0.65f));
            GameAssetsConfig config = GameAssetsConfig.Instance;
            if (AudioManager.Current != null && config != null && config.SlicePopOut != null)
                AudioManager.Current.PlayOneShot(config.SlicePopOut);
            await UniTask.WaitUntil(() => !flight.IsActive() || flight.IsComplete(), cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (target == null) return;
            icon.gameObject.SetActive(false);
            arrival = target.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f).SetLink(target.gameObject);
            if (AudioManager.Current != null && config != null && config.ColorLockOpen != null)
                AudioManager.Current.PlayOneShot(config.ColorLockOpen);
            await UniTask.WaitUntil(() => !arrival.IsActive() || arrival.IsComplete(), cancellationToken: token);
        }
        finally
        {
            flight?.Kill();
            arrival?.Kill();
            if (target != null) target.localScale = scale;
            if (view != null) view.Dismiss();
        }
    }
}
