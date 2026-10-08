using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snui.SpecialCol
{
	[Serializable]
	public class SpecialColCell
	{
		public int Count;
		public RewardItem Reward;
	}

	[Serializable]
	public class SpecialColConfig
	{
		public int CollectibleIndex;
		public List<SpecialColCell> Cells = new List<SpecialColCell>();
	}

	[Serializable]
	public class SpecialColData
	{
		public EventState State = EventState.Lock;
		public long EndTime;
		public int ConfigIndex;
		public int CurrCount;
		public int TempCount;
		public int CollectibleIndex;
		public int CompletedIndex;
		public bool IsDouble;
		public int StartLevel;
		public int LastLevel;
	}

	[CreateAssetMenu(fileName = "SpecialCollectibleSettings", menuName = "Snui/SpecialCollectibleSettings")]
	public class SpecialCollectibleSettings : ScriptableObject
	{
		private static SpecialCollectibleSettings _instance;

		[SerializeField]
		private List<Sprite> _collectibleSprites = new List<Sprite>();
		[SerializeField]
		private GameObject _detailDialogPrefab;
		[SerializeField]
		private List<string> _eventNames = new List<string>();
		[SerializeField]
		private string _eventDesc;
		[SerializeField]
		private List<SpecialColConfig> _configs = new List<SpecialColConfig>();

		public static SpecialCollectibleSettings Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = Resources.Load<SpecialCollectibleSettings>("SpecialCollectibleSettings");
				}
				return _instance;
			}
		}

		public List<Sprite> CollectibleSprites => _collectibleSprites;
		public GameObject DetailDialogPrefab => _detailDialogPrefab;
		public List<string> EventNames => _eventNames;
		public string EventDesc => _eventDesc;
		public List<SpecialColConfig> Configs => _configs;
	}
}