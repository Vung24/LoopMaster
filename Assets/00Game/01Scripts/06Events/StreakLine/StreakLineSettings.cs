using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snui.StreakLine
{
	[Serializable]
	public class StreakLineChest
	{
		public int Level;
		public RewardItem Reward;
		public List<RewardItem> Rewards;
		public bool IsBig;
		public List<RewardItem> Items() => Rewards != null && Rewards.Count > 0 ? Rewards :
			Reward != null ? new List<RewardItem> { Reward } : new List<RewardItem>();
	}

	[Serializable]
	public class StreakLineConfig
	{
		public int MaxLevel = 10;
		public List<StreakLineChest> Chests = new List<StreakLineChest>();
	}

	[Serializable]
	public class StreakLineData
	{
		public EventState State = EventState.Lock;
		public long EndTime;
		public int CurrLevel;
		public int TempLevel;
		public int TempViewLevel;
		public int MaxReachLevel;
		public List<int> ClaimedChests = new List<int>();
		public bool HasJoined;
		public bool HasPlayedTutorial;
		public bool HasPlayedEnterAnim;
	}

	[CreateAssetMenu(fileName = "StreakLineSettings", menuName = "Snui/StreakLineSettings")]
	public class StreakLineSettings : ScriptableObject
	{
		private static StreakLineSettings _instance;

		[SerializeField]
		private GameObject _bannerPrefab;
		[SerializeField]
		private StreakLineConfig _config = new StreakLineConfig();

		public static StreakLineSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<StreakLineSettings>("StreakLineSettings");
				}
				return _instance;
			}
		}

		public GameObject BannerPrefab => _bannerPrefab;
		public StreakLineConfig Config => _config;
	}
}
