using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Snui
{
	[RequireComponent(typeof(CanvasGroup))]
	[RequireComponent(typeof(Button))]
	public class HomeEventBase : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler
	{
		public enum SideDirection
		{
			Left = 0,
			Right = 1
		}

		public SideDirection Direction;
		public UnityEvent<HomeEventBase> OnDestroyEvent = new UnityEvent<HomeEventBase>();
		[SerializeField] protected Button _button;
		[SerializeField] protected Image _bgImage;
		[SerializeField] protected HomeEventLock _lock;
		[SerializeField] protected HomeIconTextBar _textBar;
		[SerializeField] protected HomeIconNotifier _notifier;
		[SerializeField] protected HomeIconProgress _progress;
		[SerializeField] protected Transform _spine;
		private bool _isPlayingLRShakeAnim;
		private Tween _downTween;
		private Tween _upTween;
		private float _originScale = 1f;

		public bool IsLeftSide => Direction == SideDirection.Left;
		public bool IsLock => _lock != null && _lock.gameObject.activeSelf;

		protected virtual void Awake()
		{
			if (_button == null) _button = GetComponent<Button>();
			if (_button != null)
			{
				_button.onClick.AddListener(OnClick);
			}

			if (_bgImage == null)
			{
				Transform bgNode = HierarchyReferenceCache.Child(transform, "Bg");
				_bgImage = bgNode != null ? bgNode.GetComponent<Image>() : GetComponent<Image>();
			}

			if (_lock == null)
			{
				Transform lockNode = HierarchyReferenceCache.Child(transform, "HomeEventLock");
				_lock = lockNode != null ? lockNode.GetComponent<HomeEventLock>() : GetComponentInChildren<HomeEventLock>(true);
			}

			if (_textBar == null)
			{
				Transform textBarNode = HierarchyReferenceCache.Child(transform, "HomeIconTextBar");
				_textBar = textBarNode != null ? textBarNode.GetComponent<HomeIconTextBar>() : GetComponentInChildren<HomeIconTextBar>(true);
			}

			if (_notifier == null) _notifier = GetComponentInChildren<HomeIconNotifier>(true);
			if (_progress == null) _progress = GetComponentInChildren<HomeIconProgress>(true);
			if (_spine == null) _spine = HierarchyReferenceCache.Child(transform, "Spine");
			_originScale = transform.localScale.x;
		}

		protected virtual void OnClick()
		{
			var tutorial = GetComponent<HomeEventTutorial>();
			if (tutorial != null)
			{
				tutorial.DismissFinger();
			}
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
			if (_upTween != null && _upTween.IsActive())
			{
				_upTween.Kill();
			}
			else
			{
				_originScale = transform.localScale.x;
			}
			_downTween?.Kill();
			_downTween = transform.DOScale(_originScale * 0.92f, 0.1f).SetLink(gameObject);
		}

		public virtual void OnPointerUp(PointerEventData eventData)
		{
			_downTween?.Kill();
			_upTween?.Kill();
			_upTween = transform.DOScale(_originScale, 0.1f).SetLink(gameObject);
		}

		public virtual async UniTask PlayEnterAnimation(float delay = 0f, bool shouldWaitAnim = false)
		{
			var canvasGroup = GetComponent<CanvasGroup>();
			if (canvasGroup != null) canvasGroup.alpha = 1f;

			Vector3 origPos = transform.localPosition;
			float offsetX = Direction == SideDirection.Left ? -200f : 200f;
			float rotZ = Direction == SideDirection.Left ? -15f : 15f;
			transform.localPosition = new Vector3(origPos.x + offsetX, origPos.y, origPos.z);
			transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

			Sequence seq = DOTween.Sequence();
			seq.AppendInterval(delay);
			seq.Append(transform.DOLocalMoveX(origPos.x, 0.24f));
			seq.Join(transform.DOLocalMoveY(origPos.y + 18f, 0.14f));
			seq.Append(transform.DOLocalMoveY(origPos.y - 16f, 0.1f));
			seq.Append(transform.DOLocalMoveY(origPos.y, 0.14f));
			seq.Insert(0f, transform.DOLocalRotate(Vector3.zero, 0.64f));
			seq.SetLink(gameObject);

			if (shouldWaitAnim)
			{
				await seq.ToUniTask();
			}
		}

		public virtual UniTask PlayExitAnimation(float delay = 0.04f)
		{
			return transform.DOScale(0f, 0.2f).SetDelay(delay).SetEase(Ease.InBack).ToUniTask();
		}

		public virtual UniTask PlayAddAnimation() => UniTask.CompletedTask;

		public virtual UniTask ShowPopupDialog() => UniTask.CompletedTask;

		public virtual bool HasNonSkippableAnimation() => true;

		public virtual void SkipAnimation()
		{
		}

		public virtual bool PlayIdleAnimation() => false;

		public virtual void CommitProgressBeforeEnterGame()
		{
		}

		public Tween PlayLRShakeAnimation(int style = 0)
		{
			if (_isPlayingLRShakeAnim) return null;
			_isPlayingLRShakeAnim = true;

			Transform bg = _bgImage != null ? _bgImage.transform : null;
			Transform spine = _spine;
			if (bg == null && spine == null)
			{
				_isPlayingLRShakeAnim = false;
				return null;
			}

			Vector3 bgPos = bg != null ? bg.localPosition : Vector3.zero;
			Vector3 spinePos = spine != null ? spine.localPosition : Vector3.zero;

			float offsetX1 = Direction == SideDirection.Left ? 15f : -15f;
			float offsetX2 = Direction == SideDirection.Left ? -20f : 20f;
			float dirSign = style == 0 ? 1f : -1f;

			Sequence moveSeq = DOTween.Sequence();
			if (bg != null) moveSeq.Append(bg.DOLocalMove(new Vector3(bgPos.x + offsetX2, bgPos.y + 2f, 0f), 0.13f));
			if (spine != null) moveSeq.Join(spine.DOLocalMove(new Vector3(spinePos.x + offsetX2, spinePos.y + 2f, 0f), 0.13f));

			if (bg != null) moveSeq.Append(bg.DOLocalMove(new Vector3(bgPos.x + offsetX1, bgPos.y - 1f, 0f), 0.23f));
			if (spine != null) moveSeq.Join(spine.DOLocalMove(new Vector3(spinePos.x + offsetX1, spinePos.y - 1f, 0f), 0.23f));

			if (bg != null) moveSeq.Append(bg.DOLocalMove(bgPos, 0.17f));
			if (spine != null) moveSeq.Join(spine.DOLocalMove(spinePos, 0.17f));

			Sequence rotSeq = DOTween.Sequence();
			if (bg != null) rotSeq.Append(bg.DOLocalRotate(new Vector3(0f, 0f, dirSign * 8f), 0.13f));
			if (spine != null) rotSeq.Join(spine.DOLocalRotate(new Vector3(0f, 0f, dirSign * 8f), 0.13f));

			if (bg != null) rotSeq.Append(bg.DOLocalRotate(new Vector3(0f, 0f, -dirSign * 6f), 0.23f));
			if (spine != null) rotSeq.Join(spine.DOLocalRotate(new Vector3(0f, 0f, -dirSign * 6f), 0.23f));

			if (bg != null) rotSeq.Append(bg.DOLocalRotate(Vector3.zero, 0.17f));
			if (spine != null) rotSeq.Join(spine.DOLocalRotate(Vector3.zero, 0.17f));

			Sequence mainSeq = DOTween.Sequence();
			mainSeq.Insert(0f, moveSeq);
			mainSeq.Insert(0f, rotSeq);
			mainSeq.AppendCallback(() => _isPlayingLRShakeAnim = false);
			mainSeq.SetLink(gameObject);

			return mainSeq;
		}

		protected virtual void OnDestroy()
		{
			_downTween?.Kill();
			_upTween?.Kill();
			OnDestroyEvent?.Invoke(this);
		}
	}
}