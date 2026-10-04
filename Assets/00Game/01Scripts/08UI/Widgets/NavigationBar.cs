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
		public NavigationAnim NavigationAnim;
		public float CursorExpand;
		[SerializeField] private RectTransform _cursor;
		[SerializeField] private Transform _cellsNode;
		[SerializeField] private NavigationCell[] _cells;
		private UnityAction[] _listeners;
		private Tween _cursorTween;
		private bool _layoutDirty;
		private Vector2 _lastSize;
		public int CurrIndex { get; private set; } = -1;
		public int PageIndex => CurrIndex;
		private void Awake()
		{
			Current = this;
			if (_cursor == null) _cursor = HierarchyReferenceCache.Child(transform, "Cursor") as RectTransform;
			if (_cellsNode == null) _cellsNode = HierarchyReferenceCache.Child(transform, "Buttons");
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
			CurrIndex = index;
			foreach (var cell in _cells) if (cell != null)
			{
				if (animate) cell.SetSelected(cell == _cells[index], Snui.NavigationAnim.Default);
				else cell.Init(cell == _cells[index], 0f, 0f);
			}
			Canvas.ForceUpdateCanvases(); LayoutDefault(); MoveCursor(animate); _layoutDirty = false;
		}
		private void LayoutDefault()
		{
			if (_cells == null || _cells.Length == 0 || _cellsNode == null) return;
			RectTransform strip = _cellsNode as RectTransform;
			if (strip == null || strip.rect.width <= 0f) return;
			float width = strip.rect.width / _cells.Length;
			foreach (var layout in strip.GetComponents<UnityEngine.UI.LayoutGroup>()) layout.enabled = false;
			var fitter = strip.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
			for (int i = 0; i < _cells.Length; i++)
			{
				RectTransform cell = _cells[i].transform as RectTransform;
				if (cell == null) continue;
				cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
				Vector3 pos = cell.localPosition; pos.x = strip.rect.xMin + width * (i + 0.5f); cell.localPosition = pos;
			}
			if (_cursor != null) _cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
		}
		private void MoveCursor(bool animate)
		{
			if (_cursor == null || !CanSelect(CurrIndex)) return;
			_cursorTween?.Kill();
			float x = _cursor.parent.InverseTransformPoint(_cells[CurrIndex].transform.position).x;
			if (animate) _cursorTween = _cursor.DOLocalMoveX(x, 0.3f).SetEase(Ease.OutQuart).SetUpdate(true);
			else { Vector3 pos = _cursor.localPosition; pos.x = x; _cursor.localPosition = pos; }
		}
		private void OnRectTransformDimensionsChange() => _layoutDirty = true;
		private void LateUpdate()
		{
			Vector2 size = ((RectTransform)transform).rect.size;
			if (size != _lastSize || _layoutDirty) { _lastSize = size; LayoutDefault(); MoveCursor(false); _layoutDirty = false; }
		}
		private void OnEnable() => _layoutDirty = true;
		private void OnDisable() { _cursorTween?.Kill(); _cursorTween = null; }
		private void OnDestroy()
		{
			OnDisable();
			if (_listeners != null) for (int i = 0; i < _listeners.Length; i++) if (_cells[i] != null) _cells[i].OnClick.RemoveListener(_listeners[i]);
			if (Current == this) Current = null;
		}
	}
}
