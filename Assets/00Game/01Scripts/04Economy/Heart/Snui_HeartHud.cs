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
			if (_timeLabel == null)
				_timeLabel = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(transform, "TimeText"));
			if (_fullLabel == null)
				_fullLabel = HierarchyReferenceCache.Component<TMP_Text>(HierarchyReferenceCache.Descendant(transform, "FullText"));
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
			// BaseHeartManager deliberately allows unlimited offline play.
			if (_infiniteGo != null) _infiniteGo.SetActive(true);
			if (_icon != null) _icon.gameObject.SetActive(false);
			if (_text != null) _text.gameObject.SetActive(false);
			if (_fullLabel != null) _fullLabel.gameObject.SetActive(false);
			// Native infinite hearts keep TimeText visible. Unlimited offline hearts have no expiry.
			if (_timeLabel != null) { _timeLabel.gameObject.SetActive(true); _timeLabel.SetText("Unlimited"); }
			if (_freeHeart != null) _freeHeart.SetActive(false);
		}

		public override void PlayShakeAnimation() => base.PlayShakeAnimation();
		public static void TrySetAutoUpdate(bool autoUpdate) { if (Current != null) Current.SetAutoUpdate(autoUpdate); }

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
