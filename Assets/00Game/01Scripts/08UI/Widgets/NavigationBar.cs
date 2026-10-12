using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace Snui
{
	public enum NavigationAnim { Default = 0, WidthChangesDynamically = 1, PoppedUp = 2 }
	public class NavigationBar : MonoBehaviour
	{
		public static NavigationBar Current;
		public UnityEvent<int> OnClickCell = new UnityEvent<int>();
		public NavigationAnim NavigationAnim = NavigationAnim.WidthChangesDynamically;
		public float CursorExpand = 1.2f;
		[SerializeField] private RectTransform _cursor;
		[SerializeField] private Transform _cellsNode;
		[SerializeField] private Transform _centerLine;
		[SerializeField] private NavigationCell[] _cells;
		private UnityAction[] _listeners;
		private Tween _cursorTween;
		private bool _layoutDirty;
		private Vector2 _lastSize;
		private RectTransform _rect;
		private float _authoredHeight;
		private Rect _lastSafeArea;
		public int CurrIndex { get; private set; } = -1;
		public int PageIndex => CurrIndex;
		public static float GetNavigationBottomOffset()
		{
			if (Current == null || Current._rect == null) return 140f;
			return Current._rect.rect.height;
		}
		private void Awake()
		{
			Current = this;
			_rect = (RectTransform)transform; _authoredHeight = _rect.sizeDelta.y;
			if (_cursor == null) _cursor = HierarchyReferenceCache.Child(transform, "Cursor") as RectTransform;
			if (_cellsNode == null) _cellsNode = HierarchyReferenceCache.Child(transform, "Buttons");
			if (_centerLine == null) _centerLine = HierarchyReferenceCache.Path(transform, "Bg", "Line");
			if (_cells == null || _cells.Length == 0) _cells = _cellsNode == null ? new NavigationCell[0] : _cellsNode.GetComponentsInChildren<NavigationCell>(true);
			_listeners = new UnityAction[_cells.Length];
			for (int i = 0; i < _cells.Length; i++)
			{
				int index = i; _listeners[i] = () => { if (CanSelect(index)) OnClickCell.Invoke(index); };
				_cells[i].OnClick.AddListener(_listeners[i]);
			}
		}
		public void SetEnabledPages(params int[] indices)
		{
			for (int i = 0; i < _cells.Length; i++) _cells[i].SetAvailable(System.Array.IndexOf(indices, i) >= 0);
		}
		private bool CanSelect(int index) => index >= 0 && _cells != null && index < _cells.Length && _cells[index] != null && _cells[index].IsAvailable;
		public void Init(int pageIndex) => Select(pageIndex, false);
		public void JumpToPage(int index) => Select(index, false);
		public void SwitchPage(int index) => Select(index, true);
		private void Select(int index, bool animate)
		{
			if (!CanSelect(index)) return;
			if (animate && CurrIndex == index) return;
			CurrIndex = index;
			ApplySafeBottom(); Canvas.ForceUpdateCanvases();
			LayoutCells(animate); MoveCursor(animate); _lastSize = _rect.rect.size; _layoutDirty = false;
		}
		private void ApplySafeBottom()
		{
			float bottom = ScreenHelper.GetCanvasSafeBottom(GetComponentInParent<Canvas>());
			_rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _authoredHeight + bottom);
			if (_cellsNode is RectTransform strip)
			{
				strip.offsetMin = new Vector2(strip.offsetMin.x, bottom);
				strip.offsetMax = new Vector2(strip.offsetMax.x, 0f);
			}
			_lastSafeArea = Screen.safeArea;
		}
		private void LayoutCells(bool animate)
		{
			if (CurrIndex < 0 || _cells == null || _cells.Length == 0 || _cellsNode == null) return;
			RectTransform strip = _cellsNode as RectTransform;
			if (strip == null || strip.rect.width <= 0f) return;
			if (_centerLine != null) _centerLine.gameObject.SetActive(NavigationAnim == Snui.NavigationAnim.Default);
			float width = strip.rect.width / _cells.Length;
			float selectedWidth = NavigationAnim == Snui.NavigationAnim.Default ? width : width * Mathf.Max(1f, CursorExpand);
			float normalWidth = NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically && _cells.Length > 1
				? (strip.rect.width - selectedWidth) / (_cells.Length - 1) : width;
			float left = strip.rect.xMin;
			for (int i = 0; i < _cells.Length; i++)
			{
				if (_cells[i] == null) continue;
				RectTransform cell = _cells[i].transform as RectTransform;
				if (cell == null) continue;
				bool selected = i == CurrIndex;
				float cellWidth = selected && NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : normalWidth;
				cell.DOKill();
				if (animate)
				{
					_cells[i].SetSelected(selected, NavigationAnim);
					cell.DOSizeDelta(new Vector2(cellWidth, cell.sizeDelta.y), 0.3f).SetEase(Ease.OutQuart).SetUpdate(true);
					cell.DOAnchorPosX(left + cellWidth * 0.5f - strip.rect.center.x, 0.3f).SetEase(Ease.OutQuart).SetUpdate(true);
				}
				else
				{
					_cells[i].Init(selected, normalWidth, NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : normalWidth);
					cell.anchoredPosition = new Vector2(left + cellWidth * 0.5f - strip.rect.center.x, 0f);
				}
				_cells[i].SetLineActive(NavigationAnim != Snui.NavigationAnim.Default && i < _cells.Length - 1);
				left += cellWidth;
			}
			if (_cursor != null) _cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selectedWidth);
		}
		private void MoveCursor(bool animate)
		{
			if (_cursor == null || !CanSelect(CurrIndex) || !(_cellsNode is RectTransform)) return;
			_cursorTween?.Kill();
			RectTransform strip = (RectTransform)_cellsNode;
			float width = strip.rect.width / _cells.Length;
			float selectedWidth = NavigationAnim == Snui.NavigationAnim.Default ? width : width * Mathf.Max(1f, CursorExpand);
			float normalWidth = NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically && _cells.Length > 1
				? (strip.rect.width - selectedWidth) / (_cells.Length - 1) : width;
			float center = strip.rect.xMin + normalWidth * CurrIndex + (NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : width) * 0.5f;
			float x = _cursor.parent.InverseTransformPoint(strip.TransformPoint(new Vector3(center, 0f, 0f))).x;
			if (animate && NavigationAnim == Snui.NavigationAnim.PoppedUp)
			{
				Vector3 pos = _cursor.localPosition; pos.x = x; _cursor.localPosition = pos;
				_cursorTween = DOTween.Sequence().Append(_cursor.DOScale(1.1f, 0.12f)).Append(_cursor.DOScale(1f, 0.08f)).SetUpdate(true);
			}
			else if (animate) _cursorTween = _cursor.DOLocalMoveX(x, 0.3f).SetEase(Ease.OutQuart).SetUpdate(true);
			else { Vector3 pos = _cursor.localPosition; pos.x = x; _cursor.localPosition = pos; }
		}
		private void OnRectTransformDimensionsChange() => _layoutDirty = true;
		private void LateUpdate()
		{
			if (_rect == null || CurrIndex < 0) return;
			Vector2 size = _rect.rect.size;
			if (size != _lastSize || _layoutDirty || Screen.safeArea != _lastSafeArea)
			{
				ApplySafeBottom(); LayoutCells(false); MoveCursor(false); _lastSize = _rect.rect.size; _layoutDirty = false;
			}
		}
		private void OnEnable() => _layoutDirty = true;
		private void OnDisable()
		{
			_cursorTween?.Kill(); _cursorTween = null;
			if (_cells != null) foreach (var cell in _cells) if (cell != null) cell.transform.DOKill();
			if (_cursor != null) _cursor.localScale = Vector3.one;
		}
		private void OnDestroy()
		{
			OnDisable();
			if (_listeners != null) for (int i = 0; i < _listeners.Length; i++) if (_cells[i] != null) _cells[i].OnClick.RemoveListener(_listeners[i]);
			if (Current == this) Current = null;
		}
	}
}
