using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Snui
{
	[Serializable]
	public struct DailyGiftReward
	{
		public List<List<RewardItem>> WeekdayRewards;
		public List<List<RewardItem>> MonthRewards;
	}

	public abstract class BaseDailyGiftManager : BaseManager
	{
		private const string PrefSignDay = "daily_gift_sign_day";
		private const string PrefSignTime = "daily_gift_sign_time";
		private const string PrefFirstLaunchTime = "daily_gift_first_launch_time";

		public List<int> SignMonthSections = new List<int>();
		public UnityEvent OnSignClaimTimeChanged = new UnityEvent();

		public abstract DailyGiftReward GetRewards();

		public override void Startup()
		{
			base.Startup();
			if (!PlayerPrefs.HasKey(PrefFirstLaunchTime))
			{
				PlayerPrefs.SetInt(PrefFirstLaunchTime, (int)TimeUtils.GetUnixTimestamp());
				PlayerPrefs.Save();
			}
			InitMonthSections();
		}

		public virtual void InitMonthSections()
		{
			SignMonthSections = new List<int> { 0, 10, 12, 16, 22, 30 };
		}

		public virtual bool IsUnlocked()
		{
			return PlayerPrefs.HasKey(PrefFirstLaunchTime) && !TimeUtils.IsToday(PlayerPrefs.GetInt(PrefFirstLaunchTime));
		}

		public bool ShouldShowIcon()
		{
			return IsUnlocked();
		}

		public UniTask<HomeEventBase> CreateIcon(Transform parent)
		{
			var prefab = EventPrefabUI.Load("DailyGiftWidget");
			return UniTask.FromResult(prefab != null ? Instantiate(prefab, parent, false).GetComponent<HomeEventBase>() : null);
		}

		public virtual bool HasNewDailyGift()
		{
			if (!IsUnlocked()) return false;
			long lastClaimTime = PlayerPrefs.GetInt(PrefSignTime, 0);
			return !TimeUtils.IsToday(lastClaimTime);
		}

		public virtual bool CanClaimToday()
		{
			return HasNewDailyGift();
		}

		public int GetSignDay()
		{
			return PlayerPrefs.GetInt(PrefSignDay, 0);
		}

		public void AddSignDay()
		{
			int current = GetSignDay();
			PlayerPrefs.SetInt(PrefSignDay, current + 1);
			PlayerPrefs.Save();
		}

		public void RefreshSignClaimTime()
		{
			PlayerPrefs.SetInt(PrefSignTime, (int)TimeUtils.GetUnixTimestamp());
			PlayerPrefs.Save();
			OnSignClaimTimeChanged?.Invoke();
		}

		public void ClaimRewardsData(List<RewardItem> rewards)
		{
			if (rewards != null)
			{
				for (int i = 0; i < rewards.Count; i++)
				{
					rewards[i]?.Claim("DailyGift");
				}
			}
		}

		public void ClaimRewardsData(int day, bool isDouble = false)
		{
			if (day == GetSignDay() + 1) TryClaimToday(isDouble);
		}

		public bool TryClaimToday(bool isDouble = false)
		{
			if (!CanClaimToday()) return false;
			int signDay = GetSignDay();
			var config = GetRewards();
			int weekday = signDay % 7;
			if (config.WeekdayRewards == null || weekday >= config.WeekdayRewards.Count) return false;
			var rewards = new List<RewardItem>();
			if (config.WeekdayRewards[weekday] != null) rewards.AddRange(config.WeekdayRewards[weekday]);
			int monthDay = signDay % 30 + 1;
			for (int i = 1; i < SignMonthSections.Count; i++)
				if (monthDay == SignMonthSections[i] && config.MonthRewards != null && i - 1 < config.MonthRewards.Count &&
				    config.MonthRewards[i - 1] != null) rewards.AddRange(config.MonthRewards[i - 1]);
			if (isDouble)
			{
				for (int i = 0; i < rewards.Count; i++)
				{
					var reward = rewards[i];
					if (reward == null || reward.Count > int.MaxValue / 2) return false;
					rewards[i] = new RewardItem(reward.Type, reward.Count * 2);
				}
			}
			if (!InventoryManager.Ensure().TryClaimRewards("DailyGift/day/" + (signDay + 1), rewards, out _)) return false;
			PlayerPrefs.SetInt(PrefSignDay, signDay + 1);
			RefreshSignClaimTime();
			return true;
		}

		public override void ClearData()
		{
			PlayerPrefs.DeleteKey(PrefSignDay);
			PlayerPrefs.DeleteKey(PrefSignTime);
			PlayerPrefs.Save();
		}
	}
}
