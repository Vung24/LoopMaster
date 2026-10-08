using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class RectHorizontalMirror : MonoBehaviour
{
    public List<Transform> MirrorNodeList = new List<Transform>();
    public List<Transform> RotateNodeList = new List<Transform>();
    public List<Transform> ScaleNodeList = new List<Transform>();
    private bool _isRtl;
    private bool? directionOverride;

    private void OnEnable() { Relayout(); }
    public void SetRightToLeft(bool rtl) { directionOverride = rtl; Relayout(); }

    private void Relayout()
    {
        bool rtl = directionOverride ?? CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        if (rtl == _isRtl) return;
        _isRtl = rtl;
        if (MirrorNodeList != null)
            foreach (Transform node in MirrorNodeList) if (node != null) SetMirror(node);
        if (RotateNodeList != null)
            foreach (Transform node in RotateNodeList) if (node != null) node.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        if (ScaleNodeList != null)
            foreach (Transform node in ScaleNodeList) if (node != null)
                node.localScale = new Vector3(-node.localScale.x, node.localScale.y, node.localScale.z);
    }

    public static void SetMirror(Transform tf)
    {
        if (tf == null) return;
        RectTransform rect = tf as RectTransform;
        if (rect == null) { tf.localPosition = new Vector3(-tf.localPosition.x, tf.localPosition.y, tf.localPosition.z); return; }
        Vector2 min = rect.anchorMin;
        Vector2 max = rect.anchorMax;
        rect.anchorMin = new Vector2(1f - max.x, min.y);
        rect.anchorMax = new Vector2(1f - min.x, max.y);
        rect.pivot = new Vector2(1f - rect.pivot.x, rect.pivot.y);
        rect.anchoredPosition = new Vector2(-rect.anchoredPosition.x, rect.anchoredPosition.y);
    }
}
