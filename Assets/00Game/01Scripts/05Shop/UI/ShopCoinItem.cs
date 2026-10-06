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
			int coins = ShopProductCatalog.CoinAmount(type);

			if (_count != null)
			{
				_count.text = coins.ToString();
			}
		}
	}
}
