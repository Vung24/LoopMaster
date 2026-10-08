using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    public enum TextStyle { Percent, Number }
    public RectTransform Mask;
    public Transform Bar;
    public Transform Text;
    public TextStyle Style;
    protected Image barImage;
    protected UnityEngine.UI.Text legacyText;
    protected TMP_Text tmpText;
    protected int currCount;
    protected int totalCount;
    protected float currPercent;
    protected string formatStr;
    protected float width;
    private Tweener tweener;
    private bool objectsEnsured;
    private Vector3 originalScale;
    private RectTransform fillBounds;

    protected virtual void Awake() { EnsureObject(); }
    public int GetCurrentCount() { return currCount; }
    public int GetTotalCount() { return totalCount; }
    public int CurrentValue => currCount;
    public float Progress { get => currPercent; set => SetProgress(value); }

    public virtual void EnsureObject()
    {
        if (objectsEnsured) return;
        objectsEnsured = true;
        if (Bar != null)
        {
            barImage = Bar.GetComponent<Image>();
            originalScale = Bar.localScale;
        }
        if (Mask != null) width = Mask.rect.width;
        if (Text != null)
        {
            legacyText = Text.GetComponentInChildren<Text>(true);
            tmpText = Text.GetComponentInChildren<TMP_Text>(true);
        }
    }

    public void SetProgress(float progress) => SetPercent(progress);
    public void SetProgress(int count, int total) => SetPercent(count, total);

    // A mask's authored width can represent partial progress rather than the full track.
    public void SetFillBounds(RectTransform bounds)
    {
        fillBounds = bounds;
        ApplyPercent(currPercent);
    }

    public void SetPercent(float percent)
    {
        StopTween();
        ApplyPercent(percent);
    }

    public void SetPercent(int count, int total)
    {
        StopTween();
        totalCount = Mathf.Max(0, total);
        currCount = Mathf.Clamp(count, 0, totalCount);
        ApplyPercent(totalCount == 0 ? 0f : (float)currCount / totalCount);
    }

    private void ApplyPercent(float percent)
    {
        EnsureObject();
        currPercent = float.IsNaN(percent) ? 0f : Mathf.Clamp01(percent);
        if (totalCount > 0) currCount = Mathf.RoundToInt(currPercent * totalCount);
        if (barImage != null && barImage.type == Image.Type.Filled) barImage.fillAmount = currPercent;
        else if (Mask != null)
        {
            float fullWidth = fillBounds != null ? fillBounds.rect.width : width;
            Mask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, fullWidth) * currPercent);
        }
        else if (Bar != null) Bar.localScale = new Vector3(originalScale.x * currPercent, originalScale.y, originalScale.z);
        string label = Style == TextStyle.Number
            ? string.Format(string.IsNullOrEmpty(formatStr) ? "{0}/{1}" : formatStr, currCount, totalCount)
            : string.Format(string.IsNullOrEmpty(formatStr) ? "{0}%" : formatStr, Mathf.RoundToInt(currPercent * 100f));
        if (legacyText != null) legacyText.text = label;
        if (tmpText != null) tmpText.text = label;
    }

    public Tweener IncreaseTo(float percent, float duration = 1f, Action<float> callback = null)
    {
        StopTween();
        EnsureObject();
        float target = float.IsNaN(percent) ? 0f : Mathf.Clamp01(percent);
        tweener = DOTween.To(() => currPercent, value => { ApplyPercent(value); callback?.Invoke(value); },
            target, Mathf.Max(0f, duration)).SetUpdate(true);
        return tweener;
    }

    public Tweener IncreaseTo(int count, float duration = 1f, Action<int> callback = null)
    {
        float target = totalCount > 0 ? (float)Mathf.Clamp(count, 0, totalCount) / totalCount : 0f;
        return IncreaseTo(target, duration, value => callback?.Invoke(currCount));
    }

    public void SetFormatStr(string format) { formatStr = format; ApplyPercent(currPercent); }
    private void StopTween() { if (tweener != null) { tweener.Kill(); tweener = null; } }
    private void OnDisable() { StopTween(); }
    private void OnDestroy() { StopTween(); }
}
