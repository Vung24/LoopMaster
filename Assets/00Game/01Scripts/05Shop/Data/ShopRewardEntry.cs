using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShopUI
{
	[Serializable]
	public class ShopRewardEntry
	{
		[HorizontalGroup("RewardRow", 56)]
		[BoxGroup("RewardRow/Icon", false)]
		[HideLabel, PreviewField(52, ObjectFieldAlignment.Center)]
		[ShowIf("@_rewardSO == null || _customIcon != null")]
		[SerializeField] private Sprite _customIcon;

		[VerticalGroup("RewardRow/Info")]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
		[LabelWidth(80)]
		[SerializeField] private ShopRewardSO _rewardSO;

		[VerticalGroup("RewardRow/Info")]
		[LabelWidth(80)]
		[SerializeField] private string _amount;

		public Sprite Icon
		{
			get
			{
				if (_customIcon != null) return _customIcon;
				if (_rewardSO != null) return _rewardSO.Icon;
				return null;
			}
		}

		public string Amount
		{
			get
			{
				if (!string.IsNullOrEmpty(_amount)) return _amount;
				if (_rewardSO != null) return _rewardSO.DefaultAmount;
				return string.Empty;
			}
		}

		public ShopRewardSO RewardSO => _rewardSO;

		public ShopRewardEntry() { }

		public ShopRewardEntry(ShopRewardSO rewardSO, string amount = null)
		{
			_rewardSO = rewardSO;
			_amount = amount;
		}

		public ShopRewardEntry(Sprite customIcon, string amount)
		{
			_customIcon = customIcon;
			_amount = amount;
		}
	}
}

