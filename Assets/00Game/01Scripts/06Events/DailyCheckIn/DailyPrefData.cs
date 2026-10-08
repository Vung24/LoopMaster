using System;
using System.Collections.Generic;

namespace Snui
{
	public enum RewardState
	{
		NotMeetCondition = 0,
		Claimed = 1,
		CanClaim = 2
	}

	public static class DailyPrefKeys
	{
		public static string DailyCheckInData = "DailyCheckInData";
	}

	[Serializable]
	public struct DailyPrefData
	{
		public int LoginDays;
		public int LastRecordDay;
		public List<RewardState> EachDayClaimState;
		public List<RewardState> EachBoxClaimState;
		public int Interval;
		public int Stage;
		public int BoxStageStartDay;
		public int IsTodayDialogShow;
	}

	public interface IDailyDelegate
	{
		void ShowClaimBoxReward(int boxIndex, Action callback);
		void ShowClaimDailyRewards(List<int> dayIndex, Action callback);
		void OnDailyCheckInPanelShow();
		void OnDailyCheckInPanelClose();
	}

	public interface IDailyDataProvider
	{
		List<RewardItem> GetDayRewardContents(int dayIndex);
		List<RewardItem> GetBoxRewardContents(int boxIndex);
		DailyCheckInRewardConfig GetRewardConfig(RewardType rewardId);
		bool IsRewardTypeInfinite(RewardType rewardId);
	}
}

