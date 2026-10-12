using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShopUI
{
	[CreateAssetMenu(fileName = "ShopBundle_New", menuName = "Shop/Bundle Package")]
	public class ShopBundleSO : ScriptableObject
	{
		[Title("Bundle Identification", "Product key and matching options")]
		[BoxGroup("Identification")]
		[SerializeField] private string _productId = "beginbundle";

		[BoxGroup("Identification")]
		[ListDrawerSettings(ShowIndexLabels = false, Expanded = false)]
		[SerializeField] private List<string> _aliases = new List<string>();

		[Title("Display & Pricing")]
		[BoxGroup("Display")]
		[SerializeField] private string _title = "SPECIAL BUNDLE";

		[BoxGroup("Display")]
		[SerializeField] private string _oldPrice = "$4.99";

		[BoxGroup("Display")]
		[SerializeField] private string _ribbonText = "LIMITED OFFER!";

		[Title("Rewards Configuration")]
		[BoxGroup("Rewards")]
		[ListDrawerSettings(ShowIndexLabels = true, AddCopiesLastElement = true)]
		[SerializeField] private List<ShopRewardEntry> _rewards = new List<ShopRewardEntry>();

		[BoxGroup("Rewards")]
		[ListDrawerSettings(ShowIndexLabels = true)]
		[InlineEditor(InlineEditorObjectFieldModes.Foldout)]
		[SerializeField] private List<ShopRewardSO> _directRewardSOs = new List<ShopRewardSO>();

		public string ProductId => _productId;
		public IReadOnlyList<string> Aliases => _aliases;
		public string Title => _title;
		public string OldPrice => _oldPrice;
		public string RibbonText => _ribbonText;
		public IReadOnlyList<ShopRewardEntry> Rewards => _rewards;
		public IReadOnlyList<ShopRewardSO> DirectRewardSOs => _directRewardSOs;

		public bool Matches(string type)
		{
			if (string.IsNullOrEmpty(type)) return false;
			string t = ShopProductCatalog.CanonicalId(type);

			if (string.Equals(ShopProductCatalog.CanonicalId(_productId), t, StringComparison.OrdinalIgnoreCase)) return true;

			if (_aliases != null)
			{
				for (int i = 0; i < _aliases.Count; i++)
				{
					if (string.Equals(ShopProductCatalog.CanonicalId(_aliases[i]), t, StringComparison.OrdinalIgnoreCase)) return true;
				}
			}

			return false;
		}

		public List<(Sprite Icon, string Amount)> GetRewardPairs()
		{
			var list = new List<(Sprite Icon, string Amount)>();

			if (_rewards != null)
			{
				for (int i = 0; i < _rewards.Count; i++)
				{
					var entry = _rewards[i];
					if (entry != null)
					{
						list.Add((entry.Icon, entry.Amount));
					}
				}
			}

			if (_directRewardSOs != null)
			{
				for (int i = 0; i < _directRewardSOs.Count; i++)
				{
					var so = _directRewardSOs[i];
					if (so != null)
					{
						list.Add((so.Icon, so.DefaultAmount));
					}
				}
			}

			return list;
		}

		public void Initialize(string productId, string title, string oldPrice, string ribbonText, IEnumerable<string> aliases = null)
		{
			_productId = productId;
			_title = title;
			_oldPrice = oldPrice;
			_ribbonText = ribbonText;
			_aliases = aliases != null ? new List<string>(aliases) : new List<string>();
		}

		public void AddReward(ShopRewardSO so, string amount = null)
		{
			if (_rewards == null) _rewards = new List<ShopRewardEntry>();
			_rewards.Add(new ShopRewardEntry(so, amount));
		}

		public void AddReward(Sprite icon, string amount)
		{
			if (_rewards == null) _rewards = new List<ShopRewardEntry>();
			_rewards.Add(new ShopRewardEntry(icon, amount));
		}
	}
}

