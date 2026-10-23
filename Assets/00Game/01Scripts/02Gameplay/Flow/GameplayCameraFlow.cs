using UnityEngine;

public interface IGameplayViewport
{
    Rect GetGameplayViewport(Camera camera);
}

public sealed class GameplayCameraFlow
{
    private Bounds _bounds;
    private Rect _viewport;
    private Rect _pixels;
    private float _aspect;
    private bool _ready;
    private bool _pending;
    private Camera _lastMain;
    public void Reset(GameData data, Transform root)
    {
        if (!GameplayCameraLayout.GetBounds(root, out _bounds))
            _bounds = GameplayCameraLayout.FallbackBounds(data, root);
        _ready = data != null;
        _pending = true;
    }

    public void Update(Camera main, IGameplayViewport viewportProvider, int level)
    {
        if (!_ready || main == null || !main.orthographic) return;
        if (_pending) Canvas.ForceUpdateCanvases();
        Rect pixels = main.pixelRect;
        Rect viewport = viewportProvider == null
            ? GameplayCameraLayout.Viewport(pixels, Screen.safeArea, 0, 0)
            : viewportProvider.GetGameplayViewport(main);
        if (!_pending && main == _lastMain && pixels == _pixels &&
            viewport == _viewport && Mathf.Approximately(_aspect, main.aspect)) return;
        GameplayCameraLayout.Fit(main, _bounds, viewport, level);
        _viewport = viewport;
        _pixels = pixels;
        _aspect = main.aspect;
        _lastMain = main;
        _pending = false;
    }
}
