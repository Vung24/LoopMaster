using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	[RequireComponent(typeof(CanvasGroup))]
	public class HomeEventController : MonoBehaviour
	{
		public static int ForceGuideCount;
		public int WidgetHeight = 140;
		public int TopPadding = 0;
		public int OffsetX = 0;
		public int SpacingY = 10;
		public bool CenterAlignIcons;
		protected List<HomeEventBase> _eventWidgets = new List<HomeEventBase>();
		private float _widgetScale = 1f;

		public List<HomeEventBase> EventWidgets => _eventWidgets;

		public static bool CanShowForceGuide() => ForceGuideCount == 0;

		protected virtual void Awake()
		{
			ForceGuideCount = 0;
			var cg = GetComponent<CanvasGroup>();
			if (cg != null)
			{
				cg.alpha = 0f;
			}
		}

		public void ClearWidgets()
		{
			foreach (var widget in _eventWidgets)
			{
				if (widget == null) continue;
				widget.OnDestroyEvent.RemoveListener(OnWidgetDestroyed);
				widget.gameObject.SetActive(false);
				Destroy(widget.gameObject);
			}
			_eventWidgets.Clear();
		}

		public void SetupWidgets(List<HomeEventBase> widgets)
		{
			_eventWidgets = widgets ?? new List<HomeEventBase>();
			_eventWidgets.RemoveAll(e => e == null);
			foreach (var w in _eventWidgets)
			{
				w.transform.SetParent(transform, false);
				w.OnDestroyEvent.AddListener(OnWidgetDestroyed);
			}
			ReLayout();
		}

		private void OnWidgetDestroyed(HomeEventBase widget)
		{
			_eventWidgets.Remove(widget);
			ReLayout();
		}

		public async UniTask PlayEnterAnimation(bool shouldWaitAnim = false)
		{
			var cg = GetComponent<CanvasGroup>();
			if (cg != null)
			{
				cg.alpha = 1f;
			}

			List<UniTask> tasks = new List<UniTask>();
			for (int i = 0; i < _eventWidgets.Count; i++)
			{
				if (_eventWidgets[i] != null)
				{
					tasks.Add(_eventWidgets[i].PlayEnterAnimation(i * 0.04f, shouldWaitAnim));
				}
			}
			if (shouldWaitAnim)
			{
				await UniTask.WhenAll(tasks);
			}
		}

		public async UniTask ShowPopupDialog(CancellationToken cancellationToken = default)
		{
			var snapshot = new List<HomeEventBase>(_eventWidgets);
			foreach (var widget in snapshot)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (widget != null && widget.gameObject.activeInHierarchy)
				{
					await widget.ShowPopupDialog().AttachExternalCancellation(cancellationToken);
				}
			}
		}

		public bool HasNonSkippableAnimation()
		{
			foreach (var w in _eventWidgets)
			{
				if (w != null && w.HasNonSkippableAnimation())
				{
					return true;
				}
			}
			return false;
		}

		public void CommitProgressBeforeEnterGame()
		{
			var snapshot = new List<HomeEventBase>(_eventWidgets);
			foreach (var w in snapshot)
			{
				if (w != null)
				{
					w.CommitProgressBeforeEnterGame();
				}
			}
		}

		public async UniTask PlayIdleAnimation(float interval = 0.3f, CancellationToken cancellationToken = default)
		{
			foreach (var w in _eventWidgets)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (w != null && w.PlayIdleAnimation())
				{
					await UniTask.WaitForSeconds(interval, cancellationToken: cancellationToken);
				}
			}
		}

		public void ReLayout()
		{
			List<HomeEventBase> leftWidgets = _eventWidgets.FindAll(w => w != null && w.IsLeftSide);
			List<HomeEventBase> rightWidgets = _eventWidgets.FindAll(w => w != null && !w.IsLeftSide);

			_widgetScale = ScreenHelper.IsShortScreen && (leftWidgets.Count >= 6 || rightWidgets.Count >= 6) ? 0.9f : 1.0f;

			foreach (var w in _eventWidgets)
			{
				if (w != null)
				{
					w.transform.localScale = Vector3.one * _widgetScale;
				}
			}

			float startY = -TopPadding;
			float itemHeight = (int)(SpacingY + _widgetScale * WidgetHeight);

			for (int i = 0; i < leftWidgets.Count; i++)
			{
				RectTransform rt = leftWidgets[i].GetComponent<RectTransform>();
				if (rt != null)
				{
					float anchorX = CenterAlignIcons ? 0.5f : 0f;
					rt.anchorMin = new Vector2(anchorX, 1f);
					rt.anchorMax = new Vector2(anchorX, 1f);
					rt.anchoredPosition = new Vector2(-OffsetX, startY - i * itemHeight);
				}
			}

			for (int i = 0; i < rightWidgets.Count; i++)
			{
				RectTransform rt = rightWidgets[i].GetComponent<RectTransform>();
				if (rt != null)
				{
					float anchorX = CenterAlignIcons ? 0.5f : 1f;
					rt.anchorMin = new Vector2(anchorX, 1f);
					rt.anchorMax = new Vector2(anchorX, 1f);
					rt.anchoredPosition = new Vector2(OffsetX, startY - i * itemHeight);
				}
			}
		}
	}
}
