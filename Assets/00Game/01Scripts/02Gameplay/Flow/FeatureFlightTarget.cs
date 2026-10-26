using UnityEngine;

internal sealed class FeatureFlightTarget
{
    private readonly Box _box;
    private readonly BoxInfo _info;
    private readonly int _version;
    private readonly Cube _cube;
    private readonly CubeInfo _cubeInfo;
    private readonly Box _partner;
    private readonly BoxInfo _partnerInfo;
    private readonly int _partnerVersion;
    private readonly CubeInfo _partnerCubeInfo;
    private readonly Transform _visual;
    private readonly Transform _partnerVisual;
    private readonly Renderer[] _renderers;
    private readonly Renderer[] _primaryRenderers;
    private readonly Renderer[] _partnerRenderers;
    private readonly SpriteRenderer _sprite;

    public FeatureFlightTarget(Box box, Transform visual, Cube cube = null, Box partner = null,
        Transform partnerVisual = null, Transform cable = null)
    {
        _box = box;
        _visual = visual;
        _partnerVisual = partnerVisual;
        _info = box.Info;
        _version = box.RoutineVersion;
        _cube = cube;
        if (cube != null) _cubeInfo = cube.Info;
        _partner = partner;
        if (partner != null)
        {
            _partnerInfo = partner.Info;
            _partnerVersion = partner.RoutineVersion;
            _partnerCubeInfo = partner.Info.PeekTopCube();
        }
        _primaryRenderers = visual.GetComponentsInChildren<Renderer>(false);
        _partnerRenderers = System.Array.Empty<Renderer>();
        if (partnerVisual != null) _partnerRenderers = partnerVisual.GetComponentsInChildren<Renderer>(false);
        var renderers = new System.Collections.Generic.List<Renderer>(_primaryRenderers);
        renderers.AddRange(_partnerRenderers);
        if (cable != null) renderers.AddRange(cable.GetComponentsInChildren<Renderer>(false));
        _renderers = renderers.ToArray();
        if (_renderers.Length == 1) _sprite = _renderers[0] as SpriteRenderer;
    }

    private bool IsCurrent()
    {
        if (_box == null || !_box.IsCurrentRoutine(_version) || _box.Info != _info ||
            !_box.IsEntrancePresentationReady) return false;
        if (_visual == null || !_visual.gameObject.activeInHierarchy) return false;
        if (_cubeInfo != null && (_cube == null || _cube.Info != _cubeInfo ||
            _cubeInfo.CubeSc != _cube || !_info.CubeInfos.Contains(_cubeInfo) || _cube.VisibleSize <= 0)) return false;
        if (_partnerInfo != null && (_partner == null || !_partner.IsCurrentRoutine(_partnerVersion) ||
            _partner.Info != _partnerInfo || !_partner.IsEntrancePresentationReady ||
            _partnerCubeInfo == null || _partner.Info.PeekTopCube() != _partnerCubeInfo || _partnerCubeInfo.CubeSc == null ||
            _partnerCubeInfo.CubeSc.VisibleSize <= 0 || _partnerVisual == null ||
            !_partnerVisual.gameObject.activeInHierarchy)) return false;
        return true;
    }

    public bool TryGetPose(Camera camera, RectTransform overlay, Camera overlayCamera, out FeatureFlightPose pose)
    {
        pose = default;
        if (camera == null || overlay == null || !IsCurrent()) return false;
        if (!HasVisibleRenderer(_primaryRenderers, camera)) return false;
        if (_partner != null && !HasVisibleRenderer(_partnerRenderers, camera)) return false;
        if (!FeatureFlightProjection.TryGetScreenRect(_renderers, camera, out Rect screen)) return false;
        if (_sprite != null) return FeatureFlightProjection.TryProjectSprite(_sprite, camera, overlay, overlayCamera, out pose);
        return FeatureFlightProjection.TryProjectQuad(screen.min, new Vector2(screen.xMax, screen.yMin),
            new Vector2(screen.xMin, screen.yMax), overlay, overlayCamera, out pose);
    }

    private static bool HasVisibleRenderer(Renderer[] renderers, Camera camera)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer) continue;
            if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0) return true;
        }
        return false;
    }
}

