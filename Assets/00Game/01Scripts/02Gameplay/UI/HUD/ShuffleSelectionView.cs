using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShuffleSelectionView : MonoBehaviour
{
    [SerializeField] private GameObject _mask;
    [SerializeField] private Button _closeButton;
    [SerializeField] private RawImage _targets;
    private GameController _controller;
    private ShuffleSelectionHighlight _highlight;

    public void Bind(GameController controller)
    {
        if (_controller == controller) return;
        CancelSelection();
        _controller = controller;
    }

    public bool Show(Camera camera, IReadOnlyList<Box> boxes)
    {
        Hide();
        if (_controller == null || _mask == null || _closeButton == null || _targets == null)
        {
            Debug.LogError("ShuffleSelectionView requires its controller, mask, close button and targets references.", this);
            return false;
        }
        if (camera == null || LayerMask.NameToLayer("Top") < 0)
        {
            Debug.LogError("Shuffle highlight requires the gameplay camera and the Top layer.", this);
            return false;
        }

        // Physics input passes through the mask; eligible boxes render above its dim.
        Button backgroundButton = _mask.GetComponent<Button>();
        if (backgroundButton != null) backgroundButton.enabled = false;
        foreach (Graphic graphic in _mask.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = graphic.transform.IsChildOf(_closeButton.transform);
        Image background = _mask.GetComponent<Image>();
        if (background != null) background.color = new Color(0f, 0f, 0f, 0.9411765f);
        _highlight = new ShuffleSelectionHighlight(camera, _targets, boxes);
        _highlight.Refresh(_controller);
        Snui.Snui_Text.EnsureFontAssets(_mask.transform);
        _closeButton.onClick.AddListener(CancelSelection);
        _mask.SetActive(true);
        return true;
    }

    public void Refresh()
    {
        if (_highlight == null) return;
        if (_controller == null || !_controller.IsSelectingShuffle)
        {
            Hide();
            return;
        }
        _highlight.Refresh(_controller);
    }

    public void CancelSelection()
    {
        if (_controller != null && _controller.IsSelectingShuffle)
            _controller.CancelShuffleSelection();
        Hide();
    }

    public void Hide()
    {
        _highlight?.Dispose();
        _highlight = null;
        if (_closeButton != null) _closeButton.onClick.RemoveListener(CancelSelection);
        if (_mask != null) _mask.SetActive(false);
    }

    private void OnDisable()
    {
        CancelSelection();
    }

    private void OnDestroy()
    {
        Hide();
    }
}
