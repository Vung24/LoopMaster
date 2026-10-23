using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects a newly introduced feature onto its existing gameplay visual.</summary>
internal sealed class FeatureUnlockFlightPresenter
{
    private readonly GameController _controller;
    private readonly IReadOnlyList<Box> _boxes;
    private readonly FeatureType _feature;
    private readonly GameData _data;
    private readonly Transform _parent;
    private readonly Vector3 _timing;
    private BoosterPresentationUI _view;
    private RectTransform _overlay;
    private Camera _overlayCamera;
    private Image _icon;
    private Image _source;
    private FeatureFlightTarget _target;
    private FeatureFlightPose _origin;
    private FeatureFlightPose _destination;
    private Color _color;
    private Rect _viewport;
    private Vector2Int _screenSize;

    public FeatureUnlockFlightPresenter(GameController controller, IReadOnlyList<Box> boxes,
        FeatureType feature, Transform parent, Vector3 timing)
    {
        _controller = controller;
        _boxes = boxes;
        _feature = feature;
        _parent = parent;
        _data = controller.Data;
        _timing = new Vector3(Mathf.Clamp(timing.x, .01f, .5f), Mathf.Clamp(timing.y, .1f, 1f),
            Mathf.Clamp(timing.z, .01f, .5f));
    }

    /// <summary>Clones the visible icon before its popup starts closing; skips unavailable targets.</summary>
    public void Prepare(Image source)
    {
        if (_view != null) return;
        try
        {
            if (source == null || source.sprite == null || !IsLevelCurrent())
            {
                ReportSkipped();
                return;
            }
            _view = BoosterPresentationUI.Create(_parent);
            _overlay = (RectTransform)_view.transform;
            _overlayCamera = FeatureFlightProjection.CanvasCamera(_view.GetComponent<Canvas>());
            _target = FeatureFlightTargetSelector.Select(_feature, _controller, _boxes, _overlay, _overlayCamera);
            if (_target == null || !FeatureFlightProjection.TryCapture(source, _overlay, _overlayCamera, out _origin) ||
                !_target.TryGetPose(_controller.MainCamera, _overlay, _overlayCamera, out _destination))
            {
                ReportSkipped();
                Dismiss();
                return;
            }
            InitializeIcon(source);
            _viewport = _controller.MainCamera.pixelRect;
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _view.SetInputBlocked(true);
            _source = source;
            source.enabled = false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, _controller);
            Dismiss();
        }
    }

    private void InitializeIcon(Image source)
    {
        _icon = _view.ShowFlight();
        _icon.sprite = source.overrideSprite;
        _icon.useSpriteMesh = source.useSpriteMesh;
        _icon.material = source.material;
        _icon.preserveAspect = false;
        _icon.raycastTarget = false;
        _color = source.color;
        _color.a *= source.canvasRenderer.GetInheritedAlpha();
        _icon.color = _color;
        RectTransform rect = _icon.rectTransform;
        rect.anchorMin = rect.anchorMax = _overlay.pivot;
        rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one;
        ApplyPose(_origin);
    }

    /// <summary>Runs the prepared flight and releases its view on completion or interruption.</summary>
    public async UniTask Play(CancellationToken token)
    {
        if (_view == null) return;
        float elapsed = 0f;
        float flightEnd = _timing.x + _timing.y;
        float duration = flightEnd + _timing.z;
        try
        {
            while (elapsed < duration)
            {
                // Run after camera/cable LateUpdate, before PostLateUpdate rebuilds and renders the Canvas.
                await UniTask.Yield(PlayerLoopTiming.LastPreLateUpdate, token);
                if (!IsLevelCurrent() || _view == null || !_view.isActiveAndEnabled || _icon == null || !ViewportUnchanged() ||
                    !_target.TryGetPose(_controller.MainCamera, _overlay, _overlayCamera, out _destination)) return;
                elapsed += Time.deltaTime;
                Animate(elapsed, flightEnd);
            }
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            Dismiss();
        }
    }

    private void Animate(float elapsed, float flightEnd)
    {
        if (elapsed < _timing.x)
        {
            FeatureFlightPose departure = _origin;
            departure.Size *= 1f + .05f * Mathf.Sin(elapsed / _timing.x * Mathf.PI);
            ApplyPose(departure);
            return;
        }
        float progress = Mathf.Clamp01((elapsed - _timing.x) / _timing.y);
        float eased = progress * progress * (3f - 2f * progress);
        FeatureFlightPose current = _destination;
        current.Center = Vector2.Lerp(_origin.Center, _destination.Center, eased);
        current.Center.y += Mathf.Sin(progress * Mathf.PI) * Mathf.Min(100f, _overlay.rect.height * .08f);
        current.Size = Vector2.Lerp(_origin.Size, FitAspect(_destination.Size, _origin.Size), eased);
        current.Angle = Mathf.LerpAngle(_origin.Angle, _destination.Angle, eased);
        ApplyPose(current);
        Color color = _color;
        color.a *= 1f - Mathf.InverseLerp(.75f, 1f, progress);
        _icon.color = color;
        if (elapsed >= flightEnd) AnimateLanding((elapsed - flightEnd) / _timing.z);
    }

    private void AnimateLanding(float progress)
    {
        progress = Mathf.Clamp01(progress);
        // Reuse the icon as a brief highlight; never mutate the target's material or transform.
        FeatureFlightPose pose = _destination;
        pose.Size = FitAspect(pose.Size, _origin.Size) * (1f + .08f * Mathf.Sin(progress * Mathf.PI));
        ApplyPose(pose);
        _icon.color = new Color(1f, 1f, 1f, .22f * Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI));
    }

    private static Vector2 FitAspect(Vector2 destination, Vector2 source)
    {
        float scale = Mathf.Min(destination.x / source.x, destination.y / source.y);
        return source * scale;
    }

    private void ApplyPose(FeatureFlightPose pose)
    {
        RectTransform rect = _icon.rectTransform;
        rect.anchoredPosition3D = new Vector3(pose.Center.x, pose.Center.y, 0f);
        rect.sizeDelta = pose.Size;
        rect.localRotation = Quaternion.Euler(0f, 0f, pose.Angle);
    }

    private bool IsLevelCurrent()
    {
        return _controller != null && _controller.isActiveAndEnabled && _controller.Data == _data &&
            _controller.EndState == GameEndState.Playing && Time.timeScale > 0f &&
            _parent != null && _parent.gameObject.activeInHierarchy;
    }

    private bool ViewportUnchanged()
    {
        return _controller.MainCamera != null && _controller.MainCamera.pixelRect == _viewport &&
            Screen.width == _screenSize.x && Screen.height == _screenSize.y;
    }

    private void ReportSkipped()
    {
        Debug.LogWarning("Feature introduction flight skipped: no ready visible target/icon for " + _feature +
            " at level " + _data.GetPuzzleActualLevel() + ". Check feature visual references and entrance readiness.", _controller);
    }

    public void Dismiss()
    {
        if (_source != null) _source.enabled = true;
        _source = null;
        if (_view != null) _view.Dismiss();
        _view = null;
        _icon = null;
        _target = null;
    }
}
