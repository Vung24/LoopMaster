using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Snui.ChampionArena
{
	[Serializable]
	public class ChampionArenaStage
	{
		public int Goal;
		public RewardItem Reward;
	}

	[Serializable]
	public class ChampionArenaConfig
	{
		public RewardItem[] StartRewards;
		public List<RewardItem[]> RankRewards;
		public List<ChampionArenaStage> Stages;
	}

	[Serializable]
	public class ChampionArenaData
	{
		public EventState State = EventState.WaitingStart;
		public long StartTimestamp;
		public long EndTimestamp;
		public int StartLv;
		public int Round;
		public int Score;
		public int LastScore;
		public int LastAddScore;
		public int Rank = 1;
		public int LastRank = 1;
		public List<EventRaceOpponent> Opponents = new List<EventRaceOpponent>();
		public bool IsFirstChampionArena = true;
		public RaceTutorialStage TutorialStage = RaceTutorialStage.ForceShowPrompt;
	}

	public class ChampionArenaSettings : ScriptableObject
	{
		public string TitleString;
		public string RewardTitleString;
		public string PersonalRewardTitleString;
		public AssetReferenceSprite CollectItemSprite;
		public AssetReferenceSprite DetailTopBgItemSprite;
		public AssetReferenceSprite DetailTopBgSprite;
		public AssetReferenceSprite StageTopBgSprite;
		public AssetReferenceGameObject HomeIconPrefab;
		public AssetReferenceGameObject OpenDialogContentPrefab;
		public AssetReferenceGameObject InfoDialogPrefab;
		public AssetReferenceGameObject DetailDialogPrefab;
		public AssetReferenceGameObject StageDialogContentPrefab;
		public AssetReferenceT<AudioClip> BgmClip;

		private static ChampionArenaSettings _instance;

		public static ChampionArenaSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<ChampionArenaSettings>("ChampionArenaSettings");
					if (_instance == null)
					{
						_instance = CreateInstance<ChampionArenaSettings>();
					}
				}
				return _instance;
			}
		}
	}
}