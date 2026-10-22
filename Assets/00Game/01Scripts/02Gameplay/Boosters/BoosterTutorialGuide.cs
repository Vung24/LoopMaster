using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BoosterTutorialGuide : MonoBehaviour
{
    private enum FocusAction { Move, Tool }
    private BoosterPresentationUI _presentation;
    private GameController _controller;
    private UICanvas _canvas;
    private IReadOnlyList<Box> _boxes;
    private string _key;
    [SerializeField] private RectTransform _root;
    [SerializeField] private Image _dimMask;
    private Tween _dimTween;
    private Finger _finger;
    private Box _targetBox;
    private FocusAction _action;
    private bool _accepted;
    private readonly CancellationTokenSource _disableLifetime = new CancellationTokenSource();
    private ToolBase _highlightedTool;
    private Button _highlightedToolButton;
    private Canvas _addedToolCanvas;
    private GraphicRaycaster _addedToolRaycaster;
    private Canvas _fingerCanvas;
    private bool _toolHadCanvas;
    private bool _toolPreviousOverride;
    private int _toolPreviousSortingOrder;

    public static async UniTask PlayGuide(GameController controller, UICanvas canvas,
        IReadOnlyList<Box> boxes, string key, CancellationToken token)
    {
        if (!BoosterUnlockProgress.IsFirstUseFree(key)) return;
        if (!BoosterTutorialRegistry.HasInteractiveGuide(key)) return;
        token.ThrowIfCancellationRequested();
        BoosterPresentationUI view = BoosterPresentationUI.Create(canvas.transform);
        BoosterTutorialGuide guide = view.ShowGuide();
        try
        {
            guide._controller = controller;
            guide._presentation = view;
            guide._canvas = canvas;
            guide._boxes = boxes;
            guide._key = key;
            guide.Initialize();
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token,
                guide.destroyCancellationToken, guide._disableLifetime.Token))
            {
                await guide.CreateFinger(lifetime.Token);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, lifetime.Token);
                Canvas.ForceUpdateCanvases();
                var context = new BoosterTutorialContext(guide, controller, key);
                await BoosterTutorialRegistry.GetStep(key).ExecuteAsync(context, lifetime.Token);
                if (!BoosterUnlockProgress.IsFirstUseFree(key)) GameplayToast.Show("Great!");
            }
        }
        finally
        {
            if (view != null) view.Dismiss();
        }
    }

    private void Initialize()
    {
        if (_root == null)
            throw new System.InvalidOperationException("BoosterPresentation/Tutorial needs its root RectTransform.");
        SetDim(false, animate: false);
    }

    public void SetDim(bool active, bool animate = true)
    {
        if (_dimMask == null) return;
        _dimTween?.Kill();
        if (!active)
        {
            if (animate && gameObject.activeInHierarchy)
            {
                _dimTween = _dimMask.DOFade(0f, 0.2f).SetLink(gameObject)
                    .OnComplete(() => _dimMask.gameObject.SetActive(false));
            }
            else
            {
                Color c = _dimMask.color;
                c.a = 0f;
                _dimMask.color = c;
                _dimMask.gameObject.SetActive(false);
            }
            return;
        }

        _dimMask.gameObject.SetActive(true);
        _dimMask.raycastTarget = true;
        if (animate && gameObject.activeInHierarchy)
        {
            Color c = _dimMask.color;
            c.a = 0f;
            _dimMask.color = c;
            _dimTween = _dimMask.DOFade(0.75f, 0.25f).SetLink(gameObject);
        }
        else
        {
            Color c = _dimMask.color;
            c.a = 0.75f;
            _dimMask.color = c;
        }
    }

    private async UniTask CreateFinger(CancellationToken token)
    {
        if (PrefabLibrary.Current == null || PrefabLibrary.Current.FingerPrefab == null) return;
        _finger = await Finger.Show(transform);
        token.ThrowIfCancellationRequested();
        foreach (Graphic graphic in _finger.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    public async UniTask<bool> GuideFirstMove(CancellationToken token)
    {
        if (_controller != null && _controller.Data != null && _controller.Data.CanUndo())
            return true;

        _action = FocusAction.Move;
        _targetBox = GetMovableBox();
        if (_targetBox == null)
        {
            Debug.LogWarning("Undo introduction needs a movable box; its free use remains available.", _controller);
            return false;
        }

        _presentation.SetInputBlocked(false);

        if (_finger != null)
        {
            Vector3 screen = _controller.MainCamera.WorldToScreenPoint(_targetBox.transform.position);
            Canvas canvas = _canvas.GetComponent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, camera, out Vector2 center);
            int baseSortingOrder = _presentation != null ? _presentation.SortingOrder : 1110;
            EnsureFingerSorting(baseSortingOrder + 2);
            _finger.ShowFinger(center);
            _finger.transform.localScale = Vector3.one * 0.6f;
        }

        await UniTask.WaitUntil(HasPlayerMoved, cancellationToken: token);

        if (_finger != null) _finger.HideFinger();

        await UniTask.WaitUntil(_controller.IsGameplaySettled, cancellationToken: token);
        return _controller.Data != null && _controller.Data.CanUndo();
    }

    private bool HasPlayerMoved()
    {
        if (_controller == null || _controller.Data == null) return false;
        return _controller.Data.CanUndo() || !_controller.IsGameplaySettled();
    }

    public async UniTask GuideToolUse(CancellationToken token)
    {
        await UniTask.WaitUntil(_controller.IsGameplaySettled, cancellationToken: token);
        if (!CanUseTool())
        {
            Debug.LogWarning("Booster introduction has no valid action; its free use remains available: " + _key, _controller);
            return;
        }
        Canvas.ForceUpdateCanvases();
        ToolBase tool = _canvas.GetTool(_key);
        if (tool == null) return;
        _action = FocusAction.Tool;
        _presentation.SetInputBlocked(true);
        SetDim(true);
        HighlightTool(tool);
        try
        {
            await WaitForTap(token);
        }
        finally
        {
            RestoreToolHighlight();
            SetDim(false);
        }
    }

    private void HighlightTool(ToolBase tool)
    {
        RestoreToolHighlight();
        _highlightedTool = tool;
        _highlightedToolButton = tool.GetComponent<Button>();
        if (_highlightedToolButton != null)
        {
            _highlightedToolButton.onClick.AddListener(OnToolClicked);
        }

        int baseSortingOrder = _presentation != null ? _presentation.SortingOrder : 1110;

        Canvas toolCanvas = tool.GetComponent<Canvas>();
        if (toolCanvas == null)
        {
            _addedToolCanvas = tool.gameObject.AddComponent<Canvas>();
            toolCanvas = _addedToolCanvas;
            _toolHadCanvas = false;
        }
        else
        {
            _toolHadCanvas = true;
            _toolPreviousOverride = toolCanvas.overrideSorting;
            _toolPreviousSortingOrder = toolCanvas.sortingOrder;
        }
        toolCanvas.overrideSorting = true;
        toolCanvas.sortingOrder = baseSortingOrder + 1;

        GraphicRaycaster toolRaycaster = tool.GetComponent<GraphicRaycaster>();
        if (toolRaycaster == null)
        {
            _addedToolRaycaster = tool.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (_finger != null)
        {
            Rect targetRect = GetFocusRect(tool.transform as RectTransform);
            EnsureFingerSorting(baseSortingOrder + 2);
            _finger.ShowFinger(targetRect.center);
            _finger.transform.localScale = Vector3.one * 0.6f;
        }
    }

    private void RestoreToolHighlight()
    {
        if (_highlightedToolButton != null)
        {
            _highlightedToolButton.onClick.RemoveListener(OnToolClicked);
            _highlightedToolButton = null;
        }
        if (_addedToolRaycaster != null)
        {
            Destroy(_addedToolRaycaster);
            _addedToolRaycaster = null;
        }
        if (_addedToolCanvas != null)
        {
            Destroy(_addedToolCanvas);
            _addedToolCanvas = null;
        }
        else if (_highlightedTool != null && _toolHadCanvas)
        {
            Canvas canvas = _highlightedTool.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = _toolPreviousOverride;
                canvas.sortingOrder = _toolPreviousSortingOrder;
            }
        }
        _highlightedTool = null;
    }

    private void OnToolClicked()
    {
        _accepted = true;
    }

    private void EnsureFingerSorting(int sortingOrder)
    {
        if (_finger == null) return;
        if (_fingerCanvas == null) _fingerCanvas = _finger.GetComponent<Canvas>();
        if (_fingerCanvas == null) _fingerCanvas = _finger.gameObject.AddComponent<Canvas>();
        _fingerCanvas.overrideSorting = true;
        _fingerCanvas.sortingOrder = sortingOrder;
    }

    private Rect GetFocusRect(RectTransform target)
    {
        Canvas sourceCanvas = _canvas != null ? _canvas.GetComponent<Canvas>().rootCanvas : target.GetComponentInParent<Canvas>().rootCanvas;
        Canvas overlayCanvas = _root.GetComponentInParent<Canvas>().rootCanvas;
        Camera sourceCamera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        Camera overlayCamera = overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlayCanvas.worldCamera;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, overlayCamera, out Vector2 local);
            minimum = Vector2.Min(minimum, local);
            maximum = Vector2.Max(maximum, local);
        }
        return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }

    private bool CanUseTool()
    {
        if (_key == InventoryKeys.TOOL_UNDO) return _controller.CanUseUndoTool;
        if (_key == InventoryKeys.TOOL_SHUFFLE) return _controller.CanUseShuffleTool;
        if (_key == InventoryKeys.TOOL_PATH) return _controller.CanUsePathTool;
        return true;
    }

    public async UniTask GuideShuffleTarget(CancellationToken token)
    {
        if (_finger != null) _finger.HideFinger();
        _presentation.SetInputBlocked(false);
        await UniTask.WaitUntil(ShuffleSelectionFinished, cancellationToken: token);
    }

    private bool ShuffleSelectionFinished()
    {
        return !_controller.IsSelectingShuffle || !BoosterUnlockProgress.IsFirstUseFree(_key);
    }

    private Box GetMovableBox()
    {
        for (int i = 0; i < _boxes.Count; i++)
        {
            Box box = _boxes[i];
            if (box == null || !box.isActiveAndEnabled || box.Info == null) continue;
            if (box.Info.State == BoxState.Free && box.CanPopupSlice()) return box;
        }
        return null;
    }

    private async UniTask WaitForTap(CancellationToken token)
    {
        _accepted = false;
        await UniTask.WaitUntil(WasAccepted, cancellationToken: token);
        if (_finger != null) _finger.HideFinger();
    }

    private bool WasAccepted()
    {
        if (_accepted) return true;
        if (_controller == null) return false;
        if (_action == FocusAction.Tool)
            return _controller.IsSelectingShuffle || !BoosterUnlockProgress.IsFirstUseFree(_key);
        return false;
    }

    private void OnDisable()
    {
        _disableLifetime.Cancel();
        SetDim(false, animate: false);
        if (_controller != null && _controller.IsSelectingShuffle) _controller.CancelShuffleSelection();
        RestoreToolHighlight();
    }

    private void OnDestroy()
    {
        _dimTween?.Kill();
        RestoreToolHighlight();
        _disableLifetime.Dispose();
    }
}
