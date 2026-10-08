using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snui.SpaceRace
{
	[CreateAssetMenu(fileName = "SpaceRaceSettings", menuName = "Snui/Events/SpaceRaceSettings")]
	public class SpaceRaceSettings : ScriptableObject
	{
		private static SpaceRaceSettings _instance;

		[SerializeField] private GameObject _openDialogPrefab;
		[SerializeField] private GameObject _detailDialogPrefab;
		[SerializeField] private GameObject _infoDialogPrefab;
		[SerializeField] private List<int> _stageDurations = new List<int> { 86400, 86400, 86400 };
		[SerializeField] private List<List<RewardItem>> _stageRewards = new List<List<RewardItem>>();
		[SerializeField] private List<List<RewardItem>> _cardPackRewards = new List<List<RewardItem>>();

		public static SpaceRaceSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<SpaceRaceSettings>("SpaceRaceSettings");
					if (_instance == null)
					{
						_instance = CreateInstance<SpaceRaceSettings>();
					}
				}
				return _instance;
			}
		}

		public GameObject OpenDialogPrefab => _openDialogPrefab;
		public GameObject DetailDialogPrefab => _detailDialogPrefab;
		public GameObject InfoDialogPrefab => _infoDialogPrefab;
		public List<int> StageDurations => _stageDurations;
		public List<List<RewardItem>> StageRewards => _stageRewards;
		public List<List<RewardItem>> CardPackRewards => _cardPackRewards;
	}
}