using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public static class BoosterFlyPresenter
{
    public static async UniTask PlayFlight(Transform parent, Sprite sprite, Vector3 origin,
        ToolBase tool, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (tool == null) return;
        RectTransform target = tool.transform as RectTransform;
        RectTransform targetIcon = tool.IconRectTransform != null ? tool.IconRectTransform : target;
        await PlayFlightInternal(parent, sprite, origin, target, targetIcon, () => tool.SetIntroLocked(false), token);
    }

    public static async UniTask PlayFlight(Transform parent, Sprite sprite, Vector3 origin,
        RectTransform target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target == null) return;
        await PlayFlightInternal(parent, sprite, origin, target, target, null, token);
    }

    private static async UniTask PlayFlightInternal(Transform parent, Sprite sprite, Vector3 origin,
        RectTransform target, RectTransform targetIcon, Action onArrived, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target == null) return;
        if (targetIcon == null) targetIcon = target;

        BoosterPresentationUI view = BoosterPresentationUI.Create(parent);
        RectTransform overlay = (RectTransform)view.transform;
        var icon = view.ShowFlight();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        RectTransform item = icon.rectTransform;
        item.position = origin;

        Vector3 destination = targetIcon.TransformPoint(targetIcon.rect.center);
        Vector3 middle = (origin + destination) * 0.5f + overlay.up * (150f * overlay.lossyScale.y);
        Vector3 initialScale = target.localScale;

        // Match endScale so item's rendered screen bounds precisely match targetIcon at the arrival frame
        Vector3 endScale = Vector3.one * (144f / 210f);
        float overlayScaleX = Mathf.Abs(overlay.lossyScale.x);
        float overlayScaleY = Mathf.Abs(overlay.lossyScale.y);
        float targetScaleX = Mathf.Abs(targetIcon.lossyScale.x);
        float targetScaleY = Mathf.Abs(targetIcon.lossyScale.y);
        if (overlayScaleX > 0.0001f && overlayScaleY > 0.0001f && item.rect.width > 0.0001f && item.rect.height > 0.0001f)
        {
            float scaleX = (targetIcon.rect.width * targetScaleX) / (item.rect.width * overlayScaleX);
            float scaleY = (targetIcon.rect.height * targetScaleY) / (item.rect.height * overlayScaleY);
            if (scaleX > 0.01f && scaleY > 0.01f)
            {
                endScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        Sequence flight = null;
        Tween arrival = null;
        try
        {
            flight = DOTween.Sequence().SetLink(overlay.gameObject);
            flight.Append(item.DOScale(1.15f, 0.15f).SetEase(Ease.OutQuad));
            flight.Append(item.DOPath(new[] { origin, middle, destination }, 0.65f, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad));
            flight.Join(item.DOScale(endScale, 0.65f).SetEase(Ease.InOutQuad));

            GameAssetsConfig config = GameAssetsConfig.Instance;
            if (AudioManager.Current != null && config != null && config.SlicePopOut != null)
                AudioManager.Current.PlayOneShot(config.SlicePopOut);

            await UniTask.WaitUntil(() => !flight.IsActive() || flight.IsComplete(), cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (target == null) return;

            // Ensure exact final position, scale and rotation at the arrival frame
            item.position = destination;
            item.localScale = endScale;
            item.rotation = targetIcon.rotation;

            // Handover at the exact arrival frame: unlock target and hide flight icon
            if (onArrived != null) onArrived();
            icon.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();

            // Punch feedback on the newly unlocked target
            arrival = target.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f).SetLink(target.gameObject);
            if (AudioManager.Current != null && config != null && config.ColorLockOpen != null)
                AudioManager.Current.PlayOneShot(config.ColorLockOpen);

            await UniTask.WaitUntil(() => !arrival.IsActive() || arrival.IsComplete(), cancellationToken: token);
        }
        finally
        {
            flight?.Kill();
            arrival?.Kill();
            if (target != null) target.localScale = initialScale;
            if (onArrived != null) onArrived();
            if (view != null) view.Dismiss();
        }
    }
}
