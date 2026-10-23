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
        await PlayFlight(parent, sprite, origin, Vector2.zero, tool,
            () => RevealTool(tool), token);
    }

    /// <summary>Flies the popup artwork into the HUD and hands it over once on arrival.</summary>
    public static async UniTask PlayFlight(Transform parent, Sprite sprite, Vector3 origin,
        Vector2 originSize, ToolBase tool, Action onArrived, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (tool == null) return;
        RectTransform target = tool.transform as RectTransform;
        RectTransform targetIcon = tool.IconRectTransform != null ? tool.IconRectTransform : target;
        await PlayFlightInternal(parent, sprite, origin, originSize, target, targetIcon, onArrived, token);
    }

    public static async UniTask PlayFlight(Transform parent, Sprite sprite, Vector3 origin,
        RectTransform target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target == null) return;
        await PlayFlightInternal(parent, sprite, origin, Vector2.zero, target, target, null, token);
    }

    private static async UniTask PlayFlightInternal(Transform parent, Sprite sprite, Vector3 origin, Vector2 originSize,
        RectTransform target, RectTransform targetIcon, Action onArrived, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target == null) return;
        if (targetIcon == null) targetIcon = target;

        BoosterPresentationUI view = BoosterPresentationUI.Create(parent);
        RectTransform overlay = (RectTransform)view.transform;
        var icon = view.ShowFlight();
        icon.sprite = sprite;
        icon.preserveAspect = false;
        RectTransform item = icon.rectTransform;
        item.position = origin;

        Vector3 destination = targetIcon.TransformPoint(targetIcon.rect.center);
        Vector3 middle = (origin + destination) * 0.5f + overlay.up * (150f * overlay.lossyScale.y);
        Vector3 initialScale = target.localScale;

        Vector3 endScale = Vector3.one * (144f / 210f);
        float overlayScaleX = Mathf.Abs(overlay.lossyScale.x);
        float overlayScaleY = Mathf.Abs(overlay.lossyScale.y);
        float targetScaleX = Mathf.Abs(targetIcon.lossyScale.x);
        float targetScaleY = Mathf.Abs(targetIcon.lossyScale.y);
        Vector3 startScale = Vector3.one;
        if (overlayScaleX > 0.0001f && overlayScaleY > 0.0001f && item.rect.width > 0.0001f && item.rect.height > 0.0001f)
        {
            Vector2 targetSize = targetIcon.rect.size;
            var targetImage = targetIcon.GetComponent<UnityEngine.UI.Image>();
            if (targetImage != null && targetImage.preserveAspect && sprite != null && sprite.rect.width > 0f && sprite.rect.height > 0f)
            {
                float fit = Mathf.Min(targetSize.x / sprite.rect.width, targetSize.y / sprite.rect.height);
                targetSize = sprite.rect.size * fit;
            }
            float scaleX = (targetSize.x * targetScaleX) / (item.rect.width * overlayScaleX);
            float scaleY = (targetSize.y * targetScaleY) / (item.rect.height * overlayScaleY);
            if (scaleX > 0.01f && scaleY > 0.01f)
            {
                endScale = new Vector3(scaleX, scaleY, 1f);
            }
            if (originSize.x > 0f && originSize.y > 0f)
                startScale = new Vector3(originSize.x / (item.rect.width * overlayScaleX),
                    originSize.y / (item.rect.height * overlayScaleY), 1f);
        }
        item.localScale = startScale;

        Sequence flight = null;
        Tween arrival = null;
        try
        {
            flight = DOTween.Sequence().SetAutoKill(false).SetLink(overlay.gameObject);
            flight.Append(item.DOScale(new Vector3(startScale.x * 1.06f, startScale.y * 1.06f, 1f), 0.15f).SetEase(Ease.OutQuad));
            flight.Append(item.DOPath(new[] { origin, middle, destination }, 0.65f, PathType.CatmullRom)
                .SetEase(Ease.InOutQuad));
            flight.Join(item.DOScale(endScale, 0.65f).SetEase(Ease.InOutQuad));

            GameAssetsConfig config = GameAssetsConfig.Instance;
            if (AudioManager.Current != null && config != null && config.SlicePopOut != null)
                AudioManager.Current.PlayOneShot(config.SlicePopOut);

            await UniTask.WaitUntil(() => !flight.IsActive() || flight.IsComplete(), cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (!flight.IsComplete() || target == null || targetIcon == null || item == null) return;

            // Ensure exact final position, scale and rotation at the arrival frame
            item.position = destination;
            item.localScale = endScale;
            item.rotation = targetIcon.rotation;

            // Handover at the exact arrival frame: unlock target and hide flight icon
            onArrived?.Invoke();
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
            if (view != null) view.Dismiss();
        }
    }

    private static void RevealTool(ToolBase tool)
    {
        if (tool != null) tool.SetIntroLocked(false);
    }
}
