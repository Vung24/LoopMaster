using Snui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShopUI
{
	public class ShopCoinItem : ShopItemBase
	{
		[SerializeField] private TMP_Text _count;
		[SerializeField] private Image _coinIcon;

		protected override void Awake()
		{
			base.Awake();
			if (_count == null)
			{
				_count = HierarchyReferenceCache.ChildComponent<TMP_Text>(transform, "Count")
					?? HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Count")
					?? GetComponentInChildren<TMP_Text>(true);
			}

			if (_coinIcon == null)
			{
				_coinIcon = HierarchyReferenceCache.ChildComponent<Image>(transform, "Coin")
					?? HierarchyReferenceCache.DescendantComponent<Image>(transform, "Coin");
			}

			if (_price == null)
			{
				_price = HierarchyReferenceCache.ChildComponent<Snui_Price>(transform, "BuyBtn")
					?? HierarchyReferenceCache.DescendantComponent<Snui_Price>(transform, "BuyBtn")
					?? GetComponentInChildren<Snui_Price>(true);
			}
		}

		public override void Init(string type, IShopDelegate shopDelegate)
		{
			base.Init(type, shopDelegate);
			SetPrice(type);

			int coins = 0;
			var match = System.Text.RegularExpressions.Regex.Match(type ?? "", @"\d+");
			if (match.Success && int.TryParse(match.Value, out int parsed))
			{
				coins = parsed;
			}

			if (_count != null)
			{
				_count.text = coins > 0 ? coins.ToString("N0") : "1,000";
			}

			var lib = DataPrefabLibrary.Current;
			if (lib != null && lib.CoinSprites != null && lib.CoinSprites.Length > 0 && _coinIcon != null)
			{
				int spriteIdx = 0;
				if (coins >= 14000) spriteIdx = Mathf.Min(12, lib.CoinSprites.Length - 1);
				else if (coins >= 6500) spriteIdx = Mathf.Min(8, lib.CoinSprites.Length - 1);
				else if (coins >= 2500) spriteIdx = Mathf.Min(4, lib.CoinSprites.Length - 1);
				else if (coins >= 1200) spriteIdx = Mathf.Min(2, lib.CoinSprites.Length - 1);
				_coinIcon.sprite = lib.CoinSprites[spriteIdx];
			}
		}
	}
}