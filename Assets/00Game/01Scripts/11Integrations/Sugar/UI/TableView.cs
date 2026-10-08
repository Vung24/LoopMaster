using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sugar.UI
{
	[RequireComponent(typeof(ScrollRect))]
	public class TableView : MonoBehaviour
	{
		public ITableViewDataSource DataSource;
		public float RecyclingThreshold = 100f;

		private ScrollRect _scrollRect;
		private RectTransform _viewport;
		private RectTransform _content;

		private readonly Dictionary<int, TableViewCell> _cells = new Dictionary<int, TableViewCell>();
		private readonly List<TableViewCell> _recycleBin = new List<TableViewCell>();

		private float _topPadding;
		private float _bottomPadding;

		public ScrollRect ScrollRect
		{
			get
			{
				if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
				return _scrollRect;
			}
		}

		private void Awake()
		{
			_scrollRect = GetComponent<ScrollRect>();
			if (_scrollRect != null)
			{
				_viewport = _scrollRect.viewport;
				_content = _scrollRect.content;
			}
		}

		public void ReloadData()
		{
			foreach (var cell in _cells.Values)
			{
				if (cell != null) Destroy(cell.gameObject);
			}
			_cells.Clear();
			_recycleBin.Clear();

			if (DataSource == null || _content == null) return;
			// Authored scroll prefabs may stretch Content to the viewport. A table's
			// height must be absolute, otherwise scrolling leaves an empty extra page.
			_content.anchorMin = new Vector2(0f, 1f);
			_content.anchorMax = Vector2.one;
			_content.pivot = new Vector2(0.5f, 1f);
			_content.anchoredPosition = Vector2.zero;

			int numSections = DataSource.GetNumOfSections(this);
			float currentY = _topPadding;

			for (int s = 0; s < numSections; s++)
			{
				int numCells = DataSource.GetNumOfCellsInSection(this, s);
				for (int r = 0; r < numCells; r++)
				{
					float height = DataSource.GetHeightOfCell(this, s, r);
					var cell = DataSource.GetTableViewCell(this, s, r);
					if (cell != null)
					{
						cell.transform.SetParent(_content, false);
						if (cell.RectTransform != null)
						{
							cell.RectTransform.anchorMin = new Vector2(0f, 1f);
							cell.RectTransform.anchorMax = Vector2.one;
							cell.RectTransform.pivot = new Vector2(0.5f, 1f);
							cell.RectTransform.anchoredPosition = new Vector2(0, -currentY);
							cell.RectTransform.sizeDelta = new Vector2(0f, height);
						}
						_cells[s * 1000 + r] = cell;
					}
					currentY += height;
				}
			}

			_content.sizeDelta = new Vector2(_content.sizeDelta.x, currentY + _bottomPadding);
		}

		public TableViewCell GetReusableCell(string type) => DequeueCell(type);

		public TableViewCell DequeueCell(string type)
		{
			for (int i = 0; i < _recycleBin.Count; i++)
			{
				if (_recycleBin[i] != null && _recycleBin[i].TypeIdentifier == type)
				{
					var cell = _recycleBin[i];
					_recycleBin.RemoveAt(i);
					return cell;
				}
			}
			return null;
		}
	}
}
