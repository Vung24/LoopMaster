using System.Collections.Generic;
using UnityEngine;

namespace Snui.StreakLine
{
	public class StreakLineAssetsCache : MonoBehaviour
	{
		public Sprite ProgressNodeGreenSprite, ProgressNodeBlueSprite;
		public Sprite ProgressLineGreenSprite, ProgressLineBlueSprite;
		public Sprite ChestEmptyLSprite, ChestEmptyMSprite, ChestEmptySSprite;
		public Sprite ChestRewardLSprite, ChestRewardMSprite, ChestRewardSSprite;
		public Sprite PlatformCommonSprite, PlatformAdvancedSprite;
		private Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

		// Native GetChestSize (0x15e5620): first half S, remaining M, final L.
		public Sprite GetChestSprite(int index, int count, bool claimed)
		{
			if (index == count - 1) return claimed ? ChestEmptyLSprite : ChestRewardLSprite;
			if (index < count / 2) return claimed ? ChestEmptySSprite : ChestRewardSSprite;
			return claimed ? ChestEmptyMSprite : ChestRewardMSprite;
		}

		public Sprite GetPlatformSprite(int index, int count) =>
			index == count - 1 ? PlatformAdvancedSprite : PlatformCommonSprite;

		public Sprite GetSprite(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			if (_sprites.TryGetValue(name, out var sprite)) return sprite;
			sprite = Resources.Load<Sprite>($"StreakLine/{name}");
			if (sprite != null) _sprites[name] = sprite;
			return sprite;
		}

		public void Clear()
		{
			_sprites.Clear();
		}
	}
}
