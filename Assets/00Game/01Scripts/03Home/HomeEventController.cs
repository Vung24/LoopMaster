using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public class HomeEventController : MonoBehaviour
	{
		public static int ForceGuideCount;
		public int WidgetHeight = 140;
		public int TopPadding = 0;
		public int OffsetX = 0;
		public int SpacingY = 10;
		public bool CenterAlignIcons;
		protected Snui.SpecialCol.SpecialProgress _specialProgress;
		protected Snui.ChampionArena.ChampionHomeIcon _championHomeIcon;
		protected List<HomeEventBase> _eventWidgets = new List<HomeEventBase>();
		private float _widgetScale = 1f;

		public Snui.SpecialCol.SpecialProgress SpecialProgress => _specialProgress;
		public Snui.ChampionArena.ChampionHomeIcon ChampionHomeIcon => _championHomeIcon;
		public List<HomeEventBase> EventWidgets => _eventWidgets;

		public static bool CanShowForceGuide() => ForceGuideCount == 0;

		protected virtual void Awake()
		{
			ForceGuideCount = 0;
		}

		public void SetupSpecialProgress(Snui.SpecialCol.SpecialProgress specialProgress)
		{
			_specialProgress = specialProgress;
			if (_specialProgress != null)
			{
				_specialProgress.transform.SetParent(transform, false);
			}
		}

		public void SetupChampionHomeIcon(Snui.ChampionArena.ChampionHomeIcon championHomeIcon)
		{
			_championHomeIcon = championHomeIcon;
			if (_championHomeIcon != null)
			{
				_championHomeIcon.transform.SetParent(transform, false);
			}
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

		public void ReplaceWidget(HomeEventBase oldWidget, HomeEventBase newWidget)
		{
			int idx = _eventWidgets.IndexOf(oldWidget);
			if (idx >= 0)
			{
				_eventWidgets[idx] = newWidget;
				if (newWidget != null)
				{
					newWidget.transform.SetParent(transform, false);
					newWidget.OnDestroyEvent.AddListener(OnWidgetDestroyed);
				}
				ReLayout();
			}
		}

		public async UniTask PlayEnterAnimation(bool shouldWaitAnim = false)
		{
			List<UniTask> tasks = new List<UniTask>();
			for (int i = 0; i < _eventWidgets.Count; i++)
			{
				if (_eventWidgets[i] != null)
				{
					tasks.Add(_eventWidgets[i].PlayEnterAnimation(i * 0.04f));
				}
			}
			if (shouldWaitAnim)
			{
				await UniTask.WhenAll(tasks);
			}
		}

		public async UniTask PlayEnterAnimation<T>(float delay = 0.04f)
		{
			List<UniTask> tasks = new List<UniTask>();
			int idx = 0;
			foreach (var w in _eventWidgets)
			{
				if (w is T)
				{
					tasks.Add(w.PlayEnterAnimation(idx * delay));
					idx++;
				}
			}
			await UniTask.WhenAll(tasks);
		}

		public async UniTask PlayExitAnimation<T>(float delay = 0.04f)
		{
			List<UniTask> tasks = new List<UniTask>();
			int idx = 0;
			foreach (var w in _eventWidgets)
			{
				if (w is T)
				{
					tasks.Add(w.PlayExitAnimation(idx * delay));
					idx++;
				}
			}
			await UniTask.WhenAll(tasks);
		}

		public async UniTask PlayUnlockAnimation()
		{
			await UniTask.CompletedTask;
		}

		public async UniTask PlayAddAnimation()
		{
			await UniTask.CompletedTask;
		}

		public async UniTask ShowPopupDialog()
		{
			await UniTask.CompletedTask;
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
			foreach (var w in _eventWidgets)
			{
				if (w != null)
				{
					w.CommitProgressBeforeEnterGame();
				}
			}
		}

		public async UniTask PlayIdleAnimation(float interval = 0.3f)
		{
			foreach (var w in _eventWidgets)
			{
				if (w != null)
				{
					await w.PlayIdleAnimation(interval);
				}
			}
		}

		public void ReLayout()
		{
			if (_specialProgress != null)
			{
				RectTransform spRt = _specialProgress.GetComponent<RectTransform>();
				if (spRt != null)
				{
					spRt.anchoredPosition = new Vector2(0f, 10f - TopPadding);
				}
			}

			List<HomeEventBase> leftWidgets = _eventWidgets.FindAll(w => w != null && w.IsLeftSide);
			List<HomeEventBase> rightWidgets = _eventWidgets.FindAll(w => w != null && !w.IsLeftSide);

			_widgetScale = (leftWidgets.Count >= 6 || rightWidgets.Count >= 6) ? 0.85f : 1.0f;

			foreach (var w in _eventWidgets)
			{
				if (w != null)
				{
					w.transform.localScale = Vector3.one * _widgetScale;
				}
			}

			float startY = (_specialProgress != null ? -128f : 0f) - TopPadding;
			float itemHeight = SpacingY + _widgetScale * WidgetHeight;

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