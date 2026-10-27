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
			_rect = (RectTransform)transform;
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
			for (int i = 0; i < _cells.Length; i++)
			{
				if (_cells[i] == null) continue;
				_cells[i].SetAvailable(System.Array.IndexOf(indices, i) >= 0);
			}
		}
		public void SetCellVisible(int index, bool visible)
		{
			if (_cells == null || index < 0 || index >= _cells.Length || _cells[index] == null) return;
			if (_cells[index].gameObject.activeSelf == visible) return;
			_cells[index].gameObject.SetActive(visible);
			_layoutDirty = true;
		}
		private int GetActiveCellCount()
		{
			if (_cells == null) return 0;
			int count = 0;
			for (int i = 0; i < _cells.Length; i++)
			{
				if (_cells[i] != null && _cells[i].gameObject.activeSelf) count++;
			}
			return count;
		}
		private bool CanSelect(int index) => index >= 0 && _cells != null && index < _cells.Length && _cells[index] != null && _cells[index].gameObject.activeSelf && _cells[index].IsAvailable;
		public void Init(int pageIndex) => Select(pageIndex, false);
		public void JumpToPage(int index) => Select(index, false);
		public void SwitchPage(int index) => Select(index, true);
		private void Select(int index, bool animate)
		{
			if (!CanSelect(index)) return;
			if (animate && CurrIndex == index) return;
			CurrIndex = index;
			Canvas.ForceUpdateCanvases();
			LayoutCells(animate); MoveCursor(animate); _lastSize = _rect.rect.size; _layoutDirty = false;
		}
		private void LayoutCells(bool animate)
		{
			if (CurrIndex < 0 || _cells == null || _cells.Length == 0 || _cellsNode == null) return;
			RectTransform strip = _cellsNode as RectTransform;
			if (strip == null || strip.rect.width <= 0f) return;
			if (_centerLine != null) _centerLine.gameObject.SetActive(NavigationAnim == Snui.NavigationAnim.Default);
			int activeCount = GetActiveCellCount();
			if (activeCount == 0) return;
			float width = strip.rect.width / activeCount;
			float selectedWidth = NavigationAnim == Snui.NavigationAnim.Default ? width : width * Mathf.Max(1f, CursorExpand);
			float normalWidth = NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically && activeCount > 1
				? (strip.rect.width - selectedWidth) / (activeCount - 1) : width;
			float left = strip.rect.xMin;
			NavigationCell prevActiveCell = null;
			for (int i = 0; i < _cells.Length; i++)
			{
				if (_cells[i] == null || !_cells[i].gameObject.activeSelf) continue;
				RectTransform cell = _cells[i].transform as RectTransform;
				if (cell == null) continue;
				bool selected = i == CurrIndex;
				float cellWidth = selected && NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : normalWidth;
				cell.DOKill();
				if (animate)
				{
					_cells[i].SetSelected(selected, NavigationAnim);
					cell.DOSizeDelta(new Vector2(cellWidth, cell.sizeDelta.y), 0.3f).SetEase(Ease.OutQuart).SetUpdate(true).SetLink(cell.gameObject);
					cell.DOAnchorPosX(left + cellWidth * 0.5f - strip.rect.center.x, 0.3f).SetEase(Ease.OutQuart).SetUpdate(true).SetLink(cell.gameObject);
				}
				else
				{
					_cells[i].Init(selected, normalWidth, NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : normalWidth);
					cell.anchoredPosition = new Vector2(left + cellWidth * 0.5f - strip.rect.center.x, 0f);
				}
				if (prevActiveCell != null)
				{
					prevActiveCell.SetLineActive(NavigationAnim != Snui.NavigationAnim.Default);
				}
				prevActiveCell = _cells[i];
				left += cellWidth;
			}
			if (prevActiveCell != null)
			{
				prevActiveCell.SetLineActive(false);
			}
			if (_cursor != null) _cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selectedWidth);
		}
		private void MoveCursor(bool animate)
		{
			if (_cursor == null || !CanSelect(CurrIndex) || !(_cellsNode is RectTransform)) return;
			_cursorTween?.Kill();
			RectTransform strip = (RectTransform)_cellsNode;
			int activeCount = GetActiveCellCount();
			if (activeCount == 0) return;
			float width = strip.rect.width / activeCount;
			float selectedWidth = NavigationAnim == Snui.NavigationAnim.Default ? width : width * Mathf.Max(1f, CursorExpand);
			float normalWidth = NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically && activeCount > 1
				? (strip.rect.width - selectedWidth) / (activeCount - 1) : width;
			float left = strip.rect.xMin;
			float targetCellX = 0f;
			for (int i = 0; i < _cells.Length; i++)
			{
				if (_cells[i] == null || !_cells[i].gameObject.activeSelf) continue;
				bool selected = i == CurrIndex;
				float cellWidth = selected && NavigationAnim == Snui.NavigationAnim.WidthChangesDynamically ? selectedWidth : normalWidth;
				if (selected)
				{
					targetCellX = left + cellWidth * 0.5f - strip.rect.center.x;
					break;
				}
				left += cellWidth;
			}
			float x = _cursor.parent.InverseTransformPoint(strip.TransformPoint(new Vector3(targetCellX, 0f, 0f))).x;
			if (animate && NavigationAnim == Snui.NavigationAnim.PoppedUp)
			{
				Vector3 pos = _cursor.localPosition; pos.x = x; _cursor.localPosition = pos;
				_cursorTween = DOTween.Sequence().Append(_cursor.DOScale(1.1f, 0.12f)).Append(_cursor.DOScale(1f, 0.08f)).SetUpdate(true).SetLink(gameObject);
			}
			else if (animate) _cursorTween = _cursor.DOLocalMoveX(x, 0.3f).SetEase(Ease.OutQuart).SetUpdate(true).SetLink(gameObject);
			else { Vector3 pos = _cursor.localPosition; pos.x = x; _cursor.localPosition = pos; }
		}
		private void OnRectTransformDimensionsChange() => _layoutDirty = true;
		private void LateUpdate()
		{
			if (_rect == null || CurrIndex < 0) return;
			Vector2 size = _rect.rect.size;
			if (size != _lastSize || _layoutDirty)
			{
				LayoutCells(false); MoveCursor(false); _lastSize = _rect.rect.size; _layoutDirty = false;
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
