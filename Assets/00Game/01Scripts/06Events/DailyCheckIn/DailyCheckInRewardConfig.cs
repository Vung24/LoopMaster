using System;

namespace Snui
{
	[Serializable]
	public class DailyCheckInRewardConfig
	{
		public RewardType RewardId;
		public string DailyRewardImgPath;

		public DailyCheckInRewardConfig()
		{
		}

		public DailyCheckInRewardConfig(RewardType rewardId, string imgPath = "")
		{
			RewardId = rewardId;
			DailyRewardImgPath = imgPath;
		}
	}
}