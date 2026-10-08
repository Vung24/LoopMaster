using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
	public class DailyRewardCell : MonoBehaviour
	{
		public static readonly string CURRENT_DAY_COLOR = "#FFD700";

		[SerializeField] private int _index;
		[SerializeField] private Transform _rewards;
		[SerializeField] private Transform _mask;
		[SerializeField] private Transform _tick;
		[SerializeField] private Transform _redDot;
		[SerializeField] private Transform _lock;
		[SerializeField] private Transform _frame;
		[SerializeField] private Button _button;
		[SerializeField] private TMP_Text _title;
		[SerializeField] private DailyRewardItem _rewardItemPrefab;
		private RewardState _state;
		private Tween _claimTween;
		private Vector3 _originalScale;
		public int Index => _index;

		private void Awake()
		{
			_originalScale = transform.localScale;
			if (_rewards == null) _rewards = HierarchyReferenceCache.Child(transform, "Rewards");
			if (_mask == null) _mask = HierarchyReferenceCache.Child(transform, "Mask");
			if (_tick == null) _tick = HierarchyReferenceCache.Child(transform, "Tick");
			if (_redDot == null) _redDot = HierarchyReferenceCache.Child(transform, "RedDot");
			if (_lock == null) _lock = HierarchyReferenceCache.Child(transform, "Lock");
			if (_frame == null) _frame = HierarchyReferenceCache.Child(transform, "TodayFrame");
			if (_title == null) _title = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Title");
			if (_button == null) _button = GetComponent<Button>();

			if (_button != null)
			{
				_button.onClick.AddListener(OnClicked);
			}
		}

		public void Init(int index, List<RewardItem> rewards)
		{
			_index = index;
			if (_title != null)
			{
				_title.text = $"Day {index + 1}";
			}

			CreatRewardsItems(rewards);

			var states = DailyCheckInManager.Instance.EachDayClaimState;
			if (states != null && index >= 0 && index < states.Count)
			{
				SetCellState(states[index]);
			}
		}

		private void CreatRewardsItems(List<RewardItem> rewards)
		{
			if (_rewards == null) return;

			for (int i = _rewards.childCount - 1; i >= 0; i--)
			{
				var child = _rewards.GetChild(i);
				if (child.GetComponent<DailyRewardItem>() == null) continue;
				child.gameObject.SetActive(false);
				Destroy(child.gameObject);
			}

			if (rewards == null) return;
			var prefab = _rewardItemPrefab != null ? _rewardItemPrefab.gameObject : Resources.Load<GameObject>(DailyRewardItem.Path);
			for (int rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
			{
				var reward = rewards[rewardIndex];
				if (reward == null) continue;
				var itemGo = prefab != null ? Instantiate(prefab, _rewards, false)
					: new GameObject("DailyRewardItem", typeof(RectTransform), typeof(DailyRewardItem));
				if (prefab == null) itemGo.transform.SetParent(_rewards, false);
				itemGo.SetActive(true);
				var item = itemGo.GetComponent<DailyRewardItem>();
				if (item == null) item = itemGo.AddComponent<DailyRewardItem>();
				item.Init(reward);
				if (prefab == null && _rewards.GetComponent<LayoutGroup>() == null)
				{
					var rect = itemGo.GetComponent<RectTransform>();
					rect.anchoredPosition = new Vector2((rewardIndex - (rewards.Count - 1) * 0.5f) * 80f, 0f);
				}
			}
		}

		public void SetCellState(RewardState state)
		{
			if (_state == RewardState.CanClaim && state == RewardState.Claimed) PlayClaimAnimation();
			_state = state;
			if (_button != null) _button.interactable = DailyCheckInManager.IsOpen && state == RewardState.CanClaim;
			bool isToday = DailyCheckInManager.Instance.GetTodayIndex() == _index;

			if (_frame != null)
			{
				_frame.gameObject.SetActive(isToday);
			}

			switch (state)
			{
				case RewardState.Claimed:
					if (_mask != null) _mask.gameObject.SetActive(true);
					if (_tick != null) _tick.gameObject.SetActive(true);
					if (_redDot != null) _redDot.gameObject.SetActive(false);
					if (_lock != null) _lock.gameObject.SetActive(false);
					break;

				case RewardState.CanClaim:
					if (_mask != null) _mask.gameObject.SetActive(false);
					if (_tick != null) _tick.gameObject.SetActive(false);
					if (_redDot != null) _redDot.gameObject.SetActive(true);
					if (_lock != null) _lock.gameObject.SetActive(false);
					break;

				case RewardState.NotMeetCondition:
				default:
					if (_mask != null) _mask.gameObject.SetActive(false);
					if (_tick != null) _tick.gameObject.SetActive(false);
					if (_redDot != null) _redDot.gameObject.SetActive(false);
					if (_lock != null) _lock.gameObject.SetActive(!isToday);
					break;
			}
		}

		public void OnClicked()
		{
			var states = DailyCheckInManager.Instance.EachDayClaimState;
			if (states != null && _index >= 0 && _index < states.Count)
			{
				if (states[_index] == RewardState.CanClaim)
				{
					DailyCheckInManager.Instance.ShowCollectRewards();
				}
			}
		}

		public void PlayClaimAnimation()
		{
			_claimTween?.Kill();
			transform.localScale = _originalScale;
			_claimTween = transform.DOPunchScale(Vector3.one * 0.15f, 0.35f, 5, 0.5f)
				.SetUpdate(true).SetLink(gameObject);
		}

		private void OnDisable()
		{
			_claimTween?.Kill();
			transform.localScale = _originalScale;
		}
	}
}
