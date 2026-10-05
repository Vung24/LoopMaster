using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Snui
{
	public class WelcomeBackTier
	{
		public int MinDays;
		public List<RewardItem> Rewards;

		public WelcomeBackTier(int minDays, List<RewardItem> rewards)
		{
			MinDays = minDays;
			Rewards = rewards;
		}
	}

	public static class WelcomeBackHelper
	{
		public static UniTask CheckAndShow(List<WelcomeBackTier> tiers)
		{
			return UniTask.CompletedTask;
		}
	}
}