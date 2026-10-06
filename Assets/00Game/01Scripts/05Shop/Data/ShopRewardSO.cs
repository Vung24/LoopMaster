using Sirenix.OdinInspector;
using UnityEngine;

namespace ShopUI
{
	[CreateAssetMenu(fileName = "ShopReward_New", menuName = "Shop/Reward Data")]
	public class ShopRewardSO : ScriptableObject
	{
		[Title("Reward Definition", "Configurable Reward Item for Bundles")]
		[BoxGroup("General Info")]
		[SerializeField] private string _rewardId = "coin";

		[BoxGroup("General Info")]
		[SerializeField] private string _displayName = "Coins";

		[BoxGroup("Visual & Display")]
		[PreviewField(64, ObjectFieldAlignment.Center)]
		[SerializeField] private Sprite _icon;

		[BoxGroup("Visual & Display")]
		[SerializeField] private string _defaultAmount = "1,000";

		public string RewardId => _rewardId;
		public string DisplayName => _displayName;
		public Sprite Icon => _icon;
		public string DefaultAmount => _defaultAmount;

		public void Initialize(string rewardId, string displayName, Sprite icon, string defaultAmount)
		{
			_rewardId = rewardId;
			_displayName = displayName;
			_icon = icon;
			_defaultAmount = defaultAmount;
		}
	}
}

