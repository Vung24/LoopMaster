using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Snui
{
	public sealed class Snui_HeartHud : Snui_BaseHud, IHeartHud
	{
		public static Snui_HeartHud Current;
		[HideInInspector] public UnityEvent AutoUpdateEvent = new UnityEvent();
		[SerializeField] private TMP_Text _timeLabel;
		[SerializeField] private TMP_Text _fullLabel;
		[SerializeField] private GameObject _infiniteGo;
		[SerializeField] private GameObject _freeHeart;
		private Coroutine _updateTime;
		private readonly WaitForSecondsRealtime _oneSecondWait = new WaitForSecondsRealtime(1f);

		protected override void Awake()
		{
			base.Awake();
			Current = this;
			HeartManager.EnsureInitialized();
			if (_timeLabel == null)
				_timeLabel = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "TimeText");
			if (_fullLabel == null)
				_fullLabel = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "FullText");
			if (_infiniteGo == null)
			{
				Transform infinite = HierarchyReferenceCache.Descendant(transform, "InfiniteIcon");
				if (infinite != null) _infiniteGo = infinite.gameObject;
			}
			if (_freeHeart == null)
			{
				Transform free = HierarchyReferenceCache.Child(transform, "FreeHeart");
				if (free != null) _freeHeart = free.gameObject;
			}
		}

		protected override void Start()
		{
			base.Start();
			OnHeartCountChanged();
		}

		private void OnEnable()
		{
			if (_updateTime == null) _updateTime = StartCoroutine(UpdateTimeLabel());
		}

		private void OnDisable()
		{
			if (_updateTime != null) StopCoroutine(_updateTime);
			_updateTime = null;
		}

		private IEnumerator UpdateTimeLabel()
		{
			while (true)
			{
				RefreshTimeLabel();
				yield return _oneSecondWait;
			}
		}

		private void RefreshTimeLabel()
		{
			var heartMgr = Managers.Heart;
			bool isInfinite = heartMgr != null && heartMgr.IsInfinite();

			if (isInfinite)
			{
				if (_infiniteGo != null) _infiniteGo.SetActive(true);
				if (_icon != null) _icon.gameObject.SetActive(false);
				if (_text != null) _text.gameObject.SetActive(false);
				if (_fullLabel != null) _fullLabel.gameObject.SetActive(false);
				if (_timeLabel != null)
				{
					_timeLabel.gameObject.SetActive(true);
					_timeLabel.SetText(TimeFormatter.FormatTimeLeft(heartMgr.InfiniteLeftTime));
				}
				if (_freeHeart != null) _freeHeart.SetActive(false);
			}
			else
			{
				if (_infiniteGo != null) _infiniteGo.SetActive(false);
				if (_icon != null) _icon.gameObject.SetActive(true);
				if (_text != null)
				{
					_text.gameObject.SetActive(true);
					_text.SetText((heartMgr != null ? heartMgr.GetHeartCount() : BaseHeartManager.DEFAULT_MAX_HEART).ToString());
				}
				bool isFull = heartMgr == null || heartMgr.IsHeartFull();
				if (isFull)
				{
					if (_timeLabel != null) _timeLabel.gameObject.SetActive(false);
					if (_fullLabel != null) _fullLabel.gameObject.SetActive(true);
				}
				else
				{
					if (_fullLabel != null) _fullLabel.gameObject.SetActive(false);
					if (_timeLabel != null)
					{
						_timeLabel.gameObject.SetActive(true);
						_timeLabel.SetText(TimeFormatter.FormatSeconds(heartMgr.GetHeartRefillTime()));
					}
				}
				if (_freeHeart != null)
				{
					int freeCount = heartMgr != null ? heartMgr.GetTotalFreeHeartCount() : 0;
					_freeHeart.SetActive(freeCount > 0);
				}
			}
		}

		public override void PlayShakeAnimation() => base.PlayShakeAnimation();
		public static void SetCurrentAutoUpdate(bool autoUpdate) { if (Current != null) Current.SetAutoUpdate(autoUpdate); }

		public override void SetAutoUpdate(bool isAuto)
		{
			base.SetAutoUpdate(isAuto);
			AutoUpdateEvent?.Invoke();
			RefreshTimeLabel();
		}

		public void OnHeartCountChanged() => RefreshTimeLabel();

		protected override void OnDestroy()
		{
			if (Current == this) Current = null;
			base.OnDestroy();
		}
	}
}
