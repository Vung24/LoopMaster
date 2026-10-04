using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Layout/Custom Content Size Fitter", 142)]
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class CustomContentSizeFitter : ContentSizeFitter
{
	[SerializeField] protected float _maxWidth;
	[SerializeField] protected float _minWidth;
	[SerializeField] protected float _maxHeight;
	[SerializeField] protected float _minHeight;

	private RectTransform _rectTf;
	private DrivenRectTransformTracker m_Tracker;

	public float MaxWidth
	{
		get => _maxWidth;
		set
		{
			_maxWidth = value;
			if (_minWidth > _maxWidth) _minWidth = _maxWidth;
			SetDirty();
		}
	}

	public float MinWidth
	{
		get => _minWidth;
		set
		{
			_minWidth = value;
			if (_maxWidth < _minWidth) _maxWidth = _minWidth;
			SetDirty();
		}
	}

	public float MaxHeight
	{
		get => _maxHeight;
		set
		{
			_maxHeight = value;
			if (_minHeight > _maxHeight) _minHeight = _maxHeight;
			SetDirty();
		}
	}

	public float MinHeight
	{
		get => _minHeight;
		set
		{
			_minHeight = value;
			if (_maxHeight < _minHeight) _maxHeight = _minHeight;
			SetDirty();
		}
	}

	private RectTransform rectTransform
	{
		get
		{
			if (_rectTf == null) _rectTf = GetComponent<RectTransform>();
			return _rectTf;
		}
	}

	protected override void OnDisable()
	{
		m_Tracker.Clear();
		LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
		base.OnDisable();
	}

	public override void SetLayoutHorizontal()
	{
		m_Tracker.Clear();
		if (horizontalFit == FitMode.Unconstrained) return;

		m_Tracker.Add(this, rectTransform, DrivenTransformProperties.SizeDeltaX);
		float width = horizontalFit == FitMode.MinSize
			? LayoutUtility.GetMinSize(rectTransform, 0)
			: LayoutUtility.GetPreferredSize(rectTransform, 0);

		if (_minWidth > 0 && width < _minWidth) width = _minWidth;
		if (_maxWidth > 0 && width > _maxWidth) width = _maxWidth;

		rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
	}

	public override void SetLayoutVertical()
	{
		if (verticalFit == FitMode.Unconstrained) return;

		m_Tracker.Add(this, rectTransform, DrivenTransformProperties.SizeDeltaY);
		float height = verticalFit == FitMode.MinSize
			? LayoutUtility.GetMinSize(rectTransform, 1)
			: LayoutUtility.GetPreferredSize(rectTransform, 1);

		if (_minHeight > 0 && height < _minHeight) height = _minHeight;
		if (_maxHeight > 0 && height > _maxHeight) height = _maxHeight;

		rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
	}
}