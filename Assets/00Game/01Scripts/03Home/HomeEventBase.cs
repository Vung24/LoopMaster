using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snui
{
	public class HomeEventBase : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
	{
		public enum SideDirection
		{
			Left = 0,
			Right = 1
		}

		public enum BgColor
		{
			Blue = 0,
			Green = 1,
			Orange = 2,
			Purple = 3,
			Red = 4,
			Yellow = 5
		}

		private static readonly Dictionary<BgColor, Color> bgColorMap = new Dictionary<BgColor, Color>
		{
			{ BgColor.Blue, new Color(0.2f, 0.6f, 1f) },
			{ BgColor.Green, new Color(0.3f, 0.8f, 0.3f) },
			{ BgColor.Orange, new Color(1f, 0.5f, 0.1f) },
			{ BgColor.Purple, new Color(0.7f, 0.3f, 0.9f) },
			{ BgColor.Red, new Color(0.9f, 0.2f, 0.2f) },
			{ BgColor.Yellow, new Color(1f, 0.85f, 0.2f) }
		};

		public SideDirection Direction;
		public UnityEvent<HomeEventBase> OnDestroyEvent = new UnityEvent<HomeEventBase>();
		protected Button _button;
		protected Image _bgImage;
		protected HomeEventLock _lock;
		protected HomeIconTextBar _textBar;
		protected HomeIconNotifier _notifier;
		protected HomeIconProgress _progress;
		private bool _isPlayingLRShakeAnim;
		private Tween _downTween;
		private Tween _upTween;
		private float _originScale = 1f;

		public bool IsLeftSide => Direction == SideDirection.Left;
		public bool IsLock => _lock != null && _lock.gameObject.activeSelf;

		protected virtual void Awake()
		{
			_button = GetComponent<Button>();
			if (_button != null)
			{
				_button.onClick.AddListener(OnClick);
			}

			_bgImage = GetComponent<Image>();
			_lock = GetComponentInChildren<HomeEventLock>(true);
			_textBar = GetComponentInChildren<HomeIconTextBar>(true);
			_notifier = GetComponentInChildren<HomeIconNotifier>(true);
			_progress = GetComponentInChildren<HomeIconProgress>(true);
			_originScale = transform.localScale.x;
		}

		protected virtual void OnClick()
		{
		}

		protected void SetUnlockLevel(int lv)
		{
			int curLevel = GameProgressStore.NextPlayableLevel;
			if (curLevel < lv)
			{
				if (_lock != null)
				{
					_lock.SetUnlockLevel(lv);
				}
				if (_textBar != null)
				{
					_textBar.gameObject.SetActive(false);
				}
			}
			else
			{
				if (_lock != null)
				{
					_lock.gameObject.SetActive(false);
				}
			}
		}

		protected void SetExpireAt(long time)
		{
			if (_textBar != null)
			{
				_textBar.SetStyle(HomeIconTextBar.Style.Timer);
				_textBar.CountdownHelper?.StartCountdown(time);
			}
		}

		protected void SetBgColor(BgColor color)
		{
			if (_bgImage != null && bgColorMap.TryGetValue(color, out Color c))
			{
				_bgImage.color = c;
			}
		}

		protected void SetNotifierCount(int count)
		{
			if (_notifier == null)
			{
				CreateNotifierAsync(count).Forget();
			}
			else
			{
				_notifier.SetCount(count);
			}
		}

		private async UniTaskVoid CreateNotifierAsync(int count)
		{
			_notifier = await HomeIconNotifier.Create(transform);
			if (_notifier != null)
			{
				_notifier.SetCount(count);
			}
		}

		protected void DestroyNotifier()
		{
			if (_notifier != null)
			{
				Destroy(_notifier.gameObject);
				_notifier = null;
			}
		}

		protected void ShowExclamationMark(bool show)
		{
			if (_notifier != null)
			{
				_notifier.ShowExclamationMark(show);
			}
		}

		public void SetProgress(int current, int total)
		{
			if (_progress == null)
			{
				CreateProgressAsync(current, total).Forget();
			}
			else
			{
				_progress.SetProgress(current, total);
			}
		}

		private async UniTaskVoid CreateProgressAsync(int current, int total)
		{
			_progress = await HomeIconProgress.Create(transform);
			if (_progress != null)
			{
				_progress.SetProgress(current, total);
			}
		}

		public virtual void OnPointerDown(PointerEventData eventData)
		{
			_downTween?.Kill();
			_upTween?.Kill();
			_downTween = transform.DOScale(_originScale * 0.92f, 0.1f);
		}

		public virtual void OnPointerUp(PointerEventData eventData)
		{
			_downTween?.Kill();
			_upTween?.Kill();
			_upTween = transform.DOScale(_originScale, 0.1f);
		}

		public virtual UniTask PlayEnterAnimation(float delay = 0.04f)
		{
			transform.localScale = Vector3.zero;
			return transform.DOScale(_originScale, 0.25f).SetDelay(delay).SetEase(Ease.OutBack).ToUniTask();
		}

		public virtual UniTask PlayExitAnimation(float delay = 0.04f)
		{
			return transform.DOScale(0f, 0.2f).SetDelay(delay).SetEase(Ease.InBack).ToUniTask();
		}

		public virtual UniTask PlayIdleAnimation(float interval = 0.3f)
		{
			return transform.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), 0.3f, 4, 0.5f).ToUniTask();
		}

		public virtual bool HasNonSkippableAnimation() => false;

		public virtual void CommitProgressBeforeEnterGame()
		{
		}

		protected virtual void OnDestroy()
		{
			_downTween?.Kill();
			_upTween?.Kill();
			OnDestroyEvent?.Invoke(this);
		}
	}
}