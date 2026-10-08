using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Snui.StreakLine
{
	public class StreakLineManager : BaseStreakLineManager
	{
		// Native game override, RVA 0x1418a38 (base library value is 26).
		public override int GetUnlockLevel() => 31;
		[Serializable] private sealed class ConfigReward { public string type; public int count; }
		[Serializable] private sealed class ConfigStage { public int Goal; public List<ConfigReward> Rewards; }

		public override void Startup()
		{
			if (CurrentConfig.Chests.Count == 0)
			{
				var asset = Resources.Load<TextAsset>("configjson/StreakLineRewardsConfig");
				if (asset != null)
				{
					var stages = JsonConvert.DeserializeObject<List<ConfigStage>>(asset.text);
					foreach (var stage in stages)
					{
						var rewards = new List<RewardItem>();
						foreach (var item in stage.Rewards)
							rewards.Add(new RewardItem(RewardType.FromString(item.type), item.count));
						CurrentConfig.Chests.Add(new StreakLineChest { Level = stage.Goal, Rewards = rewards, Reward = rewards.Count > 0 ? rewards[0] : null });
					}
				}
			}
			base.Startup();
		}
	}
}
