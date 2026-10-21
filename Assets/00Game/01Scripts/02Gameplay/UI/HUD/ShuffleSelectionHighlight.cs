using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Uses the gameplay camera for a separate Top-layer pass above the shuffle dim.
public sealed class ShuffleSelectionHighlight : IDisposable
{
    private sealed class Target
    {
        public Box Box;
        public bool IsHighlighted;
        public readonly List<Transform> Children = new List<Transform>();
        public readonly List<int> OriginalLayers = new List<int>();

        public void SetHighlighted(bool highlighted, int layer)
        {
            if (IsHighlighted == highlighted) return;
            IsHighlighted = highlighted;
            if (highlighted)
            {
                Box.GetComponentsInChildren<Transform>(true, Children);
                for (int i = 0; i < Children.Count; i++)
                {
                    OriginalLayers.Add(Children[i].gameObject.layer);
                    Children[i].gameObject.layer = layer;
                }
                return;
            }
            for (int i = 0; i < Children.Count; i++)
            {
                Transform child = Children[i];
                if (child != null) child.gameObject.layer = OriginalLayers[i];
            }
            Children.Clear();
            OriginalLayers.Clear();
        }
    }

    private readonly Camera _source;
    private readonly RawImage _image;
    private readonly int _layer;
    private readonly List<Target> _targets = new List<Target>();
    private RenderTexture _texture;
    private bool _disposed;

    public ShuffleSelectionHighlight(Camera source, RawImage image, IReadOnlyList<Box> boxes)
    {
        _source = source;
        _layer = LayerMask.NameToLayer("Top");
        _image = image;
        _image.raycastTarget = false;
        for (int i = 0; i < boxes.Count; i++)
            if (boxes[i] != null) _targets.Add(new Target { Box = boxes[i] });
    }

    public void Refresh(GameController controller)
    {
        if (_disposed || _source == null || controller == null) return;
        for (int i = 0; i < _targets.Count; i++)
        {
            Target target = _targets[i];
            target.SetHighlighted(controller.CanSelectShuffleBox(target.Box), _layer);
        }

        int width = Mathf.Max(1, _source.pixelWidth);
        int height = Mathf.Max(1, _source.pixelHeight);
        if (_texture == null || _texture.width != width || _texture.height != height)
        {
            ReleaseTexture();
            _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _texture.name = "ShuffleHighlight";
            _texture.Create();
            _image.texture = _texture;
        }
        RectTransform rect = _image.rectTransform;
        rect.anchorMin = _source.rect.min;
        rect.anchorMax = _source.rect.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        RenderHighlight();
    }

    private void RenderHighlight()
    {
        // Called from Update/LateUpdate, never from a camera render callback.
        // Restore every changed setting before the normal gameplay render.
        int cullingMask = _source.cullingMask;
        CameraClearFlags clearFlags = _source.clearFlags;
        Color background = _source.backgroundColor;
        RenderTexture target = _source.targetTexture;
        RenderTexture active = RenderTexture.active;
        bool allowHDR = _source.allowHDR;
        bool allowMSAA = _source.allowMSAA;
        bool occlusion = _source.useOcclusionCulling;
        try
        {
            _source.cullingMask = 1 << _layer;
            _source.clearFlags = CameraClearFlags.SolidColor;
            _source.backgroundColor = Color.clear;
            _source.targetTexture = _texture;
            _source.allowHDR = false;
            _source.allowMSAA = false;
            _source.useOcclusionCulling = false;
            _source.Render();
        }
        finally
        {
            _source.targetTexture = target;
            _source.cullingMask = cullingMask;
            _source.clearFlags = clearFlags;
            _source.backgroundColor = background;
            _source.allowHDR = allowHDR;
            _source.allowMSAA = allowMSAA;
            _source.useOcclusionCulling = occlusion;
            RenderTexture.active = active;
        }
    }

    private void ReleaseTexture()
    {
        if (_image != null) _image.texture = null;
        if (_texture == null) return;
        _texture.Release();
        UnityEngine.Object.Destroy(_texture);
        _texture = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = 0; i < _targets.Count; i++) _targets[i].SetHighlighted(false, _layer);
        _targets.Clear();
        ReleaseTexture();
    }
}
