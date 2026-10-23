using UnityEngine;
using UnityEngine.UI;

internal static class FeatureFlightProjection
{
    public static Camera CanvasCamera(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera;
    }

    public static bool TryCapture(Image source, RectTransform overlay, Camera overlayCamera, out FeatureFlightPose pose)
    {
        pose = default;
        if (source == null || source.sprite == null || !source.isActiveAndEnabled) return false;
        Rect rect = source.GetPixelAdjustedRect();
        Sprite sprite = source.overrideSprite;
        Vector2 spriteSize = sprite.rect.size;
        if (source.preserveAspect && rect.width > 0f && rect.height > 0f)
        {
            float ratio = Mathf.Min(rect.width / spriteSize.x, rect.height / spriteSize.y);
            Vector2 fitted = spriteSize * ratio;
            rect.position += Vector2.Scale(rect.size - fitted, source.rectTransform.pivot);
            rect.size = fitted;
        }
        // The cloned Image uses the same sprite, so Unity applies its mesh padding on both sides.
        Camera camera = CanvasCamera(source.canvas);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera,
            source.rectTransform.TransformPoint(new Vector3(rect.xMin, rect.yMin)));
        Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera,
            source.rectTransform.TransformPoint(new Vector3(rect.xMax, rect.yMin)));
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera,
            source.rectTransform.TransformPoint(new Vector3(rect.xMin, rect.yMax)));
        return TryProjectQuad(bottomLeft, bottomRight, topLeft, overlay, overlayCamera, out pose);
    }

    public static bool TryProjectQuad(Vector2 bottomLeft, Vector2 bottomRight, Vector2 topLeft,
        RectTransform overlay, Camera overlayCamera, out FeatureFlightPose pose)
    {
        pose = default;
        Camera camera = overlayCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, bottomLeft, camera, out Vector2 left) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, bottomRight, camera, out Vector2 right) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, topLeft, camera, out Vector2 top)) return false;
        Vector2 horizontal = right - left;
        Vector2 vertical = top - left;
        if (horizontal.sqrMagnitude < .01f || vertical.sqrMagnitude < .01f) return false;
        pose = new FeatureFlightPose((right + top) * .5f,
            new Vector2(horizontal.magnitude, vertical.magnitude), Mathf.Atan2(horizontal.y, horizontal.x) * Mathf.Rad2Deg);
        return true;
    }

    public static bool TryGetScreenRect(Renderer[] renderers, Camera camera, out Rect rect)
    {
        rect = default;
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer is ParticleSystemRenderer) continue;
            if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude < .000001f) continue;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = bounds.min;
                if ((corner & 1) != 0) world.x = bounds.max.x;
                if ((corner & 2) != 0) world.y = bounds.max.y;
                if ((corner & 4) != 0) world.z = bounds.max.z;
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z < camera.nearClipPlane || screen.z > camera.farClipPlane) return false;
                minimum = Vector2.Min(minimum, screen);
                maximum = Vector2.Max(maximum, screen);
                any = true;
            }
        }
        if (!any) return false;
        rect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        Rect viewport = camera.pixelRect;
        return rect.width > 1f && rect.height > 1f &&
            viewport.Contains(rect.min) && viewport.Contains(rect.max);
    }

    public static bool TryProjectSprite(SpriteRenderer sprite, Camera camera, RectTransform overlay, Camera overlayCamera,
        out FeatureFlightPose pose)
    {
        pose = default;
        if (sprite == null || sprite.sprite == null || !sprite.enabled || !sprite.gameObject.activeInHierarchy) return false;
        Bounds bounds = sprite.localBounds;
        Vector3 bottomLeft = sprite.transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
        Vector3 bottomRight = sprite.transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
        Vector3 topLeft = sprite.transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
        return TryProjectQuad(camera.WorldToScreenPoint(bottomLeft), camera.WorldToScreenPoint(bottomRight),
            camera.WorldToScreenPoint(topLeft), overlay, overlayCamera, out pose);
    }
}

